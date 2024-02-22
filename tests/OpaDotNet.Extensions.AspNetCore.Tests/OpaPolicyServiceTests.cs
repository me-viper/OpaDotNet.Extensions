﻿using System.Text.Json;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpaDotNet.Compilation.Abstractions;
using OpaDotNet.Compilation.Interop;
using OpaDotNet.Extensions.AspNetCore.Telemetry;
using OpaDotNet.Extensions.AspNetCore.Tests.Common;

using Xunit.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore.Tests;

public class OpaPolicyServiceTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    private readonly ILoggerFactory _loggerFactory = new LoggerFactory(new[] { new XunitLoggerProvider(output) });

    [Fact]
    public async Task Recompilation()
    {
        const int maxEvaluators = 5;

        var opts = new OptionsWrapper<OpaAuthorizationOptions>(
            new()
            {
                PolicyBundlePath = "./Policy",
                MaximumEvaluatorsRetained = maxEvaluators,
                EngineOptions = new()
                {
                    SerializationOptions = new()
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    },
                },
            }
            );

        var compiler = new FileSystemPolicySource(
            new RegoInteropCompiler(),
            opts,
            new OpaImportsAbiFactory(),
            new OptionsWrapper<RegoCompilerOptions>(new()),
            _loggerFactory
            );

        await compiler.StartAsync(CancellationToken.None);

        using var service = new PooledOpaPolicyService(
            compiler,
            opts,
            new OpaEvaluatorPoolProvider(),
            _loggerFactory.CreateLogger<PooledOpaPolicyService>()
            );

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 1000),
            async (i, ct) =>
            {
                if (i % 10 == 0)
                    await compiler.CompileBundle(true, ct);

                var result = service.EvaluatePredicate<object?>(null, "parallel/do");
                Assert.True(result);
            }
            );
    }

    [Fact]
    public async Task PoolSize()
    {
        const int maxEvaluators = 5;

        var opts = new OptionsWrapper<OpaAuthorizationOptions>(
            new()
            {
                PolicyBundlePath = "./Policy",
                MaximumEvaluatorsRetained = maxEvaluators,
                EngineOptions = new()
                {
                    SerializationOptions = new()
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    },
                },
            }
            );

        var compiler = new FileSystemPolicySource(
            new RegoInteropCompiler(),
            opts,
            new OpaImportsAbiFactory(),
            new OptionsWrapper<RegoCompilerOptions>(new()),
            _loggerFactory
            );

        var collector = new MetricCollector<long>(Utility.OpaMeter, "opadotnet_evaluator_instances");
        collector.RecordObservableInstruments();

        await compiler.StartAsync(CancellationToken.None);

        using var service = new PooledOpaPolicyService(
            compiler,
            opts,
            new OpaEvaluatorPoolProvider(),
            _loggerFactory.CreateLogger<PooledOpaPolicyService>()
            );

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 1000),
            (_, _) =>
            {
                var result = service.EvaluatePredicate<object?>(null, "parallel/do");
                Assert.True(result);
                return ValueTask.CompletedTask;
            }
            );

        collector.RecordObservableInstruments();
        var measure = collector.GetMeasurementSnapshot();
        Assert.Equal(maxEvaluators, measure[0].Value);
    }
}