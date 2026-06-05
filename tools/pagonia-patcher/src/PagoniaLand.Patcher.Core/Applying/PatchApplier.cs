using System.Xml.Linq;

namespace PagoniaLand.Patcher;

public sealed class PatchApplier
{
    public IReadOnlyList<PatchDiagnostic> Apply(string sourceGameRoot, string outputGameRoot, CombinedPatchPlan plan)
        => Apply(sourceGameRoot, outputGameRoot, plan, CancellationToken.None);

    /// <summary>
    /// Cancellable overload of <see cref="Apply(string,string,CombinedPatchPlan)"/>.
    /// The token is checked between files in the long-running loops (the game-root
    /// mirror copy, the per-write apply, and the per-document save) so a caller (a
    /// GUI Cancel button driving a deploy off a background thread) can interrupt a
    /// large apply mid-operation. A cancellation leaves the half-written
    /// <paramref name="outputGameRoot"/> staging tree in an incomplete state but
    /// never touches the live game install — the manager only commits staged output
    /// after Apply returns cleanly.
    /// </summary>
    public IReadOnlyList<PatchDiagnostic> Apply(string sourceGameRoot, string outputGameRoot, CombinedPatchPlan plan, CancellationToken cancellationToken)
    {
        var diagnostics = new List<PatchDiagnostic>();

        if (!plan.Success)
        {
            return
            [
                new PatchDiagnostic(
                    PatchDiagnosticSeverity.Error,
                    DiagnosticCodes.ApplyBlocked,
                    "Patch plan is not clean. Refusing to apply.")
            ];
        }

        cancellationToken.ThrowIfCancellationRequested();
        CopyGameRoot(sourceGameRoot, outputGameRoot, cancellationToken);

        var documentCache = new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase);

        foreach (var write in plan.Writes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeFile = PathUtilities.ToGameRelativeFile(write.File);
            var outputPath = Path.Combine(outputGameRoot, relativeFile);

            if (!documentCache.TryGetValue(outputPath, out var document))
            {
                document = XDocument.Load(outputPath);
                documentCache[outputPath] = document;
            }

            ApplyOneWriteToDocument(write, document, outputPath, diagnostics);
        }

        foreach (var (path, document) in documentCache)
        {
            cancellationToken.ThrowIfCancellationRequested();
            document.Save(path);
        }

        ApplyEntryOperations(outputGameRoot, plan.EntryWrites, diagnostics);

        // Pattern B overlay-pak scaffold runs after patches + entry operations
        // so the scaffold walker sees the final XML set (including any new
        // *.gd.xml from entries.add). One scaffold per mod that declared pak:.
        var scaffoldWriter = new PakScaffoldWriter();
        foreach (var modPlan in plan.ModPlans)
        {
            if (modPlan.Mod.Manifest.Pak is not { } pak) continue;
            diagnostics.AddRange(scaffoldWriter.Write(outputGameRoot, pak));
        }

        if (diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error))
        {
            var entryCount = plan.EntryWrites.Count;
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.ApplyComplete,
                entryCount > 0
                    ? $"Applied {plan.Writes.Count} write(s) and {entryCount} entry operation(s) to {outputGameRoot}."
                    : $"Applied {plan.Writes.Count} write(s) to {outputGameRoot}."));
        }

        return diagnostics;
    }

    /// <summary>
    /// apply every <c>plan.Writes</c> entry in memory and
    /// return the patched XML bytes per file, without copying any un-patched
    /// files anywhere. Manager's live-install deploy uses this to pipe patched
    /// bytes straight into <c>PakRebuilder</c> with no intermediate disk
    /// staging tree. Does NOT handle <c>plan.EntryWrites</c> or Pattern B
    /// pak scaffolds — the caller is responsible for routing those through
    /// the disk-staging <see cref="Apply"/> path when needed.
    /// </summary>
    public SparseApplyResult ApplySparse(string sourceGameRoot, CombinedPatchPlan plan)
        => ApplySparse(sourceGameRoot, plan, CancellationToken.None);

    /// <summary>Cancellable overload of
    /// <see cref="ApplySparse(string,CombinedPatchPlan)"/>. The token is checked
    /// between writes (and between the per-document serialisations) — the in-memory
    /// fast path the manager's live-install deploy uses, so a Cancel there is just
    /// as responsive as on the disk-staging <see cref="Apply"/> path. The fast path
    /// produces only in-memory bytes, so a cancellation touches no files at all.</summary>
    public SparseApplyResult ApplySparse(string sourceGameRoot, CombinedPatchPlan plan, CancellationToken cancellationToken)
    {
        var diagnostics = new List<PatchDiagnostic>();

        if (!plan.Success)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyBlocked,
                "Patch plan is not clean. Refusing to apply."));
            return new SparseApplyResult(new Dictionary<string, byte[]>(), diagnostics);
        }

        // Keyed by relative path so the same source file referenced by multiple
        // writes loads + serialises once. OrdinalIgnoreCase to match Windows
        // filesystem semantics — same as <see cref="Apply"/>'s documentCache.
        var documentCache = new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase);

        foreach (var write in plan.Writes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeFile = PathUtilities.ToGameRelativeFile(write.File);
            var sourcePath = Path.Combine(sourceGameRoot, relativeFile);

            if (!documentCache.TryGetValue(relativeFile, out var document))
            {
                document = XDocument.Load(sourcePath);
                documentCache[relativeFile] = document;
            }

            // Reuse the EXACT same per-operation logic Apply uses so produced
            // bytes match byte-for-byte. outputPathForDiagnostics is the source
            // path here — diagnostics still look natural ("Could not resolve
            // target while applying: ..." with a real file path).
            ApplyOneWriteToDocument(write, document, sourcePath, diagnostics);
        }

        // Serialise each touched document to bytes. XDocument.Save to a
        // MemoryStream uses the same XmlWriter defaults Apply's
        // document.Save(path) would — output bytes match the disk-path
        // version. Forward-slash relative paths in the result for consistency
        // with how the rest of the pipeline (modifiedFiles, manifest, pak
        // entry names) handles paths cross-platform.
        var changedFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (relativeFile, document) in documentCache)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var ms = new MemoryStream();
            document.Save(ms);
            changedFiles[relativeFile.Replace('\\', '/')] = ms.ToArray();
        }

        if (diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.ApplyComplete,
                $"Sparse-applied {plan.Writes.Count} write(s) across {changedFiles.Count} file(s) (in-memory)."));
        }

        return new SparseApplyResult(changedFiles, diagnostics);
    }

    /// <summary>Dispatches one write to the right per-operation helper +
    /// emits the standard "Applied X" info diagnostic on success. Extracted
    /// so <see cref="Apply"/> and <see cref="ApplySparse"/> stay byte-for-byte
    /// equivalent on every operation type.</summary>
    private static void ApplyOneWriteToDocument(
        PatchWrite write,
        XDocument document,
        string outputPathForDiagnostics,
        List<PatchDiagnostic> diagnostics)
    {
        bool applied;

        if (write.OperationType == PatchOperationTypes.AddEntity)
        {
            applied = ApplyAddEntity(write, document, outputPathForDiagnostics, diagnostics);
        }
        else if (write.OperationType == PatchOperationTypes.RemoveEntity)
        {
            applied = ApplyRemoveEntity(write, document, outputPathForDiagnostics, diagnostics);
        }
        else if (write.OperationType == PatchOperationTypes.MergeComponent)
        {
            applied = ApplyMergeComponent(write, document, outputPathForDiagnostics, diagnostics);
        }
        else
        {
            var targetElement = XmlTargetResolver.ResolveValueElement(document, write.EntityGuid, write.Component, write.Path);

            if (targetElement is null)
            {
                diagnostics.Add(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Error,
                    DiagnosticCodes.ApplyTargetMissing,
                    $"Could not resolve target while applying: {write.File}",
                    outputPathForDiagnostics));
                return;
            }

            applied = write.OperationType switch
            {
                PatchOperationTypes.ReplaceValue => ApplyReplaceValue(write, targetElement, outputPathForDiagnostics, diagnostics),
                PatchOperationTypes.ReplaceAttribute => ApplyReplaceAttribute(write, targetElement, outputPathForDiagnostics, diagnostics),
                PatchOperationTypes.ReplaceNode => ApplyReplaceNode(write, targetElement, outputPathForDiagnostics, diagnostics),
                PatchOperationTypes.AddListItem => ApplyAddListItem(write, targetElement, outputPathForDiagnostics, diagnostics),
                PatchOperationTypes.RemoveListItem => ApplyRemoveListItem(write, targetElement, outputPathForDiagnostics, diagnostics),
                _ => false,
            };
        }

        if (!applied)
        {
            return;
        }

        diagnostics.Add(new PatchDiagnostic(
            PatchDiagnosticSeverity.Info,
            DiagnosticCodes.PatchApplied,
            $"Applied {write.OperationId}: {Summarise(write.OldValue)} -> {Summarise(write.NewValue)}",
            outputPathForDiagnostics));
    }

    private static bool ApplyReplaceValue(PatchWrite write, XElement targetElement, string outputPath, List<PatchDiagnostic> diagnostics)
    {
        if (!string.Equals(targetElement.Value, write.OldValue, StringComparison.Ordinal))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyOldValueMismatch,
                $"Output value changed before apply. Expected '{write.OldValue}', found '{targetElement.Value}'.",
                outputPath));
            return false;
        }

        targetElement.Value = write.NewValue;
        return true;
    }

    private static bool ApplyReplaceAttribute(PatchWrite write, XElement targetElement, string outputPath, List<PatchDiagnostic> diagnostics)
    {
        if (string.IsNullOrEmpty(write.Attribute))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyTargetMissing,
                $"Apply step for '{write.OperationId}' is missing an attribute name.",
                outputPath));
            return false;
        }

        var attribute = targetElement.Attribute(write.Attribute);

        if (attribute is null)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyTargetMissing,
                $"Attribute '{write.Attribute}' was not found on the output node.",
                outputPath));
            return false;
        }

        if (!string.Equals(attribute.Value, write.OldValue, StringComparison.Ordinal))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyOldValueMismatch,
                $"Output attribute value changed before apply. Expected '{write.OldValue}', found '{attribute.Value}'.",
                outputPath));
            return false;
        }

        attribute.Value = write.NewValue;
        return true;
    }

    private static bool ApplyReplaceNode(PatchWrite write, XElement targetElement, string outputPath, List<PatchDiagnostic> diagnostics)
    {
        var currentXml = targetElement.ToString(SaveOptions.DisableFormatting);

        if (!string.Equals(currentXml, write.OldValue, StringComparison.Ordinal))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyOldValueMismatch,
                $"Output node changed before apply at '{write.Component}/{write.Path}'.",
                outputPath));
            return false;
        }

        XElement replacement;
        try
        {
            replacement = XElement.Parse(write.NewValue);
        }
        catch (System.Xml.XmlException exception)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.InvalidPatchOperationXml,
                $"Replacement XML for '{write.OperationId}' is invalid: {exception.Message}.",
                outputPath));
            return false;
        }

        targetElement.ReplaceWith(replacement);
        return true;
    }

    private static bool ApplyAddListItem(PatchWrite write, XElement container, string outputPath, List<PatchDiagnostic> diagnostics)
    {
        XElement newItem;
        try
        {
            newItem = XElement.Parse(write.NewValue);
        }
        catch (System.Xml.XmlException exception)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.InvalidPatchOperationXml,
                $"List item XML for '{write.OperationId}' is invalid: {exception.Message}.",
                outputPath));
            return false;
        }

        container.Add(newItem);
        return true;
    }

    private static bool ApplyRemoveListItem(PatchWrite write, XElement container, string outputPath, List<PatchDiagnostic> diagnostics)
    {
        var match = container.Elements()
            .FirstOrDefault(child => string.Equals(
                child.ToString(SaveOptions.DisableFormatting),
                write.OldValue,
                StringComparison.Ordinal));

        if (match is null)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyListItemMissing,
                $"No child of '{write.Component}/{write.Path}' matched the expected list item for '{write.OperationId}'.",
                outputPath));
            return false;
        }

        match.Remove();
        return true;
    }

    private static bool ApplyAddEntity(PatchWrite write, XDocument document, string outputPath, List<PatchDiagnostic> diagnostics)
    {
        XElement newEntity;
        try
        {
            newEntity = XElement.Parse(write.NewValue);
        }
        catch (System.Xml.XmlException exception)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.InvalidPatchOperationXml,
                $"Entity XML for '{write.OperationId}' is invalid: {exception.Message}.",
                outputPath));
            return false;
        }

        var newGuid = (string?)newEntity.Attribute("Guid");

        if (newGuid is not null && document.Descendants("Entity")
            .Any(element => string.Equals((string?)element.Attribute("Guid"), newGuid, StringComparison.OrdinalIgnoreCase)))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyEntityAlreadyExists,
                $"An entity with Guid '{newGuid}' already exists in the output document.",
                outputPath));
            return false;
        }

        // The group name is carried in the Component slot for addEntity writes (see ResolveAddEntity).
        var group = document.Descendants("Group")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("Name"), write.Component, StringComparison.Ordinal));
        var entities = group?.Element("Entities");

        if (entities is null)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyTargetMissing,
                $"Group '{write.Component}' is missing in the output document.",
                outputPath));
            return false;
        }

        entities.Add(newEntity);
        return true;
    }

    private static bool ApplyRemoveEntity(PatchWrite write, XDocument document, string outputPath, List<PatchDiagnostic> diagnostics)
    {
        var entity = document.Descendants("Entity")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("Guid"), write.EntityGuid, StringComparison.OrdinalIgnoreCase));

        if (entity is null)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyEntityMissing,
                $"Entity '{write.EntityGuid}' was not found in the output document.",
                outputPath));
            return false;
        }

        if (!string.Equals(entity.ToString(SaveOptions.DisableFormatting), write.OldValue, StringComparison.Ordinal))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyOldValueMismatch,
                $"Output entity '{write.EntityGuid}' changed before apply.",
                outputPath));
            return false;
        }

        entity.Remove();
        return true;
    }

    private static bool ApplyMergeComponent(PatchWrite write, XDocument document, string outputPath, List<PatchDiagnostic> diagnostics)
    {
        XElement parsedComponent;
        try
        {
            parsedComponent = XElement.Parse(write.NewValue);
        }
        catch (System.Xml.XmlException exception)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.InvalidPatchOperationXml,
                $"Component XML for '{write.OperationId}' is invalid: {exception.Message}.",
                outputPath));
            return false;
        }

        var entity = document.Descendants("Entity")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("Guid"), write.EntityGuid, StringComparison.OrdinalIgnoreCase));

        if (entity is null)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyEntityMissing,
                $"Entity '{write.EntityGuid}' was not found in the output document.",
                outputPath));
            return false;
        }

        var values = entity.Element("Values");
        if (values is null)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ApplyComponentMissing,
                $"Entity '{write.EntityGuid}' has no <Values> block in the output document.",
                outputPath));
            return false;
        }

        var existing = values.Element(write.Component);
        if (existing is null)
        {
            values.Add(parsedComponent);
        }
        else
        {
            // Add the new children to the existing component. Duplicate children are not deduplicated;
            // mod authors are expected to coordinate via the conflict detector.
            foreach (var child in parsedComponent.Elements())
            {
                existing.Add(new XElement(child));
            }
        }

        return true;
    }

    private static string Summarise(string value)
    {
        const int maxLength = 80;
        var collapsed = value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace('\n', ' ');
        return collapsed.Length <= maxLength
            ? collapsed
            : collapsed[..maxLength] + "...";
    }

    private static void ApplyEntryOperations(
        string outputGameRoot,
        IReadOnlyList<PatchEntryWrite> entries,
        List<PatchDiagnostic> diagnostics)
    {
        if (entries.Count == 0) return;

        // Replace and Add both materialise the source bytes at the canonical
        // entry path under outputGameRoot. The packer step that turns
        // outputGameRoot back into a .pak then decides whether the resulting
        // entry was a substitution or a brand-new addition based on whether
        // the base pak had that path. Delete writes the path into a sidecar
        // list (.entries-deleted.txt) that the packer reads and uses as its
        // omission set; the path is also removed from outputGameRoot if it
        // happens to be present there.
        var deletionRecords = new List<string>();

        foreach (var entry in entries)
        {
            if (entry.Operation == EntryOperationType.Delete)
            {
                deletionRecords.Add(entry.Path);
                var stalePath = Path.Combine(outputGameRoot, EntryRelativePath(entry.Path));
                try
                {
                    if (File.Exists(stalePath))
                    {
                        File.Delete(stalePath);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    diagnostics.Add(new PatchDiagnostic(
                        PatchDiagnosticSeverity.Error,
                        DiagnosticCodes.EntrySourceUnreadable,
                        $"Failed to delete stale pak entry '{stalePath}': {exception.Message}",
                        stalePath));
                    continue;
                }
                diagnostics.Add(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Info,
                    DiagnosticCodes.EntryDeleted,
                    $"Marked pak entry for deletion: {entry.Path}",
                    outputGameRoot));
                continue;
            }

            if (entry.SourceFile is null) continue; // defensive; planner should have populated

            var outputPath = Path.Combine(outputGameRoot, EntryRelativePath(entry.Path));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.Copy(entry.SourceFile, outputPath, overwrite: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Error,
                    DiagnosticCodes.EntrySourceUnreadable,
                    $"Failed to copy entry source '{entry.SourceFile}' to '{outputPath}': {exception.Message}",
                    entry.SourceFile));
                continue;
            }

            var code = entry.Operation == EntryOperationType.Replace
                ? DiagnosticCodes.EntryReplaced
                : DiagnosticCodes.EntryAdded;
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                code,
                $"{(entry.Operation == EntryOperationType.Replace ? "Replaced" : "Added")} pak entry: {entry.Path}",
                outputPath));
        }

        if (deletionRecords.Count > 0)
        {
            // Stable order so the file is deterministic.
            var sorted = deletionRecords
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
            var deletionsFile = Path.Combine(outputGameRoot, ".entries-deleted.txt");
            File.WriteAllLines(deletionsFile, sorted);
        }
    }

    /// <summary>
    /// Translate an in-pak entry path (forward slashes) to a host-OS relative
    /// path under the output root. The path is already normalised by the
    /// schema's <c>pakEntryPath</c> regex (no traversal, no rooted paths).
    /// </summary>
    private static string EntryRelativePath(string entryPath)
        => entryPath.Replace('/', Path.DirectorySeparatorChar);

    private static void CopyGameRoot(string sourceGameRoot, string outputGameRoot, CancellationToken cancellationToken)
    {
        if (Directory.Exists(outputGameRoot))
        {
            Directory.Delete(outputGameRoot, recursive: true);
        }

        Directory.CreateDirectory(outputGameRoot);

        foreach (var file in Directory.EnumerateFiles(sourceGameRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeFile = Path.GetRelativePath(sourceGameRoot, file);
            var outputFile = Path.Combine(outputGameRoot, relativeFile);
            // Path.Combine always produces a path with a directory component because outputGameRoot is rooted.
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
            File.Copy(file, outputFile, overwrite: true);
        }
    }
}
