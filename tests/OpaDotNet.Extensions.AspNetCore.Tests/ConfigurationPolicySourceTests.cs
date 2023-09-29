﻿using System.Text.Json;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpaDotNet.Compilation.Abstractions;
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
    public async Task NoPolicies()
    {
        var policyOptions = new OpaPolicyOptions();
        var optionsMonitor = new PolicyOptionsMonitor(policyOptions);

        using var compiler = new ConfigurationPolicySource(
            new RegoInteropCompiler(
                null,
                _loggerFactory.CreateLogger<RegoInteropCompiler>()
                ),
            new OptionsWrapper<OpaAuthorizationOptions>(new()),
            optionsMonitor,
            new OpaImportsAbiFactory(),
            _loggerFactory
            );

        await Assert.ThrowsAsync<RegoCompilationException>(() => compiler.StartAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(2, "p1/t1/allow")]
    [InlineData(3, "p2/allow")]
    [InlineData(4, "p2/allow2")]
    public async Task Configuration(int data, string entrypoint)
    {
        var opts = new OpaAuthorizationOptions
        {
            EngineOptions = new WasmPolicyEngineOptions
            {
                SerializationOptions = new()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                },
            },
        };

        var policyOptions = new OpaPolicyOptions
        {
            {
                "p1",
                new()
                {
                    Package = "p1/t1",
                    DataJson = """{ "t": 2 }""",
                    Source = """
                        package p1.t1
                        import future.keywords.if
                        # METADATA
                        # entrypoint: true
                        allow if { input.t == data.p1.t1.t }
                        """,
                }
            },
            {
                "p2",
                new()
                {
                    DataYaml = "t: 3",
                    Source = """
                        package p2
                        import future.keywords.if
                        # METADATA
                        # entrypoint: true
                        allow if { input.t == data.t }
                        """,
                }
            },
            {
                "p3",
                new()
                {
                    DataYaml = "t1: 4",
                    Source = """
                        package p2
                        import future.keywords.if
                        # METADATA
                        # entrypoint: true
                        allow2 if { input.t == data.t1 }
                        """,
                }
            },
        };

        var optionsMonitor = new PolicyOptionsMonitor(policyOptions);

        var compilerOpts = new RegoCompilerOptions
        {
            Debug = true,
        };

        using var compiler = new ConfigurationPolicySource(
            new RegoInteropCompiler(
                new OptionsWrapper<RegoCompilerOptions>(compilerOpts),
                _loggerFactory.CreateLogger<RegoInteropCompiler>()
                ),
            new OptionsWrapper<OpaAuthorizationOptions>(opts),
            optionsMonitor,
            new OpaImportsAbiFactory(),
            _loggerFactory
            );

        await compiler.StartAsync(CancellationToken.None);

        using var evaluator = compiler.CreateEvaluator();
        var result = evaluator.EvaluatePredicate(new { t = data }, entrypoint);

        Assert.True(result.Result);
    }

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

        var policyOptions = new OpaPolicyOptions
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
            new OpaImportsAbiFactory(),
            _loggerFactory
            );

        await compiler.StartAsync(CancellationToken.None);

        for (var i = 0; i < 3; i++)
        {
            var eval = compiler.CreateEvaluator();
            var result = eval.EvaluatePredicate(new UserPolicyInput($"u{i}"));

            _output.WriteLine($"Checking: u{i}");
            Assert.True(result.Result);

            var newOpts = new OpaPolicyOptions { { "p1", new() { Source = Policy(i + 1) } } };
            optionsMonitor.Change(newOpts);

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

    private class PolicyOptionsMonitor : IOptionsMonitor<OpaPolicyOptions>
    {
        private Action<OpaPolicyOptions, string?>? _listener;

        public OpaPolicyOptions CurrentValue { get; private set; }

        public PolicyOptionsMonitor(OpaPolicyOptions opts)
        {
            CurrentValue = opts;
        }

        public OpaPolicyOptions Get(string? name)
        {
            return CurrentValue;
        }

        public void Change(OpaPolicyOptions opts)
        {
            CurrentValue = opts;
            _listener?.Invoke(CurrentValue, null);
        }

        public IDisposable? OnChange(Action<OpaPolicyOptions, string?> listener)
        {
            _listener = listener;
            return null;
        }
    }
}