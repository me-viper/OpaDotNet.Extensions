using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using OpaDotNet.Wasm;
using OpaDotNet.Wasm.Compilation;

namespace OpaDotNet.Extensions.AspNetCore;

public class OpaPolicyBackgroundCompiler : IHostedService, IOpaPolicyCompiler
{
    private readonly IRegoCompiler _compiler;

    private readonly ILogger _logger;

    private readonly ILoggerFactory _loggerFactory;

    private readonly IOptions<OpaAuthorizationOptions> _options;

    private CancellationTokenSource _changeTokenSource = new();

    private CancellationChangeToken _changeToken;

    private readonly SemaphoreSlim _lock = new(1, 1);

    public OpaEvaluatorFactory Factory { get; private set; } = default!;

    public OpaPolicyBackgroundCompiler(
        IRegoCompiler compiler,
        IOptions<OpaAuthorizationOptions> options,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(options);

        _compiler = compiler;
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<OpaPolicyBackgroundCompiler>();
        _changeToken = new(_changeTokenSource.Token);
    }

    public IChangeToken OnRecompiled()
    {
        if (_changeTokenSource.IsCancellationRequested)
        {
            _changeTokenSource = new();
            _changeToken = new(_changeTokenSource.Token);
        }

        return _changeToken;
    }

    public async Task CompileBundle(bool recompiling, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Compiling");

        try
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

            var policy = await _compiler.CompileBundle(
                _options.Value.PolicyBundlePath,
                cancellationToken: cancellationToken,
                entrypoints: _options.Value.Entrypoints
                ).ConfigureAwait(false);

            await using var _ = policy.ConfigureAwait(false);

            Factory = new OpaBundleEvaluatorFactory(
                policy,
                loggerFactory: _loggerFactory,
                options: _options.Value.EngineOptions
                );

            if (recompiling)
            {
                _logger.LogDebug("Recompilation completed. Triggering notifications");
                _changeTokenSource.Cancel();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bundle compilation failed");
            throw;
        }
        finally
        {
            _lock.Release();
        }

        _logger.LogDebug("Compilation succeeded");
    }

    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        await CompileBundle(false, cancellationToken).ConfigureAwait(false);
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}