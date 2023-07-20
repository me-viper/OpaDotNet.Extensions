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
    public ConnectionInput Connection
    {
        get => new(_request.HttpContext.Connection);
    }

    [UsedImplicitly]
    public IDictionary<string, string?> Headers { get; }

    [UsedImplicitly]
    public IEnumerable<ClaimInput> Claims { get; }

    [UsedImplicitly]
    internal record ClaimInput(string Type, string Value);
    
    [UsedImplicitly]
    internal class ConnectionInput
    {
        private readonly ConnectionInfo _connection;
        
        public ConnectionInput(ConnectionInfo connection)
        {
            _connection = connection;
        }

        [UsedImplicitly]
        public string? RemoteIpAddress
        {
            get => _connection.RemoteIpAddress?.ToString();
        }

        [UsedImplicitly]
        public int RemotePort
        {
            get => _connection.RemotePort;
        }

        [UsedImplicitly]
        public string? LocalIpAddress
        {
            get => _connection.LocalIpAddress?.ToString();
        }

        [UsedImplicitly]
        public int LocalPort
        {
            get => _connection.LocalPort;
        }

        // public X509Certificate2? ClientCertificate
        // {
        //     get => _connection.ClientCertificate;
        // }
    }

    public HttpRequestPolicyInput(
        HttpRequest request,
        IReadOnlySet<string> includedHeaders,
        IEnumerable<Claim>? claims = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(includedHeaders);

        _request = request;

        Headers = request.Headers
            .Where(p => includedHeaders.Any(pp => Regex.IsMatch(p.Key, pp)))
            .ToDictionary(p => p.Key, p => (string?)p.Value);

        Claims = claims?.Select(p => new ClaimInput(p.Type, p.Value)) ?? Array.Empty<ClaimInput>();
    }
}