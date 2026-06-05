namespace PagoniaLand.Patcher;

/// <summary>
/// Stable identifiers for the patch operations defined in
/// <c>schemas/mod-patches/patch-file.schema.json</c>. Listed here as constants so
/// every dispatch site references the same canonical string.
/// </summary>
public static class PatchOperationTypes
{
    /// <summary>Replace a single text value at a resolved XML path.</summary>
    public const string ReplaceValue = "replaceValue";

    /// <summary>Replace one attribute on a resolved XML node. Reserved for Operation Group 1.</summary>
    public const string ReplaceAttribute = "replaceAttribute";

    /// <summary>Replace a whole XML element with a literal <c>xml</c> block. Reserved for Operation Group 1.</summary>
    public const string ReplaceNode = "replaceNode";

    /// <summary>Insert an <c>xml</c> fragment into a target list. Reserved for Operation Group 2.</summary>
    public const string AddListItem = "addListItem";

    /// <summary>Remove a list item identified by <c>expectedOldXml</c>. Reserved for Operation Group 2.</summary>
    public const string RemoveListItem = "removeListItem";

    /// <summary>Insert a complete <c>&lt;Entity&gt;</c> block. Reserved for Operation Group 3.</summary>
    public const string AddEntity = "addEntity";

    /// <summary>Remove an entity identified by <c>expectedOldXml</c>. Reserved for Operation Group 3.</summary>
    public const string RemoveEntity = "removeEntity";

    /// <summary>Merge an <c>xml</c> component block into an existing entity. Reserved for Operation Group 3.</summary>
    public const string MergeComponent = "mergeComponent";
}
