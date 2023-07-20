using Microsoft.Extensions.Primitives;

using OpaDotNet.Wasm;

using Xunit.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore.Tests;

public class CompilationTests
{
    private readonly ITestOutputHelper _output;

    public CompilationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Recompile()
    {
        var compiler = new CompilerStub();

        var svc = new ServiceStub(compiler, _output);
        compiler.Recompile();
        compiler.Recompile();
        compiler.Recompile();
        compiler.Recompile();
    }

    private class ServiceStub
    {
        private int _i = 0;

        public ServiceStub(IOpaPolicyBackgroundCompiler compiler, ITestOutputHelper output)
        {
            ChangeToken.OnChange(compiler.OnRecompiled, () => output.WriteLine($"Recompiling {_i++}"));
        }
    }

    private class CompilerStub : IOpaPolicyBackgroundCompiler
    {
        private CancellationTokenSource _changeTokenSource = new();

        private CancellationChangeToken _changeToken;

        public OpaEvaluatorFactory Factory { get; } = default!;

        public CompilerStub()
        {
            _changeToken = new(_changeTokenSource.Token);
        }

        public void Recompile()
        {
            _changeTokenSource.Cancel();
        }

        public IChangeToken OnRecompiled()
        {
            if (_changeTokenSource.IsCancellationRequested)
            {
                _changeTokenSource = new();
                _changeToken = new(_changeTokenSource.Token);
            }

            return _changeToken;
        }
    }
}