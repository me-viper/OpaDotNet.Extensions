using System.Diagnostics.CodeAnalysis;

using JetBrains.Annotations;

using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

/// <summary>
/// Specifies options for Opa policy evaluator.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public class OpaAuthorizationOptions
{
    /// <summary>
    /// Headers that can be used for policy evaluation. Supports regex.
    /// </summary>
    public HashSet<string> AllowedHeaders { get; set; } = [];

    /// <summary>
    /// If <c>true</c> will append user claims to the policy evaluation query.
    /// </summary>
    public bool IncludeClaimsInHttpRequest { get; set; }

    /// <summary>
    /// Authentication schemes OPA policies will be evaluated against.
    /// </summary>
    public HashSet<string> AuthenticationSchemes { get; set; } = [];

    /// <summary>
    /// Directory containing policy bundle source code.
    /// </summary>
    public string? PolicyBundlePath { get; set; }

    /// <summary>
    /// If <c>true</c> bundle contents are resolved prior sending to compiler; otherwise compiler will resolve
    /// bundle contents by itself.
    /// </summary>
    /// <remarks>
    /// This option is less efficient but useful for cases when underlying compiler has troubles resolving bundle contents
    /// from the file system, specifically when symlinks are involved.
    /// </remarks>
    public bool ForceBundleWriter { get; set; }

    /// <summary>
    /// List of permitted policy entrypoints.
    /// </summary>
    public HashSet<string>? Entrypoints { get; set; }

    public WasmPolicyEngineOptions? EngineOptions { get; set; }

    /// <summary>
    /// Maximum number of <see cref="IOpaEvaluator"/> instances to keep in the pool.
    /// </summary>
    public int MaximumEvaluatorsRetained { get; set; } = Environment.ProcessorCount * 2;

    /// <summary>
    /// How frequently recompilation is allowed to happen if policy sources have been changed.
    /// </summary>
    public TimeSpan MonitoringInterval { get; set; } = TimeSpan.Zero;
}