﻿using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

internal class PooledOpaPolicyService : IOpaPolicyService, IDisposable
{
    private ObjectPool<IOpaEvaluator> _evaluatorPool;

    private readonly ILogger _logger;

    private readonly IDisposable _recompilationMonitor;

    private readonly OpaEvaluatorPoolProvider _poolProvider;

    private readonly IOpaPolicySource _factoryProvider;

    private readonly object _syncLock = new();

    public PooledOpaPolicyService(
        IOpaPolicySource factoryProvider,
        IOptions<OpaAuthorizationOptions> options,
        OpaEvaluatorPoolProvider poolProvider,
        ILogger<PooledOpaPolicyService> logger)
    {
        ArgumentNullException.ThrowIfNull(factoryProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(poolProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _factoryProvider = factoryProvider;
        _poolProvider = poolProvider;
        _logger = logger;

        _poolProvider.MaximumRetained = options.Value.MaximumEvaluatorsRetained;

        _evaluatorPool = _poolProvider.Create(new OpaEvaluatorPoolPolicy(() => _factoryProvider.CreateEvaluator()));
        _recompilationMonitor = ChangeToken.OnChange(factoryProvider.OnPolicyUpdated, ResetPool);
    }

    private void ResetPool()
    {
        _logger.LogDebug("Recompiled. Resetting pool");

        lock (_syncLock)
        {
            var oldPool = _evaluatorPool;
            _evaluatorPool = _poolProvider.Create(new OpaEvaluatorPoolPolicy(() => _factoryProvider.CreateEvaluator()));

            if (oldPool is not IDisposable pool)
            {
                _logger.LogWarning("Pool is not disposable");
                return;
            }

            _logger.LogDebug("Disposing old pool");
            pool.Dispose();
        }
    }

    public bool EvaluatePredicate<T>(T? input, string entrypoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);

        var pool = _evaluatorPool;
        var evaluator = pool.Get();

        try
        {
            var result = evaluator.EvaluatePredicate(input, entrypoint);
            return result.Result;
        }
        finally
        {
            pool.Return(evaluator);
        }
    }

    public TOutput Evaluate<TInput, TOutput>(TInput input, string entrypoint) where TOutput : notnull
    {
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);

        var pool = _evaluatorPool;
        var evaluator = pool.Get();

        try
        {
            var result = evaluator.Evaluate<TInput, TOutput>(input, entrypoint);
            return result.Result;
        }
        finally
        {
            pool.Return(evaluator);
        }
    }

    public string EvaluateRaw(ReadOnlySpan<char> inputJson, string entrypoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);

        var pool = _evaluatorPool;
        var evaluator = pool.Get();

        try
        {
            return evaluator.EvaluateRaw(inputJson, entrypoint);
        }
        finally
        {
            pool.Return(evaluator);
        }
    }

    public void Dispose()
    {
        _recompilationMonitor.Dispose();

        if (_evaluatorPool is IDisposable d)
            d.Dispose();
    }
}