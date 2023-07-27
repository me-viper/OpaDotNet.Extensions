using System.Diagnostics.CodeAnalysis;

using JetBrains.Annotations;

using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

[ExcludeFromCodeCoverage]
public class OpaAuthorizationOptions
{
    /// <summary>
    /// Headers that can be used for policy evaluation. Supports regex.
    /// </summary>
    [UsedImplicitly]
    public HashSet<string> AllowedHeaders { get; set; } = new();

    /// <summary>
    /// If <c>true</c> will append user claims to the policy evaluation query.
    /// </summary>
    [UsedImplicitly]
    public bool IncludeClaimsInHttpRequest { get; set; }

    /// <summary>
    /// Directory containing policy bundle source code.
    /// </summary>
    [UsedImplicitly]
    public string? PolicyBundlePath { get; set; }

    /// <summary>
    /// List of permitted policy entrypoints.
    /// </summary>
    [UsedImplicitly]
    public HashSet<string>? Entrypoints { get; set; }

    [UsedImplicitly]
    public WasmPolicyEngineOptions? EngineOptions { get; set; }

    /// <summary>
    /// Maximum number of <see cref="IOpaEvaluator"/> instances to keep in the pool.
    /// </summary>
    [UsedImplicitly]
    public int MaximumEvaluatorsRetained { get; set; } = Environment.ProcessorCount * 2;

    /// <summary>
    /// How frequently recompilation is allowed to happen if policy sources have been changed. 
    /// </summary>
    [UsedImplicitly]
    public TimeSpan MonitoringInterval { get; set; } = TimeSpan.Zero;
}