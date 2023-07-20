using Microsoft.Extensions.Primitives;

using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

public interface IOpaPolicyCompiler
{
    OpaEvaluatorFactory Factory { get; }

    IChangeToken OnRecompiled();
}