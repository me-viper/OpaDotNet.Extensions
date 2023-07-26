using System.Text.Json;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpaDotNet.Extensions.AspNetCore.Tests.Common;
using OpaDotNet.Wasm;
using OpaDotNet.Wasm.Compilation;

using Xunit.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore.Tests;

public class OpaPolicyWatchingCompilationServiceTests
{
    private readonly ITestOutputHelper _output;

    private readonly ILoggerFactory _loggerFactory;

    public OpaPolicyWatchingCompilationServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = new LoggerFactory(new[] { new XunitLoggerProvider(output) });
    }

    private record UserPolicyInput([UsedImplicitly] string User);

    [Fact]
    public async Task WatchChanges()
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

        using var compiler = new OpaPolicyCompiler(
            new RegoCliCompiler(),
            new OptionsWrapper<OpaAuthorizationOptions>(opts),
            _loggerFactory
            );

        using var svc = new OpaPolicyWatchingCompilationService(
            compiler,
            new OptionsWrapper<OpaAuthorizationOptions>(opts),
            _loggerFactory.CreateLogger<OpaPolicyWatchingCompilationService>()
            );

        await svc.StartAsync(CancellationToken.None);

        for (var i = 0; i < 3; i++)
        {
            var eval = compiler.Factory.Create();
            var result = eval.EvaluatePredicate(new UserPolicyInput($"u{i}"));

            _output.WriteLine($"Checking: u{i}");
            Assert.True(result.Result);

            await File.WriteAllTextAsync("./Watch/policy.rego", Policy(i + 1));
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        await svc.StopAsync(CancellationToken.None);
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