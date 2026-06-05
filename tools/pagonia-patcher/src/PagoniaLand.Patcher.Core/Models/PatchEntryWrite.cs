namespace PagoniaLand.Patcher;

public enum EntryOperationType
{
    Replace,
    Add,
    Delete,
}

/// <summary>
/// One resolved binary entry operation from a mod manifest's <c>entries:</c>
/// section. Replace and Add carry a <see cref="SourceFile"/> that must exist
/// on disk; Delete leaves it null.
/// </summary>
public sealed record PatchEntryWrite(
    string ModId,
    EntryOperationType Operation,
    string Path,
    string? SourceFile);

public sealed record PatchEntryConflict(
    string Type,
    string Path,
    IReadOnlyList<PatchEntryWrite> Writes);
