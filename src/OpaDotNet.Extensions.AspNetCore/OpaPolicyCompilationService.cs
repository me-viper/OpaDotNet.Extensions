using JetBrains.Annotations;

using Microsoft.Extensions.Hosting;

namespace OpaDotNet.Extensions.AspNetCore;

[PublicAPI]
public class OpaPolicyCompilationService : IHostedService
{
    protected IOpaPolicyCompiler Compiler { get; }

    public OpaPolicyCompilationService(IOpaPolicyCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        Compiler = compiler;
    }

    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        await Compiler.CompileBundle(false, cancellationToken).ConfigureAwait(false);
    }

    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}