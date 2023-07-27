using JetBrains.Annotations;

using Microsoft.Extensions.Options;

namespace OpaDotNet.Extensions.AspNetCore;

[PublicAPI]
public sealed class OpaPolicyWatchingCompilationService : OpaPolicyCompilationService, IDisposable
{
    private readonly ILogger _logger;

    private readonly FileSystemWatcher _policyWatcher;

    private readonly PeriodicTimer _changesMonitor;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly IOptions<OpaAuthorizationOptions> _options;

    private bool _needsRecompilation;

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
            Filters = { "*.rego", "data.json", "data.yaml" },
            NotifyFilter = NotifyFilters.LastWrite,
            IncludeSubdirectories = true,
        };

        _policyWatcher.Changed += PolicyChanged;
        _changesMonitor = new(_options.Value.MonitoringInterval);
    }

    private void PolicyChanged(object sender, FileSystemEventArgs e)
    {
        if (_cancellationTokenSource.Token.IsCancellationRequested)
            return;

        _logger.LogDebug(
            "Detected policy change {Change} in {File}. Stashing until next recompilation cycle",
            e.ChangeType,
            e.FullPath
            );

        _needsRecompilation = true;
    }

    private async Task TrackPolicyChanged(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Watching for policy changes in {Path}", _options.Value.PolicyBundlePath);

        while (await _changesMonitor.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                if (!_needsRecompilation)
                    continue;

                if (cancellationToken.IsCancellationRequested)
                    break;

                _needsRecompilation = false;

                _logger.LogDebug("Detected changes. Recompiling");
                await Compiler.CompileBundle(true, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Recompilation succeeded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process policy changes");
                _needsRecompilation = true;
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

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken).ConfigureAwait(false);

        _policyWatcher.EnableRaisingEvents = true;
        _ = Task.Run(() => TrackPolicyChanged(_cancellationTokenSource.Token), cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        _policyWatcher.EnableRaisingEvents = false;
        _cancellationTokenSource.Cancel();
        _logger.LogDebug("Stopped");
    }
}