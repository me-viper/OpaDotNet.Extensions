using System.Diagnostics.CodeAnalysis;

using JetBrains.Annotations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;

using OpaDotNet.Wasm.Compilation;

namespace OpaDotNet.Extensions.AspNetCore;

[PublicAPI]
[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpaAuthorization(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "OpaAuthorizationHandler")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        services.AddOptions();
        services.Configure<OpaAuthorizationOptions>(configuration.GetRequiredSection(sectionName));

        services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.TryAddSingleton<IAuthorizationPolicyProvider, OpaPolicyProvider>();
        services.TryAddSingleton<IAuthorizationHandler, OpaPolicyHandler>();
        services.TryAddSingleton<IRegoCompiler, RegoCliCompiler>();
        services.TryAddSingleton<IOpaPolicyService, PooledOpaPolicyService>();

        return services;
    }

    public static IServiceCollection AddOpaAuthorization(
        this IServiceCollection services,
        Action<OpaAuthorizationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions();
        services.Configure(configure);

        services.TryAddSingleton<OpaEvaluatorPoolProvider>();
        services.TryAddSingleton<IAuthorizationPolicyProvider, OpaPolicyProvider>();
        services.TryAddSingleton<IAuthorizationHandler, OpaPolicyHandler>();
        services.TryAddSingleton<IRegoCompiler, RegoCliCompiler>();
        services.TryAddSingleton<IOpaPolicyService, PooledOpaPolicyService>();

        return services;
    }
}