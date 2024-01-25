using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using OpaDotNet.Compilation.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore;

public sealed class CompiledBundlePolicySource : PathPolicySource
{
    public CompiledBundlePolicySource(
        IRegoCompiler compiler,
        IOptions<OpaAuthorizationOptions> options,
        IOpaImportsAbiFactory importsAbiFactory,
        ILoggerFactory loggerFactory) : base(compiler, options, importsAbiFactory, loggerFactory)
    {
        var path = Options.Value.PolicyBundlePath!;

        if (!Path.IsPathRooted(Options.Value.PolicyBundlePath!))
            path = Path.GetFullPath(Options.Value.PolicyBundlePath!);

        if (!File.Exists(path))
            throw new FileNotFoundException("Policy bundle file was not found", path);

        var fileProvider = new PhysicalFileProvider(
            Path.GetDirectoryName(path)!,
            ExclusionFilters.Sensitive
            );

        var file = Path.GetFileName(path);

        if (MonitoringEnabled)
        {
            CompositeChangeToken MakePolicyChangeToken() => new(new[] { fileProvider.Watch(file), });

            void OnPolicyChange()
            {
                Logger.LogDebug("Detected changes in policy");
                NeedsRecompilation = true;
            }

            PolicyWatcher = ChangeToken.OnChange(MakePolicyChangeToken, OnPolicyChange);
        }
    }

    protected override Task<Stream?> CompileBundleFromSource(bool recompiling, CancellationToken cancellationToken = default)
    {
        var stream = new FileStream(Options.Value.PolicyBundlePath!, FileMode.Open);
        return Task.FromResult<Stream?>(stream);
    }
}