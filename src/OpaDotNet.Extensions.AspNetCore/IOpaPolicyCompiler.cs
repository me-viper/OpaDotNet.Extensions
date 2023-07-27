namespace OpaDotNet.Extensions.AspNetCore;

public interface IOpaPolicyCompiler : IOpaEvaluatorFactoryProvider
{
    /// <summary>
    /// Performs policy bundle compilation.
    /// </summary>
    /// <param name="recompiling">
    /// <c>true</c> if it's first time bundle is compiled; otherwise <c>false</c>
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    Task CompileBundle(bool recompiling, CancellationToken cancellationToken = default);
}