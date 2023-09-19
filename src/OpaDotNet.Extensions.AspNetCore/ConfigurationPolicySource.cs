using JetBrains.Annotations;

using Microsoft.Extensions.Options;

using OpaDotNet.Compilation.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore;

[UsedImplicitly]
public class ConfigurationPolicySource : OpaPolicySource
{
    private readonly IOptionsMonitor<OpaPolicyOptions> _policy;

    private readonly IDisposable? _policyChangeMonitor;

    public ConfigurationPolicySource(
        IRegoCompiler compiler,
        IOptions<OpaAuthorizationOptions> authOptions,
        IOptionsMonitor<OpaPolicyOptions> policy,
        ILoggerFactory loggerFactory) : base(compiler, authOptions, loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(policy);

        _policy = policy;
        _policyChangeMonitor = _policy.OnChange(
            _ =>
            {
                try
                {
                    Task.Run(() => CompileBundle(true)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            );
    }

    /// <inheritdoc />
    protected override async Task<Stream?> CompileBundleFromSource(bool recompiling, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        var bundleWriter = new BundleWriter(ms);

        await using (bundleWriter.ConfigureAwait(false))
        {
            foreach (var (name, policy) in _policy.CurrentValue)
            {
                if (!string.IsNullOrWhiteSpace(policy.DataJson))
                    bundleWriter.WriteEntry(policy.DataJson, Path.Combine(policy.Package ?? "", "/data.json"));

                if (!string.IsNullOrWhiteSpace(policy.DataYaml))
                    bundleWriter.WriteEntry(policy.DataYaml, Path.Combine(policy.Package ?? "", "/data.yaml"));

                if (!string.IsNullOrWhiteSpace(policy.Source))
                    bundleWriter.WriteEntry(policy.Source, Path.Combine(policy.Package ?? "", $"/{name}.rego"));
            }
        }

        ms.Seek(0, SeekOrigin.Begin);

        var result = await Compiler.CompileStream(
            ms,
            entrypoints: Options.Value.Entrypoints,
            cancellationToken: cancellationToken
            ).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
                _policyChangeMonitor?.Dispose();
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}