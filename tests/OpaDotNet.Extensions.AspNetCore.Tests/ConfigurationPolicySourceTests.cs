using System.Text.Json;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpaDotNet.Compilation.Interop;
using OpaDotNet.Extensions.AspNetCore.Tests.Common;
using OpaDotNet.Wasm;

using Xunit.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore.Tests;

public class ConfigurationPolicySourceTests
{
    private readonly ITestOutputHelper _output;

    private readonly ILoggerFactory _loggerFactory;

    public ConfigurationPolicySourceTests(ITestOutputHelper output)
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

        var policyOptions = new PolicyOptions
        {
            {
                "p1",
                new()
                {
                    Package = "watch",
                    Source = Policy(0),
                    DataJson = "{}",
                    DataYaml = "",
                }
            },
        };

        var optionsMonitor = new PolicyOptionsMonitor(policyOptions);

        using var compiler = new ConfigurationPolicySource(
            new RegoInteropCompiler(),
            new OptionsWrapper<OpaAuthorizationOptions>(opts),
            optionsMonitor,
            _loggerFactory
            );

        await compiler.StartAsync(CancellationToken.None);

        for (var i = 0; i < 3; i++)
        {
            var eval = compiler.CreateEvaluator();
            var result = eval.EvaluatePredicate(new UserPolicyInput($"u{i}"));

            _output.WriteLine($"Checking: u{i}");
            Assert.True(result.Result);

            policyOptions["p1"].Source = Policy(i + 1);
            optionsMonitor.Change(policyOptions);

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        await compiler.StopAsync(CancellationToken.None);
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

    private class PolicyOptionsMonitor : IOptionsMonitor<PolicyOptions>
    {
        private Action<PolicyOptions, string?>? _listener;

        public PolicyOptions CurrentValue { get; private set; }

        public PolicyOptionsMonitor(PolicyOptions opts)
        {
            CurrentValue = opts;
        }

        public PolicyOptions Get(string? name)
        {
            return CurrentValue;
        }

        public void Change(PolicyOptions opts)
        {
            CurrentValue = opts;
            _listener?.Invoke(CurrentValue, null);
        }

        public IDisposable? OnChange(Action<PolicyOptions, string?> listener)
        {
            _listener = listener;
            return null;
        }
    }
}