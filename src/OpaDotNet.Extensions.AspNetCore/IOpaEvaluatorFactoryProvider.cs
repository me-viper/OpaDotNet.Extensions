using Microsoft.Extensions.Primitives;

using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

public interface IOpaEvaluatorFactoryProvider : IDisposable
{
    OpaEvaluatorFactory Factory { get; }

    IChangeToken OnPolicyUpdated();
}