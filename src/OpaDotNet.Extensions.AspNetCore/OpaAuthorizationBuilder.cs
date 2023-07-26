using Microsoft.Extensions.DependencyInjection;

namespace OpaDotNet.Extensions.AspNetCore;

internal sealed class OpaAuthorizationBuilder : IOpaAuthorizationBuilder
{
    public IServiceCollection Services { get; }

    public OpaAuthorizationBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }
}