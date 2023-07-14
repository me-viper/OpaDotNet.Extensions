using System.Diagnostics.CodeAnalysis;

using JetBrains.Annotations;

using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

[ExcludeFromCodeCoverage]
public class OpaPolicyHandlerOptions
{
    /// <summary>
    /// Headers that can be used for policy evaluation. Supports regex.
    /// </summary>
    [UsedImplicitly]
    public HashSet<string> AllowedHeaders { get; set; } = new();

    [UsedImplicitly]
    public bool IncludeClaimsInHttpRequest { get; set; }

    [UsedImplicitly]
    public string PolicyBundlePath { get; set; } = default!;

    [UsedImplicitly]
    public HashSet<string>? Entrypoints { get; set; }

    [UsedImplicitly]
    public WasmPolicyEngineOptions? EngineOptions { get; set; }
}