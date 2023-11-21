﻿using Microsoft.Extensions.Options;

using OpaDotNet.Compilation.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore;

public abstract class PathPolicySource : OpaPolicySource
{
    protected IDisposable? PolicyWatcher { get; init; }

    private readonly PeriodicTimer? _changesMonitor;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    protected bool NeedsRecompilation { get; set; }

    protected bool MonitoringEnabled => Options.Value.MonitoringInterval > TimeSpan.Zero;

    protected PathPolicySource(
        IRegoCompiler compiler,
        IOptions<OpaAuthorizationOptions> options,
        IOpaImportsAbiFactory importsAbiFactory,
        ILoggerFactory loggerFactory) : base(compiler, options, importsAbiFactory, loggerFactory)
    {
        if (string.IsNullOrWhiteSpace(options.Value.PolicyBundlePath))
        {
            throw new InvalidOperationException(
                $"{GetType()} requires OpaAuthorizationOptions.PolicyBundlePath specified"
                );
        }

        if (MonitoringEnabled)
            _changesMonitor = new(Options.Value.MonitoringInterval);
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
                if (!NeedsRecompilation)
                    continue;

                if (cancellationToken.IsCancellationRequested)
                    break;

                NeedsRecompilation = false;

                Logger.LogDebug("Detected changes. Recompiling");
                await CompileBundle(true, cancellationToken).ConfigureAwait(false);
                Logger.LogDebug("Recompilation succeeded");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to process policy changes");
                NeedsRecompilation = true;
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
                PolicyWatcher?.Dispose();
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
            _ = Task.Run(() => TrackPolicyChanged(_cancellationTokenSource.Token), cancellationToken);
        }
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
        await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
#else
        _cancellationTokenSource.Cancel();
#endif

        Logger.LogDebug("Stopped");
    }
}