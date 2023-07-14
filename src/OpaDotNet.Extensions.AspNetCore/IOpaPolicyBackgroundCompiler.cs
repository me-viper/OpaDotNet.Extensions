using Microsoft.Extensions.Primitives;

using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

public interface IOpaPolicyBackgroundCompiler
{
    OpaEvaluatorFactoryBase Factory { get; }
    
    IChangeToken OnRecompiled();
}