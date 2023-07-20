using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

using OpaDotNet.Extensions.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Register core services.
builder.Services.AddOpaAuthorization(
    p =>
    {
        // Allow to pass all headers as policy query input.
        p.AllowedHeaders.Add(".*");
        
        // Path where look for rego policies.
        p.PolicyBundlePath = "./Policy";
        p.EngineOptions = new()
        {
            SerializationOptions = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
        };
    }
    );

// Register compiler.
builder.Services.AddSingleton<OpaPolicyBackgroundCompiler>();
builder.Services.AddHostedService(p => p.GetRequiredService<OpaPolicyBackgroundCompiler>());
builder.Services.AddSingleton<IOpaPolicyCompiler>(p => p.GetRequiredService<OpaPolicyBackgroundCompiler>());

// In real scenarios here will be real authentication. 
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, NopAuthenticationSchemeHandler>(
        NopAuthenticationSchemeHandler.AuthenticationSchemeName, 
        null
        );

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Will evaluate example/allow rule and return 200. 
app.MapGet("/allow", [OpaPolicyAuthorize("example", "allow")] () => "Hi!");

// Will evaluate example/deny rule and return 403.
app.MapGet("/deny", [OpaPolicyAuthorize("example", "deny")] () => "Should not be here!");

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