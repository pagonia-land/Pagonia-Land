using System.Diagnostics.CodeAnalysis;

namespace PagoniaLand.Manager;

public sealed class StoreStateWriter
{
    // AOT: StoreStateReader has matching DynamicDependency for the READ path.
    // Pinning here makes the write path safe independently of reader-side coverage.
    private const DynamicallyAccessedMemberTypes Shape =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    [DynamicDependency(Shape, typeof(StoreState))]
    [DynamicDependency(Shape, typeof(StoreLastDeploy))]
    [DynamicDependency(Shape, typeof(InstallRecord))]
    [DynamicDependency(Shape, typeof(OwnedExpansions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(Dictionary<string, InstallRecord>))]
    public StoreStateWriter()
    {
    }

    public void Write(StoreLayout layout, StoreState state)
    {
        AtomicFile.WriteAllText(layout.StateFile, ManagerYaml.CreateSerializer().Serialize(state));
    }
}
