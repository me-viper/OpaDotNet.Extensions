using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace OpaDotNet.Extensions.AspNetCore;

public class OpaPolicyHandler : AuthorizationHandler<OpaPolicyRequirement>
{
    private readonly IOpaPolicyService _service;

    private readonly ILogger _logger;

    private readonly IOptions<OpaPolicyHandlerOptions> _options;

    public OpaPolicyHandler(
        IOpaPolicyService service,
        IOptions<OpaPolicyHandlerOptions> options,
        ILogger<OpaPolicyHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _service = service;
        _options = options;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OpaPolicyRequirement requirement)
    {
        using var scope = _logger.BeginScope(new { requirement.Entrypoint });

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
                _options.Value.AllowedHeaders,
                _options.Value.IncludeClaimsInHttpRequest ? context.User.Claims : null
                );

            _logger.LogDebug("Evaluating policy");
            var result = _service.EvaluatePredicate(input, requirement.Entrypoint);

            if (!result)
                _logger.LogDebug("Authorization policy denied");
            else
            {
                _logger.LogDebug("Authorization policy succeeded");
                context.Succeed(requirement);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authorization policy failed");
        }
        
        return Task.CompletedTask;
    }
}