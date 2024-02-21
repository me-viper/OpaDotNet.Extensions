using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using OpaDotNet.Compilation.Abstractions;
using OpaDotNet.Extensions.AspNetCore.Telemetry;

namespace OpaDotNet.Extensions.AspNetCore;

public sealed class FileSystemPolicySource : PathPolicySource
{
    private readonly IOptions<RegoCompilerOptions> _compilerOptions;

    public FileSystemPolicySource(
        IRegoCompiler compiler,
        IOptions<OpaAuthorizationOptions> options,
        IOpaImportsAbiFactory importsAbiFactory,
        IOptions<RegoCompilerOptions> compilerOptions,
        ILoggerFactory loggerFactory) : base(compiler, options, importsAbiFactory, loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(compilerOptions);

        var path = Options.Value.PolicyBundlePath!;
        _compilerOptions = compilerOptions;

        if (!Path.IsPathRooted(Options.Value.PolicyBundlePath!))
            path = Path.GetFullPath(Options.Value.PolicyBundlePath!);

        var fileProvider = new PhysicalFileProvider(
            path,
            ExclusionFilters.Sensitive
            );

        if (MonitoringEnabled)
        {
            CompositeChangeToken MakePolicyChangeToken() => new(
                new[]
                {
                    fileProvider.Watch("**/*.rego"),
                    fileProvider.Watch("**/data.json"),
                    fileProvider.Watch("**/data.yaml"),
                }
                );

            void OnPolicyChange()
            {
                Logger.BundleCompilationHasChanges();
                NeedsRecompilation = true;
            }

            PolicyWatcher = ChangeToken.OnChange(MakePolicyChangeToken, OnPolicyChange);
        }
    }

    protected override async Task<Stream?> CompileBundleFromSource(bool recompiling, CancellationToken cancellationToken = default)
    {
        Stream? capsStream = null;

        try
        {
            if (ImportsAbiFactory.Capabilities != null)
                capsStream = ImportsAbiFactory.Capabilities();

            var parameters = new CompilationParameters
            {
                IsBundle = true,
                Entrypoints = Options.Value.Entrypoints,
                CapabilitiesStream = capsStream,
            };

            if (!Options.Value.ForceBundleWriter)
            {
                return await Compiler.Compile(
                    Options.Value.PolicyBundlePath!,
                    parameters,
                    cancellationToken: cancellationToken
                    ).ConfigureAwait(false);
            }
            else
            {
                using var ms = new MemoryStream();

                var bundle = BundleWriter.FromDirectory(
                    ms,
                    Options.Value.PolicyBundlePath!,
                    _compilerOptions.Value.Ignore
                    );

                await bundle.DisposeAsync().ConfigureAwait(false);
                ms.Seek(0, SeekOrigin.Begin);

                return await Compiler.Compile(ms, parameters, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (capsStream != null)
                await capsStream.DisposeAsync().ConfigureAwait(false);
        }
    }
}