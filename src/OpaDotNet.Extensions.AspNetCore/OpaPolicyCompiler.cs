using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using OpaDotNet.Wasm;
using OpaDotNet.Wasm.Compilation;

namespace OpaDotNet.Extensions.AspNetCore;

internal sealed class OpaPolicyCompiler : IOpaPolicyCompiler
{
    private readonly IRegoCompiler _compiler;

    private readonly ILogger _logger;

    // Has nothing to do with cancellation really but used to notify about recompilation.
    private CancellationTokenSource _changeTokenSource = new();

    private CancellationChangeToken _changeToken;

    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly ILoggerFactory _loggerFactory;

    private readonly IOptions<OpaAuthorizationOptions> _options;

    private OpaEvaluatorFactory? _factory;

    public OpaEvaluatorFactory Factory
    {
        get
        {
            if (_factory == null)
                throw new InvalidOperationException("Evaluator factory have not been initialized");

            return _factory;
        }
    }

    public OpaPolicyCompiler(
        IRegoCompiler compiler,
        IOptions<OpaAuthorizationOptions> options,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _compiler = compiler;
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<OpaPolicyCompilationService>();
        _changeToken = new(_changeTokenSource.Token);

        if (string.IsNullOrWhiteSpace(options.Value.PolicyBundlePath))
            throw new InvalidOperationException("Compiler requires OpaAuthorizationOptions.PolicyBundlePath specified");
    }

    public IChangeToken OnPolicyUpdated()
    {
        if (_changeTokenSource.IsCancellationRequested)
        {
            _changeTokenSource = new();
            _changeToken = new(_changeTokenSource.Token);
        }

        return _changeToken;
    }

    public async Task CompileBundle(bool recompiling, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Compiling");

        try
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

            var policy = await _compiler.CompileBundle(
                _options.Value.PolicyBundlePath!,
                cancellationToken: cancellationToken,
                entrypoints: _options.Value.Entrypoints
                ).ConfigureAwait(false);

            await using var _ = policy.ConfigureAwait(false);

            _factory = new OpaBundleEvaluatorFactory(
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

    public void Dispose()
    {
        _factory?.Dispose();
        _lock.Dispose();
        _changeTokenSource.Dispose();
    }
}