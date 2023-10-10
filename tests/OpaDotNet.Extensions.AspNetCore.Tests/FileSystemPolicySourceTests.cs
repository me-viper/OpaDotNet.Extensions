using System.Text.Json;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpaDotNet.Compilation.Abstractions;
using OpaDotNet.Compilation.Interop;
using OpaDotNet.Extensions.AspNetCore.Tests.Common;
using OpaDotNet.Wasm;

using Xunit.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore.Tests;

public sealed class FileSystemPolicySourceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly ILoggerFactory _loggerFactory;

    public FileSystemPolicySourceTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = new LoggerFactory(new[] { new XunitLoggerProvider(output) });
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", null);
    }

    private record UserPolicyInput([UsedImplicitly] string User);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Simple(bool forceBundleWriter)
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

        await File.WriteAllTextAsync("./Watch/policy.rego", Policy(0));

        using var compiler = new FileSystemPolicySource(
            new RegoInteropCompiler(),
            new OptionsWrapper<OpaAuthorizationOptions>(opts),
            new OpaImportsAbiFactory(),
            new OptionsWrapper<RegoCompilerOptions>(new()),
            _loggerFactory
            );

        await compiler.StartAsync(CancellationToken.None);

        var eval = compiler.CreateEvaluator();
        var result = eval.EvaluatePredicate(new UserPolicyInput("u0"));

        _output.WriteLine("Checking: u0");
        Assert.True(result.Result);

        await compiler.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WatchChanges(bool usePolingWatcher)
    {
        var opts = new OpaAuthorizationOptions
        {
            MonitoringInterval = TimeSpan.FromSeconds(3),
            PolicyBundlePath = "./Watch",
            EngineOptions = new WasmPolicyEngineOptions
            {
                SerializationOptions = new()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                },
            },
        };

        await File.WriteAllTextAsync("./Watch/policy.rego", Policy(0));

        if (usePolingWatcher)
            Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
        else
            Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "0");

        using var source = new FileSystemPolicySource(
            new RegoInteropCompiler(),
            new OptionsWrapper<OpaAuthorizationOptions>(opts),
            new OpaImportsAbiFactory(),
            new OptionsWrapper<RegoCompilerOptions>(new()),
            _loggerFactory
            );

        await source.StartAsync(CancellationToken.None);
        using var iterate = new AutoResetEvent(false);

        for (var i = 0; i < 3; i++)
        {
            using var eval = source.CreateEvaluator();
            var result = eval.EvaluatePredicate(new UserPolicyInput($"u{i}"));

            _output.WriteLine($"Checking: u{i}");
            Assert.True(result.Result);

            await File.WriteAllTextAsync("./Watch/policy.rego", Policy(i + 1));

            var token = source.OnPolicyUpdated();
            using var _ = token.RegisterChangeCallback(_ => iterate.Set(), null);

            iterate.WaitOne(TimeSpan.FromSeconds(10));
        }

        await source.StopAsync(CancellationToken.None);
    }

    private static string Policy(int i)
    {
        return $$"""
            package watch
            import future.keywords.if

            # METADATA
            # entrypoint: true
            user if {
                input.user == "u{{i}}"
            }
            """;
    }
}