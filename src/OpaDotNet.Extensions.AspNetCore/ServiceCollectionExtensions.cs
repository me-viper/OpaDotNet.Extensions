using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using JetBrains.Annotations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OpaDotNet.Compilation.Abstractions;
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

    public static IOpaAuthorizationBuilder AddConfiguration(
        this IOpaAuthorizationBuilder builder,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.Services.Configure<OpaAuthorizationOptions>(configuration);

        return builder;
    }

    public static IOpaAuthorizationBuilder AddImportsAbi<T>(this IOpaAuthorizationBuilder builder)
        where T : DefaultOpaImportsAbi
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddTransient<IOpaImportsAbi, T>();
        builder.Services.TryAddTransient<IOpaImportsAbiFactory>(
            p => new OpaImportsAbiFactory(p.GetRequiredService<IOpaImportsAbi>)
            );

        return builder;
    }

    public static IOpaAuthorizationBuilder AddCompiler<TCompiler, TOptions>(
        this IOpaAuthorizationBuilder builder,
        Action<TOptions> configuration,
        Func<IServiceProvider, TCompiler>? buildCompiler = null)
        where TCompiler : class, IRegoCompiler
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure(configuration);

        if (buildCompiler == null)
            builder.AddCompiler<TCompiler>();
        else
            builder.AddCompiler(buildCompiler);

        return builder;
    }

    public static IOpaAuthorizationBuilder AddCompiler<T>(
        this IOpaAuthorizationBuilder builder)
        where T : class, IRegoCompiler
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddSingleton<IRegoCompiler, T>();
        return builder;
    }

    public static IOpaAuthorizationBuilder AddCompiler<T>(
        this IOpaAuthorizationBuilder builder,
        Func<IServiceProvider, T> buildCompiler)
        where T : class, IRegoCompiler
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddSingleton<IRegoCompiler>(buildCompiler);
        return builder;
    }

    public static IOpaAuthorizationBuilder AddConfigurationPolicySource(
        this IOpaAuthorizationBuilder builder,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.Services.Configure<OpaPolicyOptions>(configuration);
        builder.AddPolicySource<ConfigurationPolicySource>();

        return builder;
    }

    public static IOpaAuthorizationBuilder AddConfigurationPolicySource(
        this IOpaAuthorizationBuilder builder,
        Action<OpaPolicyOptions> configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.Services.Configure(configuration);
        builder.AddPolicySource<ConfigurationPolicySource>();

        return builder;
    }

    public static IOpaAuthorizationBuilder AddFileSystemPolicySource(this IOpaAuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddPolicySource<FileSystemPolicySource>();

        return builder;
    }

    public static IOpaAuthorizationBuilder AddPolicySource<T>(
        this IOpaAuthorizationBuilder builder,
        Func<IServiceProvider, T>? buildPolicySource = null) where T : class, IOpaPolicySource
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (buildPolicySource == null)
            builder.Services.TryAddSingleton<IOpaPolicySource, T>();
        else
            builder.Services.TryAddSingleton<IOpaPolicySource>(buildPolicySource);

        builder.Services.AddHostedService(p => p.GetRequiredService<IOpaPolicySource>());

        return builder;
    }

    public static IServiceCollection AddOpaAuthorization(
        this IServiceCollection services,
        Action<IOpaAuthorizationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOpaAuthorization();
        configure(new OpaAuthorizationBuilder(services));

        services.TryAddTransient<IOpaImportsAbiFactory>(_ => new OpaImportsAbiFactory());

        return services;
    }

    private static IServiceCollection AddOpaAuthorization(this IServiceCollection services)
    {
        services.AddOptions();
        services.TryAddSingleton<OpaEvaluatorPoolProvider>();
        services.TryAddSingleton<IAuthorizationPolicyProvider, OpaPolicyProvider>();
        services.TryAddSingleton<IAuthorizationHandler, OpaPolicyHandler>();
        services.TryAddSingleton<IOpaPolicyService, PooledOpaPolicyService>();

        return services;
    }
}