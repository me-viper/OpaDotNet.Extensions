using JetBrains.Annotations;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace OpaDotNet.Extensions.AspNetCore;

[PublicAPI]
public class OpaPolicyHandler : AuthorizationHandler<OpaPolicyRequirement>
{
    protected IOpaPolicyService Service { get; }

    protected ILogger Logger { get; }

    protected IOptions<OpaAuthorizationOptions> Options { get; }

    public OpaPolicyHandler(
        IOpaPolicyService service,
        IOptions<OpaAuthorizationOptions> options,
        ILogger<OpaPolicyHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        Service = service;
        Options = options;
        Logger = logger;
    }

    protected virtual Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OpaPolicyRequirement requirement,
        IHttpRequestPolicyInput resource)
    {
        Logger.LogDebug("Evaluating policy");
        var result = Service.EvaluatePredicate(resource, requirement.Entrypoint);

        if (!result)
            Logger.LogDebug("Authorization policy denied");
        else
        {
            Logger.LogDebug("Authorization policy succeeded");
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OpaPolicyRequirement requirement)
    {
        using var scope = Logger.BeginScope(new { requirement.Entrypoint });

        try
        {
            HttpRequest request;

            if (context.Resource is HttpContext httpContext)
                request = httpContext.Request;
            else
            {
                if (context.Resource is not HttpRequest rq)
                    return Task.CompletedTask;

                request = rq;
            }

            var input = new HttpRequestPolicyInput(
                request,
                Options.Value.AllowedHeaders,
                Options.Value.IncludeClaimsInHttpRequest ? context.User.Claims : null
                );

            return HandleRequirementAsync(context, requirement, input);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Authorization policy failed");
        }

        return Task.CompletedTask;
    }
}