using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using JetBrains.Annotations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

[PublicAPI]
[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IOpaAuthorizationBuilder AddJsonOptions(
        this IOpaAuthorizationBuilder builder,
        Action<JsonSerializerOptions> jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(jsonOptions);

        builder.Services
            .AddOptions<OpaAuthorizationOptions>()
            .PostConfigure(
                p =>
                {
                    p.EngineOptions ??= WasmPolicyEngineOptions.Default;
                    jsonOptions.Invoke(p.EngineOptions.SerializationOptions);
                }
                );

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

    public static IOpaAuthorizationBuilder AddPolicySource<T>(
        this IOpaAuthorizationBuilder builder,
        Func<IServiceProvider, T>? buildCompiler = null) where T : class, IOpaPolicySource
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (buildCompiler == null)
            builder.Services.TryAddSingleton<IOpaPolicySource, T>();
        else
            builder.Services.TryAddSingleton<IOpaPolicySource>(buildCompiler);

        builder.Services.AddHostedService(p => p.GetRequiredService<IOpaPolicySource>());

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
        services.TryAddSingleton<IOpaPolicyService, PooledOpaPolicyService>();

        return services;
    }
}