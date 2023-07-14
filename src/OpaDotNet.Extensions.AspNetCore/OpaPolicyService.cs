using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

internal class OpaPolicyService : IOpaPolicyService, IDisposable
{
    private readonly IOptions<OpaPolicyHandlerOptions> _options;

    private readonly ObjectPool<IOpaEvaluator> _evaluatorPool;
    
    private readonly ILogger _logger;
    
    private readonly IDisposable _recompilationMonitor;

    public OpaPolicyService(
        IOpaPolicyBackgroundCompiler compiler,
        IOptions<OpaPolicyHandlerOptions> options,
        ObjectPoolProvider poolProvider,
        ILogger<OpaPolicyService> logger)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(poolProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _evaluatorPool = poolProvider.Create(new OpaEvaluatorPoolPolicy(() => compiler.Factory.Create()));
        _logger = logger;
        
        _recompilationMonitor = ChangeToken.OnChange(compiler.OnRecompiled, ResetPool);
    }
    
    private void ResetPool()
    {
        _logger.LogDebug("Recompiled. Resetting pool");
    }
    
    public bool EvaluatePredicate<T>(T? input, string entrypoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);

        var evaluator = _evaluatorPool.Get();

        try
        {
            var result = evaluator.EvaluatePredicate(input, entrypoint);
            return result.Result;
        }
        finally
        {
            _evaluatorPool.Return(evaluator);
        }
    }

    public TOutput Evaluate<TInput, TOutput>(TInput input, string entrypoint) where TOutput : notnull
    {
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);

        var evaluator = _evaluatorPool.Get();

        try
        {
            var result = evaluator.Evaluate<TInput, TOutput>(input, entrypoint);
            return result.Result;
        }
        finally
        {
            _evaluatorPool.Return(evaluator);
        }
    }

    public string EvaluateRaw(ReadOnlySpan<char> inputJson, string entrypoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);

        var evaluator = _evaluatorPool.Get();

        try
        {
            return evaluator.EvaluateRaw(inputJson, entrypoint);
        }
        finally
        {
            _evaluatorPool.Return(evaluator);
        }
    }

    public void Dispose()
    {
        _recompilationMonitor.Dispose();
    }
}