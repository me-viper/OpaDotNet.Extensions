using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

internal class OpaImportsAbiFactory : IOpaImportsAbiFactory
{
    public Func<IOpaImportsAbi> ImportsAbi { get; }

    internal OpaImportsAbiFactory()
    {
        ImportsAbi = () => new DefaultImportsAbi();
    }

    public OpaImportsAbiFactory(Func<IOpaImportsAbi> importsAbi)
    {
        ArgumentNullException.ThrowIfNull(importsAbi);
        ImportsAbi = importsAbi;
    }
}