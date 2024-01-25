using System.Text.Json;

using JetBrains.Annotations;

using Microsoft.Extensions.Options;

using OpaDotNet.Compilation.Abstractions;
using OpaDotNet.Compilation.Interop;
using OpaDotNet.Wasm;

using Xunit.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore.Tests;

[UsedImplicitly]
public sealed class FileSystemPolicySourceTests : PathPolicySourceTests<FileSystemPolicySource>
{
    public FileSystemPolicySourceTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override FileSystemPolicySource CreatePolicySource(
        bool forceBundleWriter,
        Action<OpaAuthorizationOptions>? configure = null)
    {
        var opts = new OpaAuthorizationOptions
        {
            PolicyBundlePath = "./Watch",
            ForceBundleWriter = forceBundleWriter,
            EngineOptions = new WasmPolicyEngineOptions
            {
                SerializationOptions = new()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                },
            },
        };

        configure?.Invoke(opts);

        return new FileSystemPolicySource(
            new RegoInteropCompiler(),
            new OptionsWrapper<OpaAuthorizationOptions>(opts),
            new OpaImportsAbiFactory(),
            new OptionsWrapper<RegoCompilerOptions>(new()),
            LoggerFactory
            );
    }

    protected override async Task WritePolicy(string policy)
    {
        await File.WriteAllTextAsync("./Watch/policy.rego", policy);
    }
}