using JetBrains.Annotations;

using Microsoft.AspNetCore.Http;

namespace OpaDotNet.Extensions.AspNetCore;

[PublicAPI]
public interface IConnectionInput
{
    string? RemoteIpAddress { get; }

    int RemotePort { get; }

    string? LocalIpAddress { get; }

    int LocalPort { get; }
}

[PublicAPI]
public record ClaimPolicyInput(string Type, string Value);

[PublicAPI]
public interface IHttpRequestPolicyInput
{
    string Method { get; }

    string Scheme { get; }

    string Host { get; }

    string? PathBase { get; }

    string? Path { get; }

    string? QueryString { get; }

    IQueryCollection Query { get; }

    string Protocol { get; }
    IConnectionInput Connection { get; }

    IDictionary<string, string?> Headers { get; }

    IEnumerable<ClaimPolicyInput> Claims { get; }
}