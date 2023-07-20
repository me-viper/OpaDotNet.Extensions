using System.Security.Claims;
using System.Text.RegularExpressions;

using JetBrains.Annotations;

using Microsoft.AspNetCore.Http;

namespace OpaDotNet.Extensions.AspNetCore;

internal class HttpRequestPolicyInput
{
    private readonly HttpRequest _request;

    [UsedImplicitly]
    public string Method
    {
        get => _request.Method;
    }

    [UsedImplicitly]
    public string Scheme
    {
        get => _request.Scheme;
    }

    [UsedImplicitly]
    public string Host
    {
        get => _request.Host.Value;
    }

    [UsedImplicitly]
    public string? PathBase
    {
        get => _request.PathBase.Value;
    }

    [UsedImplicitly]
    public string? Path
    {
        get => _request.Path.Value;
    }

    [UsedImplicitly]
    public string? QueryString
    {
        get => _request.QueryString.Value;
    }

    [UsedImplicitly]
    public IQueryCollection Query
    {
        get => _request.Query;
    }

    [UsedImplicitly]
    public string Protocol
    {
        get => _request.Protocol;
    }

    [UsedImplicitly]
    public ConnectionInfo Connection
    {
        get => _request.HttpContext.Connection;
    }

    [UsedImplicitly]
    public IDictionary<string, string?> Headers { get; }

    [UsedImplicitly]
    public IEnumerable<ClaimInput> Claims { get; }

    [UsedImplicitly]
    internal record ClaimInput(string Type, string Value);

    public HttpRequestPolicyInput(
        HttpRequest request,
        IReadOnlySet<string> includedHeaders,
        IEnumerable<Claim>? claims = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(includedHeaders);

        _request = request;

        //var allowAll = includedHeaders.Any(p => string.Equals("*", p, StringComparison.Ordinal));

        Headers = request.Headers
            .Where(p => includedHeaders.Any(pp => Regex.IsMatch(p.Key, pp)))
            .ToDictionary(p => p.Key, p => (string?)p.Value);

        Claims = claims?.Select(p => new ClaimInput(p.Type, p.Value)) ?? Array.Empty<ClaimInput>();
    }
}