using System.Diagnostics.CodeAnalysis;

namespace OpaDotNet.Extensions.AspNetCore;

public record OpaPolicyRequirement : IAuthorizationRequirement
{
    public OpaPolicyRequirement(string entrypoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);

        Entrypoint = entrypoint;
    }

    /// <summary>
    /// OPA policy entrypoint.
    /// </summary>
    public string Entrypoint { get; }

    internal static bool TryParse(string policyName, [MaybeNullWhen(false)] out OpaPolicyRequirement result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(policyName))
            return false;

        policyName = policyName.TrimEnd('/');

        var prefixIndex = policyName.IndexOf(OpaPolicyAuthorizeAttribute.PolicyPrefix, StringComparison.Ordinal);

        if (prefixIndex < 0)
            return false;

        prefixIndex += OpaPolicyAuthorizeAttribute.PolicyPrefix.Length + 1;

        if (prefixIndex > policyName.Length)
            return false;

        var ep = policyName[prefixIndex..];

        if (string.IsNullOrWhiteSpace(ep))
            return false;

        result = new OpaPolicyRequirement(ep);

        return true;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        return $"OPA: {Entrypoint}";
    }
}