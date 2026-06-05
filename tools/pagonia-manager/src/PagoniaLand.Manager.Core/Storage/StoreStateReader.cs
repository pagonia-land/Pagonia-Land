using System.Diagnostics.CodeAnalysis;

namespace PagoniaLand.Manager;

public sealed class StoreStateReader
{
    // AOT: pin the state.yaml model types so YamlDotNet's reflection survives trimming.
    private const DynamicallyAccessedMemberTypes Shape =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    [DynamicDependency(Shape, typeof(StoreState))]
    [DynamicDependency(Shape, typeof(StoreLastDeploy))]
    [DynamicDependency(Shape, typeof(InstallRecord))]
    [DynamicDependency(Shape, typeof(OwnedExpansions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(Dictionary<string, InstallRecord>))]
    public StoreStateReader()
    {
    }

    public bool Exists(StoreLayout layout) => File.Exists(layout.StateFile);

    public StoreState Read(StoreLayout layout)
    {
        if (!File.Exists(layout.StateFile))
        {
            throw new InvalidOperationException(
                $"[{ManagerDiagnosticCodes.StoreNotInitialised}] " +
                $"Store at '{layout.Root}' is not initialised. Run 'pagonia-manager store init' first.");
        }

        var yaml = File.ReadAllText(layout.StateFile);
        var state = ManagerYaml.CreateDeserializer().Deserialize<StoreState>(yaml);
        if (state is null)
        {
            throw new InvalidOperationException(
                $"[{ManagerDiagnosticCodes.StoreStateUnreadable}] " +
                $"state.yaml at '{layout.StateFile}' is empty or invalid.");
        }

        return state;
    }
}
