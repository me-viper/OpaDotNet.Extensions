using Microsoft.Extensions.Primitives;

using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

public interface IOpaEvaluatorFactoryProvider : IDisposable
{
    /// <summary>
    /// Factory instance.
    /// </summary>
    OpaEvaluatorFactory Factory { get; }

    /// <summary>
    /// Propagates notifications that a policy recompilation has occurred.
    /// </summary>
    /// <returns></returns>
    IChangeToken OnPolicyUpdated();
}