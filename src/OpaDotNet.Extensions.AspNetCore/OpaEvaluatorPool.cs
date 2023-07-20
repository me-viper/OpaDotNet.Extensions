using Microsoft.Extensions.ObjectPool;

using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

public class OpaEvaluatorPoolPolicy : PooledObjectPolicy<IOpaEvaluator>
{
    private readonly Func<IOpaEvaluator> _factory;

    public OpaEvaluatorPoolPolicy(Func<IOpaEvaluator> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    public override IOpaEvaluator Create()
    {
        return _factory();
    }

    public override bool Return(IOpaEvaluator obj)
    {
        return true;
    }
}