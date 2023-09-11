using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

using OpaDotNet.Compilation.Abstractions;
using OpaDotNet.Compilation.Interop;
using OpaDotNet.Extensions.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddYamlFile("appsettings.yaml", false, true);

// Setup Interop compiler.
builder.Services.Configure<RegoCompilerOptions>(builder.Configuration.GetSection("Compiler"));
builder.Services.AddSingleton<IRegoCompiler, RegoInteropCompiler>();

// Configuration section containing policies.
builder.Services.Configure<PolicyOptions>(builder.Configuration.GetSection("policies"));

builder.Services.AddOpaAuthorization(
    cfg =>
    {
        // Get policies from the configuration.
        cfg.AddPolicySource<ConfigurationPolicySource>();
        cfg.AddConfiguration(builder.Configuration.GetSection("Opa").Bind);
        cfg.AddJsonOptions(p => p.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
    }
    );

// In real scenarios here will be more sophisticated authentication.
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, NopAuthenticationSchemeHandler>(
        NopAuthenticationSchemeHandler.AuthenticationSchemeName,
        null
        );

builder.Services.AddAuthorization();

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Ensure build path exists.
var buildPath = app.Services.GetService<IOptions<RegoCompilerOptions>>()?.Value.OutputPath;

if (!string.IsNullOrWhiteSpace(buildPath))
{
    if (!Directory.Exists(buildPath))
        Directory.CreateDirectory(buildPath);
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();

internal class NopAuthenticationSchemeHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationSchemeName = "Nop";

    public NopAuthenticationSchemeHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = new ClaimsPrincipal();
        var ticket = new AuthenticationTicket(principal, AuthenticationSchemeName);
        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}