using System.Collections.Concurrent;

using JetBrains.Annotations;

using Microsoft.Extensions.Options;

namespace OpaDotNet.Extensions.AspNetCore;

[PublicAPI]
public sealed class OpaPolicyWatchingCompilationService : OpaPolicyCompilationService, IDisposable
{
    private readonly ILogger _logger;

    private readonly FileSystemWatcher _policyWatcher;

    private readonly ConcurrentBag<string> _changes = new();

    private readonly PeriodicTimer _changesMonitor;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly IOptions<OpaAuthorizationOptions> _options;

    public OpaPolicyWatchingCompilationService(
        IOpaPolicyCompiler compiler,
        IOptions<OpaAuthorizationOptions> options,
        ILogger<OpaPolicyWatchingCompilationService> logger) : base(compiler)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _options = options;

        if (string.IsNullOrWhiteSpace(options.Value.PolicyBundlePath))
            throw new InvalidOperationException("Compiler requires OpaAuthorizationOptions.PolicyBundlePath specified");

        _policyWatcher = new()
        {
            Path = _options.Value.PolicyBundlePath!,
            Filters = { "*.rego", "data.json" },
            NotifyFilter = NotifyFilters.LastWrite,
            IncludeSubdirectories = true,
        };

        _policyWatcher.Changed += PolicyChanged;
        _changesMonitor = new(_options.Value.MonitoringInterval);
    }

    private void PolicyChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
            return;

        var changedFile = new FileInfo(e.FullPath);

        if (!changedFile.Exists)
            return;

        if (_cancellationTokenSource.Token.IsCancellationRequested)
            return;

        _logger.LogDebug("Detected policy change in {File}. Stashing until next recompilation cycle", e.FullPath);
        _changes.Add(changedFile.FullName);
    }

    private async Task TrackPolicyChanged(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Watching for policy changes in {Path}", _options.Value.PolicyBundlePath);

        while (await _changesMonitor.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                if (_changes.IsEmpty)
                    continue;

                if (cancellationToken.IsCancellationRequested)
                    break;

                _logger.LogDebug("Detected changes. Recompiling");
                await Compiler.CompileBundle(true, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Recompilation succeeded");

                _changes.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process policy changes");
            }
        }

        _logger.LogDebug("Stopped watching for policy changes");
    }

    public void Dispose()
    {
        _changesMonitor.Dispose();
        _policyWatcher.Dispose();
        _cancellationTokenSource.Dispose();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken).ConfigureAwait(false);

        _policyWatcher.EnableRaisingEvents = true;
        _ = Task.Run(() => TrackPolicyChanged(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        _policyWatcher.EnableRaisingEvents = false;
        _cancellationTokenSource.Cancel();
        _logger.LogDebug("Stopped");
    }
}