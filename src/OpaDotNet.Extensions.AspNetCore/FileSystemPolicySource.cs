using Microsoft.Extensions.Options;

using OpaDotNet.Compilation.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore;

public sealed class FileSystemPolicySource : OpaPolicySource
{
    private readonly FileSystemWatcher _policyWatcher;

    private readonly PeriodicTimer? _changesMonitor;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private bool _needsRecompilation;

    private bool MonitoringEnabled => Options.Value.MonitoringInterval > TimeSpan.Zero;

    public FileSystemPolicySource(
        IRegoCompiler compiler,
        IOptions<OpaAuthorizationOptions> options,
        ILoggerFactory loggerFactory) : base(compiler, options, loggerFactory)
    {
        if (string.IsNullOrWhiteSpace(options.Value.PolicyBundlePath))
            throw new InvalidOperationException("Compiler requires OpaAuthorizationOptions.PolicyBundlePath specified");

        _policyWatcher = new()
        {
            Path = Options.Value.PolicyBundlePath!,
            Filters = { "*.rego", "data.json", "data.yaml" },
            NotifyFilter = NotifyFilters.LastWrite,
            IncludeSubdirectories = true,
        };

        if (MonitoringEnabled)
        {
            _policyWatcher.Changed += PolicyChanged;
            _changesMonitor = new(Options.Value.MonitoringInterval);
        }
    }

    protected override async Task<Stream?> CompileBundleFromSource(bool recompiling, CancellationToken cancellationToken = default)
    {
        return await Compiler.CompileBundle(
            Options.Value.PolicyBundlePath!,
            cancellationToken: cancellationToken,
            entrypoints: Options.Value.Entrypoints
            ).ConfigureAwait(false);
    }

    private void PolicyChanged(object sender, FileSystemEventArgs e)
    {
        if (_cancellationTokenSource.Token.IsCancellationRequested)
            return;

        Logger.LogDebug(
            "Detected policy change {Change} in {File}. Stashing until next recompilation cycle",
            e.ChangeType,
            e.FullPath
            );

        _needsRecompilation = true;
    }

    private async Task TrackPolicyChanged(CancellationToken cancellationToken)
    {
        if (!MonitoringEnabled || _changesMonitor == null)
            return;

        Logger.LogDebug("Watching for policy changes in {Path}", Options.Value.PolicyBundlePath);

        while (await _changesMonitor.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                if (!_needsRecompilation)
                    continue;

                if (cancellationToken.IsCancellationRequested)
                    break;

                _needsRecompilation = false;

                Logger.LogDebug("Detected changes. Recompiling");
                await CompileBundle(true, cancellationToken).ConfigureAwait(false);
                Logger.LogDebug("Recompilation succeeded");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to process policy changes");
                _needsRecompilation = true;
            }
        }

        Logger.LogDebug("Stopped watching for policy changes");
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                _changesMonitor?.Dispose();
                _policyWatcher.Dispose();
                _cancellationTokenSource.Dispose();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    /// <inheritdoc/>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken).ConfigureAwait(false);

        if (MonitoringEnabled)
        {
            _policyWatcher.EnableRaisingEvents = true;
            _ = Task.Run(() => TrackPolicyChanged(_cancellationTokenSource.Token), cancellationToken);
        }
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        _policyWatcher.EnableRaisingEvents = false;
        _cancellationTokenSource.Cancel();
        Logger.LogDebug("Stopped");
    }
}