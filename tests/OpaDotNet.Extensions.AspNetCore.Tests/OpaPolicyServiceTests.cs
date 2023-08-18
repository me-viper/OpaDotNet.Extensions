using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpaDotNet.Compilation.Interop;
using OpaDotNet.Extensions.AspNetCore.Tests.Common;

using Xunit.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore.Tests;

public class OpaPolicyServiceTests
{
    private readonly ITestOutputHelper _output;

    private readonly ILoggerFactory _loggerFactory;

    public OpaPolicyServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = new LoggerFactory(new[] { new XunitLoggerProvider(output) });
    }

    [Fact]
    public async Task Recompilation()
    {
        var opts = new OptionsWrapper<OpaAuthorizationOptions>(
            new()
            {
                PolicyBundlePath = "./Policy",
                MaximumEvaluatorsRetained = 5,
                EngineOptions = new()
                {
                    SerializationOptions = new()
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    },
                },
            }
            );

        var compiler = new OpaPolicyCompiler(
            new RegoInteropCompiler(),
            opts,
            _loggerFactory
            );

        await compiler.CompileBundle(false, CancellationToken.None);

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
}