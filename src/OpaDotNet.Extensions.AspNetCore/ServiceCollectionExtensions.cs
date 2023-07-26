using System.Diagnostics.CodeAnalysis;

using JetBrains.Annotations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OpaDotNet.Wasm.Compilation;

namespace OpaDotNet.Extensions.AspNetCore;

public interface IOpaAuthorizationBuilder
{
    IServiceCollection Services { get; }
}

internal sealed class OpaAuthorizationBuilder : IOpaAuthorizationBuilder
{
    public IServiceCollection Services { get; }

    public OpaAuthorizationBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }
}

[PublicAPI]
[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IOpaAuthorizationBuilder AddConfiguration(
        this IOpaAuthorizationBuilder builder,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.Services.Configure<OpaAuthorizationOptions>(configuration);

        return builder;
    }

    public static IOpaAuthorizationBuilder AddConfiguration(
        this IOpaAuthorizationBuilder builder,
        Action<OpaAuthorizationOptions> configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.Services.Configure(configuration);

        return builder;
    }

    public static IOpaAuthorizationBuilder AddEvaluatorFactory<T>(
        this IOpaAuthorizationBuilder builder,
        Func<IServiceProvider, T> evaluatorFactoryProvider) where T : class, IOpaEvaluatorFactoryProvider
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<IOpaEvaluatorFactoryProvider>(evaluatorFactoryProvider);

        return builder;
    }

    public static IOpaAuthorizationBuilder AddDefaultCompiler(this IOpaAuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddCompiler<OpaPolicyCompiler>();
    }

    public static IOpaAuthorizationBuilder AddCompiler<T>(
        this IOpaAuthorizationBuilder builder,
        Func<IServiceProvider, T> compiler) where T : class, IOpaPolicyCompiler
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<IOpaPolicyCompiler>(compiler);
        builder.Services.TryAddSingleton<IOpaEvaluatorFactoryProvider>(p => p.GetRequiredService<IOpaPolicyCompiler>());

        return builder;
    }

    public static IOpaAuthorizationBuilder AddCompiler<T>(this IOpaAuthorizationBuilder builder)
        where T : class, IOpaPolicyCompiler
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<IOpaPolicyCompiler, T>();
        builder.Services.TryAddSingleton<IOpaEvaluatorFactoryProvider>(p => p.GetRequiredService<IOpaPolicyCompiler>());

        return builder;
    }

    public static IServiceCollection AddOpaAuthorization(
        this IServiceCollection services,
        Action<IOpaAuthorizationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions();
        services.AddOpaAuthorization();

        configure(new OpaAuthorizationBuilder(services));

        return services;
    }

    private static IServiceCollection AddOpaAuthorization(this IServiceCollection services)
    {
        services.TryAddSingleton<OpaEvaluatorPoolProvider>();
        services.TryAddSingleton<IAuthorizationPolicyProvider, OpaPolicyProvider>();
        services.TryAddSingleton<IAuthorizationHandler, OpaPolicyHandler>();
        services.TryAddSingleton<IRegoCompiler, RegoCliCompiler>();
        services.TryAddSingleton<IOpaPolicyService, PooledOpaPolicyService>();

        return services;
    }
}