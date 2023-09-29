using JetBrains.Annotations;

using Microsoft.Extensions.Options;

using OpaDotNet.Compilation.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore;

[UsedImplicitly]
public class ConfigurationPolicySource : OpaPolicySource
{
    private readonly IDisposable? _policyChangeMonitor;

    private OpaPolicyOptions _opts;

    public ConfigurationPolicySource(
        IRegoCompiler compiler,
        IOptions<OpaAuthorizationOptions> authOptions,
        IOptionsMonitor<OpaPolicyOptions> policy,
        IOpaImportsAbiFactory importsAbiFactory,
        ILoggerFactory loggerFactory) : base(compiler, authOptions, importsAbiFactory, loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(policy);

        _opts = policy.CurrentValue;
        _policyChangeMonitor = policy.OnChange(
            p =>
            {
                try
                {
                    if (!HasChanged(p, _opts))
                    {
                        Logger.LogDebug("No changes in policies configuration");
                        return;
                    }

                    _opts = p;
                    Task.Run(() => CompileBundle(true)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            );
    }

    private static bool HasChanged(OpaPolicyOptions a, OpaPolicyOptions b)
    {
        if (ReferenceEquals(a, b))
            return false;

        if (a.Keys.Count != b.Keys.Count)
            return true;

        foreach (var (k, v) in a)
        {
            if (!b.TryGetValue(k, out var ov))
                return true;

            if (!v.Equals(ov))
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    protected override async Task<Stream?> CompileBundleFromSource(bool recompiling, CancellationToken cancellationToken = default)
    {
        var hasSources = false;
        using var ms = new MemoryStream();
        var bundleWriter = new BundleWriter(ms);

        await using (bundleWriter.ConfigureAwait(false))
        {
            foreach (var (name, policy) in _opts)
            {
                if (!string.IsNullOrWhiteSpace(policy.DataJson))
                    bundleWriter.WriteEntry(policy.DataJson, $"/{policy.Package}/data.json");

                if (!string.IsNullOrWhiteSpace(policy.DataYaml))
                    bundleWriter.WriteEntry(policy.DataYaml, $"/{policy.Package}/data.yaml");

                if (!string.IsNullOrWhiteSpace(policy.Source))
                {
                    hasSources = true;
                    bundleWriter.WriteEntry(policy.Source, $"/{policy.Package}/{name}.rego");
                }
            }
        }

        if (!hasSources)
            throw new RegoCompilationException("Configuration has no policies defined");

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