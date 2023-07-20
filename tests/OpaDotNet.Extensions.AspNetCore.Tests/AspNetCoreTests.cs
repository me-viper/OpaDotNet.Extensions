using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using JetBrains.Annotations;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using OpaDotNet.Extensions.AspNetCore.Tests.Common;

using Xunit.Abstractions;

namespace OpaDotNet.Extensions.AspNetCore.Tests;

public class AspNetCoreTests
{
    private readonly ITestOutputHelper _output;

    public AspNetCoreTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HttpRequestInput()
    {
        var server = CreateServer(
            _output,
            handler: async (context, _) =>
            {
                var request = context.Request;
                var azs = context.RequestServices.GetRequiredService<IAuthorizationService>();
                var result = await azs.AuthorizeAsync(context.User, request, "Opa/az/rq_path");

                if (!result.Succeeded)
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            }
            );

        var request = new HttpRequestMessage(HttpMethod.Get, $"{server.BaseAddress}az/request");

        var transaction = new Transaction
        {
            Request = request,
            Response = await server.CreateClient().SendAsync(request),
        };

        transaction.ResponseText = await transaction.Response.Content.ReadAsStringAsync();

        Assert.NotNull(transaction.Response);
        Assert.Equal(HttpStatusCode.OK, transaction.Response.StatusCode);
    }

    [Theory]
    [InlineData("u1", HttpStatusCode.OK)]
    [InlineData("wrong", HttpStatusCode.Forbidden)]
    [Trait("Category", "Integration")]
    public async Task Simple(string user, HttpStatusCode expected)
    {
        var server = CreateServer(
            _output,
            handler: async (context, _) =>
            {
                var azs = context.RequestServices.GetRequiredService<IAuthorizationService>();

                var result = await azs.AuthorizeAsync(context.User, new UserPolicyInput(user), "Opa/az/user");

                if (!result.Succeeded)
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            }
            );

        var request = new HttpRequestMessage(HttpMethod.Get, $"{server.BaseAddress}");

        var transaction = new Transaction
        {
            Request = request,
            Response = await server.CreateClient().SendAsync(request),
        };

        transaction.ResponseText = await transaction.Response.Content.ReadAsStringAsync();

        Assert.NotNull(transaction.Response);
        Assert.Equal(expected, transaction.Response.StatusCode);
    }

    [Theory]
    [InlineData("u1", HttpStatusCode.OK)]
    [InlineData("wrong", HttpStatusCode.Forbidden)]
    [Trait("Category", "Integration")]
    public async Task ParallelSimple(string user, HttpStatusCode expected)
    {
        var server = CreateServer(
            _output,
            handler: async (context, _) =>
            {
                var azs = context.RequestServices.GetRequiredService<IAuthorizationService>();

                var result = await azs.AuthorizeAsync(context.User, new UserPolicyInput(user), "Opa/az/user");

                if (!result.Succeeded)
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            }
            );

        async Task DoSimple()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{server.BaseAddress}");

            var transaction = new Transaction
            {
                Request = request,
                Response = await server.CreateClient().SendAsync(request),
            };

            transaction.ResponseText = await transaction.Response.Content.ReadAsStringAsync();

            Assert.NotNull(transaction.Response);
            Assert.Equal(expected, transaction.Response.StatusCode);
        }

        await Parallel.ForEachAsync(Enumerable.Range(0, 10_000), async (_, _) => await DoSimple());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Jwt()
    {
        var server = CreateServer(
            _output,
            handler: async (context, _) =>
            {
                var request = context.Request;
                var azs = context.RequestServices.GetRequiredService<IAuthorizationService>();

                var result = await azs.AuthorizeAsync(context.User, request, "Opa/az/jwt");

                if (!result.Succeeded)
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            }
            );

        var request = new HttpRequestMessage(HttpMethod.Get, server.BaseAddress);

        var handler = new JwtSecurityTokenHandler();

        var sd = new SecurityTokenDescriptor
        {
            Issuer = "opa.tests",
            Claims = new Dictionary<string, object>
            {
                { "user", "jwtUser" },
                { "role", "jwtTester" },
            },
        };

        var token = handler.CreateEncodedJwt(sd);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var transaction = new Transaction
        {
            Request = request,
            Response = await server.CreateClient().SendAsync(request),
        };

        transaction.ResponseText = await transaction.Response.Content.ReadAsStringAsync();

        Assert.NotNull(transaction.Response);
        Assert.Equal(HttpStatusCode.OK, transaction.Response.StatusCode);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("az/attr")]
    [InlineData("az/svc")]
    [InlineData("az/attr", "xxx", HttpStatusCode.Forbidden)]
    [InlineData("az/svc", "xxx", HttpStatusCode.Forbidden)]
    public async Task RouteAuthorization(string path, string? user = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var server = CreateServer(_output);
        var request = new HttpRequestMessage(HttpMethod.Get, $"{server.BaseAddress}{path}");
        var handler = new JwtSecurityTokenHandler();

        var sd = new SecurityTokenDescriptor
        {
            Issuer = "opa.tests",
            Claims = new Dictionary<string, object>
            {
                { "user", user ?? "attrUser" },
                { "role", "attrTester" },
            },
        };

        var token = handler.CreateEncodedJwt(sd);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var transaction = new Transaction
        {
            Request = request,
            Response = await server.CreateClient().SendAsync(request),
        };

        transaction.ResponseText = await transaction.Response.Content.ReadAsStringAsync();

        Assert.NotNull(transaction.Response);
        Assert.Equal(expected, transaction.Response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Claims()
    {
        var server = CreateServer(
            _output,
            handler: async (context, _) =>
            {
                var request = context.Request;
                var azs = context.RequestServices.GetRequiredService<IAuthorizationService>();

                var claims = new List<Claim>
                {
                    new("claimX", "valueY"),
                };

                var identity = new ClaimsIdentity(claims);
                context.User.AddIdentity(identity);

                var result = await azs.AuthorizeAsync(context.User, request, "Opa/az/claims");

                if (!result.Succeeded)
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            }
            );

        var request = new HttpRequestMessage(HttpMethod.Get, server.BaseAddress);

        var transaction = new Transaction
        {
            Request = request,
            Response = await server.CreateClient().SendAsync(request),
        };

        transaction.ResponseText = await transaction.Response.Content.ReadAsStringAsync();

        Assert.NotNull(transaction.Response);
        Assert.Equal(HttpStatusCode.OK, transaction.Response.StatusCode);
    }

    private record UserPolicyInput([UsedImplicitly] string User);

    private static TestServer CreateServer(
        ITestOutputHelper output,
        Func<HttpContext, Func<Task>, Task>? handler = null,
        Uri? baseAddress = null,
        Func<IServiceCollection, IServiceCollection>? configureServices = null)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(
                builder =>
                {
                    builder.AddLogging(p => p.AddXunit(output).AddFilter(pp => pp > LogLevel.Trace));

                    builder.AddOpaAuthorization(
                        p =>
                        {
                            p.PolicyBundlePath = "./Policy";
                            p.AllowedHeaders.Add(".*");
                            p.IncludeClaimsInHttpRequest = true;
                            p.EngineOptions = new()
                            {
                                SerializationOptions = new()
                                {
                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                },
                            };
                        }
                        );

                    builder.AddSingleton<OpaPolicyBackgroundCompiler>();
                    builder.AddHostedService(p => p.GetRequiredService<OpaPolicyBackgroundCompiler>());
                    builder.AddSingleton<IOpaPolicyBackgroundCompiler>(p => p.GetRequiredService<OpaPolicyBackgroundCompiler>());

                    builder.AddSingleton<IAuthorizationHandler, OpaPolicyHandler<UserPolicyInput>>();

                    if (handler == null)
                        builder.AddRouting();

                    builder.AddAuthentication()
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationSchemeHandler>("Test", null);
                    builder.AddAuthorization();

                    configureServices?.Invoke(builder);
                }
                )
            .Configure(
                app =>
                {
                    if (handler != null)
                    {
                        app.UseAuthentication();
                        app.UseAuthorization();

                        app.Use(handler);
                    }
                    else
                    {
                        app.UseRouting();

                        app.UseAuthentication();
                        app.UseAuthorization();

                        app.UseEndpoints(
                            p =>
                            {
                                p.MapGet("/az/attr", [OpaPolicyAuthorize("az", "attr")](context) => Task.FromResult(Results.Ok()));
                                p.MapGet(
                                    "/az/svc",
                                    async ([FromServices] IAuthorizationService azs, ClaimsPrincipal user, HttpContext context) =>
                                    {
                                        var result = await azs.AuthorizeAsync(user, context, "Opa/az/attr");
                                        return result.Succeeded ? Results.Ok() : Results.Forbid();
                                    }
                                    );
                            }
                            );
                    }
                }
                );

        var server = new TestServer(builder);

        if (baseAddress != null)
            server.BaseAddress = baseAddress;

        return server;
    }
}

internal class TestAuthenticationSchemeHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationSchemeHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = new ClaimsPrincipal();
        var ticket = new AuthenticationTicket(principal, "Test");
        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}