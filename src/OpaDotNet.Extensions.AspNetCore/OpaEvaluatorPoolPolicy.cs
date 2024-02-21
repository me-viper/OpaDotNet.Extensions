using Microsoft.Extensions.ObjectPool;

using OpaDotNet.Extensions.AspNetCore.Telemetry;
using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

internal class OpaEvaluatorPoolPolicy : PooledObjectPolicy<IOpaEvaluator>
{
    private readonly Func<IOpaEvaluator> _factory;

    public OpaEvaluatorPoolPolicy(Func<IOpaEvaluator> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    public override IOpaEvaluator Create()
    {
        OpaEventSource.Log.EvaluatorCreated();
        return _factory();
    }

    public override bool Return(IOpaEvaluator obj)
    {
        OpaEventSource.Log.EvaluatorReleased();
        return true;
    }
}