namespace OpaDotNet.Extensions.AspNetCore;

public class OpaPolicyHandler<TResource> : AuthorizationHandler<OpaPolicyRequirement, TResource>
{
    private readonly IOpaPolicyService _service;

    private readonly ILogger _logger;

    public OpaPolicyHandler(IOpaPolicyService service, ILogger<OpaPolicyHandler<TResource>> logger)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(logger);

        _service = service;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OpaPolicyRequirement requirement,
        TResource resource)
    {
        using var scope = _logger.BeginScope(new { requirement.Entrypoint });

        try
        {
            _logger.LogDebug("Evaluating policy");
            var result = _service.EvaluatePredicate(resource, requirement.Entrypoint);

            if (!result)
                _logger.LogDebug("Failed");
            else
            {
                _logger.LogDebug("Success");
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