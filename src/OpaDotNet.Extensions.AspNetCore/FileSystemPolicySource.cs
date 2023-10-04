﻿using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using OpaDotNet.Compilation.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore;

public sealed class FileSystemPolicySource : OpaPolicySource
{
    private readonly IDisposable? _policyWatcher;

    private readonly PeriodicTimer? _changesMonitor;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private bool _needsRecompilation;

    private bool MonitoringEnabled => Options.Value.MonitoringInterval > TimeSpan.Zero;

    public FileSystemPolicySource(
        IRegoCompiler compiler,
        IOptions<OpaAuthorizationOptions> options,
        IOpaImportsAbiFactory importsAbiFactory,
        ILoggerFactory loggerFactory) : base(compiler, options, importsAbiFactory, loggerFactory)
    {
        if (string.IsNullOrWhiteSpace(options.Value.PolicyBundlePath))
            throw new InvalidOperationException("Compiler requires OpaAuthorizationOptions.PolicyBundlePath specified");

        var path = Options.Value.PolicyBundlePath!;

        if (!Path.IsPathRooted(Options.Value.PolicyBundlePath!))
            path = Path.GetFullPath(Options.Value.PolicyBundlePath!);

        var fileProvider = new PhysicalFileProvider(
            path,
            ExclusionFilters.Sensitive
            );

        if (MonitoringEnabled)
        {
            _policyWatcher = ChangeToken.OnChange(() => fileProvider.Watch("**/*.*"), OnPolicyChange);
            _changesMonitor = new(Options.Value.MonitoringInterval);
        }

        void OnPolicyChange()
        {
            Logger.LogDebug("Detected changes in policy");
            _needsRecompilation = true;
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
                _policyWatcher?.Dispose();
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

        _cancellationTokenSource.Cancel();
        Logger.LogDebug("Stopped");
    }
}