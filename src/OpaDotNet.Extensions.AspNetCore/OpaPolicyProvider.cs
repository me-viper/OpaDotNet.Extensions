using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Options;

namespace OpaDotNet.Extensions.AspNetCore;

public class OpaPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _default;

    public OpaPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _default = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!OpaPolicyRequirement.TryParse(policyName, out var opr))
            return Task.FromResult<AuthorizationPolicy?>(null);

        var policy = new AuthorizationPolicyBuilder();
        policy.AddRequirements(opr);
        return Task.FromResult<AuthorizationPolicy?>(policy.Build());
    }

    [ExcludeFromCodeCoverage]
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _default.GetDefaultPolicyAsync();
    }

    [ExcludeFromCodeCoverage]
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return Task.FromResult<AuthorizationPolicy?>(null);
    }
}