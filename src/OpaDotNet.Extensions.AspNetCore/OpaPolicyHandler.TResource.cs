using JetBrains.Annotations;

namespace OpaDotNet.Extensions.AspNetCore;

[PublicAPI]
public class OpaPolicyHandler<TResource> : AuthorizationHandler<OpaPolicyRequirement, TResource>
{
    protected IOpaPolicyService Service { get; }

    protected ILogger Logger { get; }

    public OpaPolicyHandler(IOpaPolicyService service, ILogger<OpaPolicyHandler<TResource>> logger)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(logger);

        Service = service;
        Logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OpaPolicyRequirement requirement,
        TResource resource)
    {
        using var scope = Logger.BeginScope(new { requirement.Entrypoint });

        try
        {
            Logger.LogDebug("Evaluating policy");
            var result = Service.EvaluatePredicate(resource, requirement.Entrypoint);

            if (!result)
                Logger.LogDebug("Failed");
            else
            {
                Logger.LogDebug("Success");
                context.Succeed(requirement);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Authorization policy failed");
        }

        return Task.CompletedTask;
    }
}