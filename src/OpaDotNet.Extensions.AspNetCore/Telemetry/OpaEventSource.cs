using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;

namespace OpaDotNet.Extensions.AspNetCore.Telemetry;

internal sealed class OpaEventSource : EventSource
{
    public static readonly OpaEventSource Log = new();

    private static readonly Counter<long> PolicyAllowedCounter = Utility.OpaMeter.CreateCounter<long>(
        "opadotnet_policies_allowed",
        description: "Policy decisions allowed"
        );

    private static readonly Counter<long> PolicyDeniedCounter = Utility.OpaMeter.CreateCounter<long>(
        "opadotnet_policies_denied",
        description: "Policy decisions denied"
        );

    private static readonly Counter<long> PolicyFailedCounter = Utility.OpaMeter.CreateCounter<long>(
        "opadotnet_policies_failed",
        description: "Policy decisions failed"
        );

    private static readonly Counter<long> BundleCompilationSucceededCounter = Utility.OpaMeter.CreateCounter<long>(
        "opadotnet_compilations_succeeded",
        description: "Bundle compilations succeeded"
        );

    private static readonly Counter<long> BundleCompilationFailedCounter = Utility.OpaMeter.CreateCounter<long>(
        "opadotnet_compilations_failed",
        description: "Bundle compilations failed"
        );

    private static readonly UpDownCounter<long> EvaluatorInstancesCounter = Utility.OpaMeter.CreateUpDownCounter<long>(
        "opadotnet_evaluator_instances",
        description: "Active evaluator instances"
        );

    [Event(Utility.PolicyAllowedEventId, Level = EventLevel.Informational)]
    public void PolicyAllowed(string entrypoint)
    {
        PolicyAllowedCounter.Add(1, new KeyValuePair<string, object?>("policy", entrypoint));

        if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            WriteEvent(Utility.PolicyAllowedEventId, entrypoint);
    }

    [Event(Utility.PolicyDeniedEventId, Level = EventLevel.Informational)]
    public void PolicyDenied(string entrypoint)
    {
        PolicyDeniedCounter.Add(1, new KeyValuePair<string, object?>("policy", entrypoint));

        if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            WriteEvent(Utility.PolicyDeniedEventId, entrypoint);
    }

    [Event(Utility.PolicyFailedEventId, Level = EventLevel.Error)]
    public void PolicyFailed(string entrypoint)
    {
        PolicyFailedCounter.Add(1, new KeyValuePair<string, object?>("policy", entrypoint));

        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            WriteEvent(Utility.PolicyFailedEventId, entrypoint);
    }

    [Event(Utility.BundleCompilationSucceeded, Level = EventLevel.Informational)]
    public void BundleCompilationSucceeded()
    {
        BundleCompilationSucceededCounter.Add(1);

        if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            WriteEvent(Utility.BundleCompilationSucceeded);
    }

    [Event(Utility.BundleCompilationFailed, Level = EventLevel.Error)]
    public void BundleCompilationFailed()
    {
        BundleCompilationFailedCounter.Add(1);

        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            WriteEvent(Utility.BundleCompilationFailed);
    }

    [Event(Utility.EvaluatorCreated, Level = EventLevel.Informational)]
    public void EvaluatorCreated()
    {
        EvaluatorInstancesCounter.Add(1);

        if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            WriteEvent(Utility.EvaluatorCreated);
    }

    [Event(Utility.EvaluatorReleased, Level = EventLevel.Informational)]
    public void EvaluatorReleased()
    {
        EvaluatorInstancesCounter.Add(-1);

        if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            WriteEvent(Utility.EvaluatorReleased);
    }
}