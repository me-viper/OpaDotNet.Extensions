namespace OpaDotNet.Extensions.AspNetCore;

public interface IOpaPolicyCompiler : IOpaEvaluatorFactoryProvider
{
    Task CompileBundle(bool recompiling, CancellationToken cancellationToken = default);
}