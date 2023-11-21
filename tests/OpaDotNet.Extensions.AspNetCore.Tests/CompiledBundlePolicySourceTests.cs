using System.Text.Json;

using JetBrains.Annotations;

using Microsoft.Extensions.Options;

using OpaDotNet.Compilation.Abstractions;
using OpaDotNet.Compilation.Interop;
using OpaDotNet.Wasm;

using Xunit.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore.Tests;

[UsedImplicitly]
public class CompiledBundlePolicySourceTests : PathPolicySourceTests<CompiledBundlePolicySource>
{
    public CompiledBundlePolicySourceTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override CompiledBundlePolicySource CreatePolicySource(
        bool forceBundleWriter,
        Action<OpaAuthorizationOptions>? configure = null)
    {
        var opts = new OpaAuthorizationOptions
        {
            PolicyBundlePath = "./Watch/policy.tar.gz",
            EngineOptions = new WasmPolicyEngineOptions
            {
                SerializationOptions = new()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                },
            },
        };

        configure?.Invoke(opts);

        return new CompiledBundlePolicySource(
            new RegoInteropCompiler(),
            new OptionsWrapper<OpaAuthorizationOptions>(opts),
            new OpaImportsAbiFactory(),
            LoggerFactory
            );
    }

    protected override async Task WritePolicy(string policy)
    {
        var compiler = new RegoInteropCompiler();
        await using var bundle = await compiler.CompileSource(policy);

        await using var fs = new FileStream("./Watch/policy.tar.gz", FileMode.Create);
        await bundle.CopyToAsync(fs);
    }
}