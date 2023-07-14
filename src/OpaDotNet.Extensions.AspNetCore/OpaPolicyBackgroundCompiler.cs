using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using OpaDotNet.Wasm;
using OpaDotNet.Wasm.Compilation;

namespace OpaDotNet.Extensions.AspNetCore;

public class OpaPolicyBackgroundCompiler : IHostedService, IOpaPolicyBackgroundCompiler
{
    private readonly IRegoCompiler _compiler;
    
    private readonly ILogger _logger;

    private readonly ILoggerFactory _loggerFactory;
    
    private readonly IOptions<OpaPolicyHandlerOptions> _options;
    
    private CancellationTokenSource _changeTokenSource = new();
    
    private CancellationChangeToken _changeToken;
    
    public OpaEvaluatorFactoryBase Factory { get; private set; }

    public OpaPolicyBackgroundCompiler(
        IRegoCompiler compiler, 
        IOptions<OpaPolicyHandlerOptions> options, 
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(options);
        
        _compiler = compiler;
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<OpaPolicyBackgroundCompiler>();
        _changeToken = new(_changeTokenSource.Token);
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
    
    private async Task CompileBundle(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Compiling");

        try
        {
            await using var policy = await _compiler.CompileBundle(
                _options.Value.PolicyBundlePath,
                cancellationToken: cancellationToken,
                entrypoints: _options.Value.Entrypoints
                );

            Factory = new OpaBundleEvaluatorFactory(
                policy, 
                loggerFactory: _loggerFactory, 
                options: _options.Value.EngineOptions
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bundle compilation failed");
            throw;
        }

        _logger.LogDebug("Compilation succeeded");
    }

    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        await CompileBundle(cancellationToken);
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}