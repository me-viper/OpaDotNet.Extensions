using OpaDotNet.Wasm;

namespace OpaDotNet.Extensions.AspNetCore;

internal class OpaImportsAbiFactory : IOpaImportsAbiFactory
{
    public Func<IOpaImportsAbi> ImportsAbi { get; }

    public Func<Stream>? Capabilities { get; }

    internal OpaImportsAbiFactory()
    {
        ImportsAbi = () => new CoreImportsAbi();
    }

    public OpaImportsAbiFactory(Func<IOpaImportsAbi> importsAbi)
    {
        ArgumentNullException.ThrowIfNull(importsAbi);

        ImportsAbi = importsAbi;
    }

    public OpaImportsAbiFactory(Func<IOpaImportsAbi> importsAbi, Func<Stream> capabilities)
    {
        ArgumentNullException.ThrowIfNull(importsAbi);
        ArgumentNullException.ThrowIfNull(capabilities);

        ImportsAbi = importsAbi;
        Capabilities = capabilities;
    }
}