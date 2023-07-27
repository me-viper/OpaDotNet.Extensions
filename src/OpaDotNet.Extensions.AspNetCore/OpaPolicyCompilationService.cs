using JetBrains.Annotations;

using Microsoft.Extensions.Hosting;

namespace OpaDotNet.Extensions.AspNetCore;

/// <summary>
/// Performs initial policy bundle compilation on startup.
/// </summary>
[PublicAPI]
public class OpaPolicyCompilationService : IHostedService
{
    protected IOpaPolicyCompiler Compiler { get; }

    public OpaPolicyCompilationService(IOpaPolicyCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        Compiler = compiler;
    }

    /// <inheritdoc/>
    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        await Compiler.CompileBundle(false, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}