using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace PagoniaLand.Patcher;

/// <summary>
/// Cross-checks a mod-distribution repo's top-level <c>index.yaml</c> against each mod's authoritative
/// <c>mod.yaml</c>, and optionally re-syncs the mirrored fields back into the index.
///
/// <para>
/// The index duplicates a curated subset of every mod's manifest so the manager can browse a catalog
/// (name, version, safety) <em>without</em> fetching each mod's folder — the copy is a deliberate cache,
/// but it can silently drift from the manifest it mirrors. This class is the drift detector and fixer.
/// </para>
///
/// <para>
/// <b>Mirror contract</b> — these index fields MUST equal their <c>mod.yaml</c> source (drift = bug):
/// <list type="bullet">
///   <item><c>displayName</c> ↔ manifest <c>name</c></item>
///   <item><c>version</c> ↔ manifest <c>version</c></item>
///   <item><c>gameDatabaseVersion</c> ↔ manifest <c>gameDatabaseVersion</c></item>
///   <item><c>safetyFlags.{requiresNewGame,safeToRemove,multiplayerSafe,campaignSafe}</c> ↔ the flat
///   manifest fields of the same names (note flat ↔ nested shape change)</item>
/// </list>
/// These index fields are <b>curated</b> and intentionally allowed to differ — never compared:
/// <c>description</c> (the index carries a short catalog blurb; the manifest the full text),
/// <c>tags</c>, <c>screenshots</c>, and the index-only <c>path</c>.
/// </para>
/// </summary>
public sealed class RepoIndexMirror
{
    private readonly ManifestReader _manifestReader = new();

    private enum IssueKind
    {
        MirrorMismatch,
        OrphanEntry,
        MissingEntry,
        IdMismatch,
    }

    private sealed record MirrorIssue(
        IssueKind Kind,
        string EntryId,
        string Field,
        string? IndexValue,
        string? ModValue,
        YamlScalarNode? IndexNode);

    /// <summary>Read-only drift check. Errors on any mirror mismatch or structural orphan/missing entry.</summary>
    public IReadOnlyList<PatchDiagnostic> Check(string repoRoot)
    {
        var diagnostics = new List<PatchDiagnostic>();
        if (!TryLoad(repoRoot, diagnostics, out var indexPath, out var rawText, out var stream))
        {
            return diagnostics;
        }

        var issues = ComputeIssues(repoRoot, stream!, diagnostics);
        foreach (var issue in issues)
        {
            diagnostics.Add(IssueToDiagnostic(issue, indexPath));
        }

        if (issues.Count == 0 && diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error))
        {
            diagnostics.Add(Info(DiagnosticCodes.IndexMirrorInSync, "index.yaml mirror fields are in sync with every mod.yaml.", indexPath));
        }

        return diagnostics;
    }

    /// <summary>
    /// Re-sync the index's mirror fields from each <c>mod.yaml</c>. With <paramref name="checkOnly"/> true,
    /// reports drift without writing (CI gate). Otherwise rewrites the drifted scalar values in place,
    /// preserving all surrounding formatting; structural gaps that can't be patched surgically are reported
    /// for the maintainer to resolve by hand.
    /// </summary>
    public IReadOnlyList<PatchDiagnostic> Build(string repoRoot, bool checkOnly)
    {
        var diagnostics = new List<PatchDiagnostic>();
        if (!TryLoad(repoRoot, diagnostics, out var indexPath, out var rawText, out var stream))
        {
            return diagnostics;
        }

        var issues = ComputeIssues(repoRoot, stream!, diagnostics);

        // Structural issues (orphan / missing / id mismatch) are never auto-fixable — a human must
        // add, remove, or rename an entry. Surface them either way.
        foreach (var structural in issues.Where(i => i.Kind != IssueKind.MirrorMismatch))
        {
            diagnostics.Add(IssueToDiagnostic(structural, indexPath));
        }

        var mismatches = issues.Where(i => i.Kind == IssueKind.MirrorMismatch).ToList();
        var fixable = mismatches.Where(i => i.IndexNode is not null && i.ModValue is not null).ToList();
        var manual = mismatches.Where(i => i.IndexNode is null || i.ModValue is null).ToList();

        foreach (var m in manual)
        {
            // e.g. the index lacks the scalar entirely, or the manifest omits a safety flag the index
            // still declares — can't be resolved by a value swap without restructuring lines.
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.IndexMirrorManualFixNeeded,
                $"'{m.EntryId}' {m.Field}: index={Show(m.IndexValue)} vs mod.yaml={Show(m.ModValue)} — needs a manual edit (cannot be auto-synced in place).",
                indexPath));
        }

        // Read-old / write-new: if indexFormatVersion is an older same-major minor, bump it up to the
        // current minor in the same in-place rewrite. (Newer minors / majors are the read gate's concern,
        // never a silent rewrite here.) Returns null when no migration is due.
        var rootMap = (YamlMappingNode)stream!.Documents[0].RootNode;
        var versionMigration = ComputeVersionMigration(rootMap);

        // Entries with no contentHash yet — index build inserts a freshly-computed one (a brand-new
        // optional integrity field, so there's nothing to splice over; it's an insertion).
        var contentHashInsertions = ComputeContentHashInsertions(repoRoot, stream!);

        if (checkOnly)
        {
            foreach (var f in fixable)
            {
                diagnostics.Add(IssueToDiagnostic(f, indexPath));
            }

            if (versionMigration is { } pending)
            {
                diagnostics.Add(Info(
                    DiagnosticCodes.FormatMigratedInPlace,
                    $"indexFormatVersion {pending.Declared} -> {pending.Target}: would migrate to the current format minor on the next `index build`.",
                    indexPath));
            }

            foreach (var insertion in contentHashInsertions)
            {
                diagnostics.Add(Info(
                    DiagnosticCodes.IndexMirrorUpdated,
                    $"'{insertion.EntryId}' contentHash: would add {insertion.Hash} on the next `index build`.",
                    indexPath));
            }

            if (mismatches.Count == 0 && issues.Count == 0 && versionMigration is null && contentHashInsertions.Count == 0)
            {
                diagnostics.Add(Info(DiagnosticCodes.IndexMirrorInSync, "index.yaml mirror fields are in sync with every mod.yaml.", indexPath));
            }

            return diagnostics;
        }

        // Collect every edit as a (start, end, newText) span over the raw source plus the diagnostic
        // to emit once applied. A value re-sync / version bump replaces its scalar span; a new
        // contentHash is a zero-width insertion at the end of the entry's `path` line.
        var edits = new List<(int Start, int End, string NewText, PatchDiagnostic Diagnostic)>();
        foreach (var f in fixable)
        {
            edits.Add((
                (int)f.IndexNode!.Start.Index,
                (int)f.IndexNode!.End.Index,
                FormatScalar(f.ModValue!, f.IndexNode!.Style, f.Field),
                Info(DiagnosticCodes.IndexMirrorUpdated, $"'{f.EntryId}' {f.Field}: {Show(f.IndexValue)} -> {Show(f.ModValue)}.", indexPath)));
        }
        if (versionMigration is { } migration)
        {
            edits.Add((
                (int)migration.Node.Start.Index,
                (int)migration.Node.End.Index,
                FormatScalar(migration.Target, migration.Node.Style, "indexFormatVersion"),
                Info(DiagnosticCodes.FormatMigratedInPlace, $"indexFormatVersion {migration.Declared} -> {migration.Target} (migrated to the current format minor).", indexPath)));
        }
        foreach (var insertion in contentHashInsertions)
        {
            var at = EndOfLineIndex(rawText!, (int)insertion.Anchor.End.Index);
            edits.Add((at, at, $"\n{insertion.Indent}contentHash: {insertion.Hash}",
                Info(DiagnosticCodes.IndexMirrorUpdated, $"'{insertion.EntryId}' contentHash: added {insertion.Hash}.", indexPath)));
        }

        if (edits.Count == 0)
        {
            diagnostics.Add(Info(
                manual.Count == 0 && issues.Count == 0 ? DiagnosticCodes.IndexMirrorInSync : DiagnosticCodes.IndexMirrorUpdated,
                manual.Count == 0 && issues.Count == 0 ? "index.yaml already in sync — nothing to write." : "No auto-fixable drift; see the issues above.",
                indexPath));
            return diagnostics;
        }

        // Apply right-to-left so earlier edits don't shift later spans' character indices.
        var updated = rawText!;
        foreach (var edit in edits.OrderByDescending(e => e.Start))
        {
            updated = updated[..edit.Start] + edit.NewText + updated[edit.End..];
            diagnostics.Add(edit.Diagnostic);
        }

        // Never write YAML we can't read back. A value that needed quoting but slipped through,
        // or any other splice mishap, must not silently corrupt the catalog the command exists to fix.
        try
        {
            new YamlStream().Load(new StringReader(updated));
        }
        catch (YamlException exception)
        {
            diagnostics.RemoveAll(d => d.Code is DiagnosticCodes.IndexMirrorUpdated or DiagnosticCodes.FormatMigratedInPlace);
            diagnostics.Add(Error(
                DiagnosticCodes.IndexMirrorWriteAborted,
                $"Re-syncing index.yaml would have produced invalid YAML ({exception.Message}); nothing was written. Fix the offending mod.yaml value or the index entry by hand.",
                indexPath));
            return diagnostics;
        }

        File.WriteAllText(indexPath, updated);
        return diagnostics;
    }

    // The index's own format version is not a mirror of any mod.yaml — it follows the shared
    // MAJOR.MINOR contract. index build stamps the current minor when it touches a file authored
    // against an older same-major minor, so a purely additive format bump rises the next time the
    // tool runs rather than forcing a hand-edit.
    private static (YamlScalarNode Node, string Target, string Declared)? ComputeVersionMigration(YamlMappingNode root)
    {
        if (!TryGetChild(root, "indexFormatVersion", out var node) || node is not YamlScalarNode scalar || scalar.Value is not { } declared)
        {
            return null;
        }

        if (!FormatVersionPolicy.TryParse(declared, out var parsed))
        {
            return null;
        }

        var known = FormatVersionPolicy.KnownVersion(ManagedFormat.RepoIndex);
        return parsed.Major == known.Major && parsed.Minor < known.Minor
            ? (scalar, known.ToString(), declared)
            : null;
    }

    // Mod entries that carry no contentHash yet — index build inserts a freshly-computed one. Anchored
    // on the entry's `path` line (always present), indented to match its sibling keys.
    private static List<(YamlScalarNode Anchor, string Indent, string Hash, string EntryId)> ComputeContentHashInsertions(string repoRoot, YamlStream stream)
    {
        var result = new List<(YamlScalarNode, string, string, string)>();
        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        if (!TryGetChild(root, "mods", out var modsNode) || modsNode is not YamlSequenceNode mods)
        {
            return result;
        }

        foreach (var entry in mods.Children.OfType<YamlMappingNode>())
        {
            // Already advertises one (any value) — handled by the re-sync path, not insertion.
            if (TryGetChild(entry, "contentHash", out _))
            {
                continue;
            }

            var path = ScalarValue(entry, "path");
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            var modDirectory = Path.Combine(repoRoot, path.Replace('/', Path.DirectorySeparatorChar));
            var hash = ContentHash.OfModPayload(modDirectory);
            if (hash is null)
            {
                continue; // no readable mod.yaml — orphan/missing checks own that
            }

            var pathKey = KeyNode(entry, "path");
            var pathValue = TryGetChild(entry, "path", out var pv) ? pv as YamlScalarNode : null;
            if (pathKey is null || pathValue is null)
            {
                continue;
            }

            var indent = new string(' ', Math.Max(0, (int)pathKey.Start.Column - 1));
            result.Add((pathValue, indent, hash, ScalarValue(entry, "id") ?? string.Empty));
        }

        return result;
    }

    // Index of the line terminator at/after <paramref name="from"/> (or end of text). Inserting a new
    // line at this position appends it after the anchor's line, so a trailing inline comment is left
    // intact rather than split.
    private static int EndOfLineIndex(string text, int from)
    {
        var newline = text.IndexOf('\n', Math.Min(from, text.Length));
        return newline < 0 ? text.Length : newline;
    }

    private static YamlScalarNode? KeyNode(YamlMappingNode map, string key)
    {
        foreach (var kv in map.Children)
        {
            if (kv.Key is YamlScalarNode scalar && scalar.Value == key)
            {
                return scalar;
            }
        }
        return null;
    }

    private bool TryLoad(string repoRoot, List<PatchDiagnostic> diagnostics, out string indexPath, out string? rawText, out YamlStream? stream)
    {
        indexPath = Path.Combine(repoRoot, "index.yaml");
        rawText = null;
        stream = null;

        if (!File.Exists(indexPath))
        {
            diagnostics.Add(Error(DiagnosticCodes.IndexReadFailed, $"index.yaml not found under '{repoRoot}'.", indexPath));
            return false;
        }

        try
        {
            rawText = File.ReadAllText(indexPath);
            var loaded = new YamlStream();
            loaded.Load(new StringReader(rawText));
            stream = loaded;
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(DiagnosticCodes.IndexReadFailed, $"Cannot read index.yaml: {ex.Message}", indexPath));
            return false;
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode)
        {
            diagnostics.Add(Error(DiagnosticCodes.IndexReadFailed, "index.yaml is empty or its root is not a mapping.", indexPath));
            return false;
        }

        return true;
    }

    private List<MirrorIssue> ComputeIssues(string repoRoot, YamlStream stream, List<PatchDiagnostic> diagnostics)
    {
        var issues = new List<MirrorIssue>();
        var root = (YamlMappingNode)stream.Documents[0].RootNode;

        var indexedIds = new HashSet<string>(StringComparer.Ordinal);

        if (TryGetChild(root, "mods", out var modsNode) && modsNode is YamlSequenceNode mods)
        {
            foreach (var entryNode in mods.Children.OfType<YamlMappingNode>())
            {
                var id = ScalarValue(entryNode, "id") ?? string.Empty;
                var path = ScalarValue(entryNode, "path");
                if (!string.IsNullOrEmpty(id))
                {
                    indexedIds.Add(id);
                }

                if (string.IsNullOrEmpty(path))
                {
                    // Schema validation owns "path is required"; nothing to cross-check without it.
                    continue;
                }

                var modDirectory = Path.Combine(repoRoot, path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(Path.Combine(modDirectory, "mod.yaml")))
                {
                    issues.Add(new MirrorIssue(IssueKind.OrphanEntry, id, "path", path, null, null));
                    continue;
                }

                var manifestResult = _manifestReader.ReadModManifest(modDirectory);
                if (manifestResult.Value is not { } manifest)
                {
                    foreach (var d in manifestResult.Diagnostics.Where(d => d.Severity == PatchDiagnosticSeverity.Error))
                    {
                        diagnostics.Add(d);
                    }

                    continue;
                }

                if (!string.IsNullOrEmpty(manifest.Id) && !string.Equals(manifest.Id, id, StringComparison.Ordinal))
                {
                    issues.Add(new MirrorIssue(IssueKind.IdMismatch, id, "id", id, manifest.Id, null));
                }

                CompareEntry(entryNode, id, manifest, modDirectory, issues);
            }
        }

        // Mods present on disk but absent from the index — invisible to the catalog.
        var modsRoot = Path.Combine(repoRoot, "mods");
        if (Directory.Exists(modsRoot))
        {
            foreach (var dir in Directory.EnumerateDirectories(modsRoot))
            {
                if (!File.Exists(Path.Combine(dir, "mod.yaml")))
                {
                    continue;
                }

                var manifestResult = _manifestReader.ReadModManifest(dir);
                var id = manifestResult.Value?.Id;
                if (!string.IsNullOrEmpty(id) && !indexedIds.Contains(id))
                {
                    issues.Add(new MirrorIssue(IssueKind.MissingEntry, id, "id", null, id, null));
                }
            }
        }

        return issues;
    }

    private static void CompareEntry(YamlMappingNode entry, string id, ModManifest manifest, string modDirectory, List<MirrorIssue> issues)
    {
        AddScalarMismatch(entry, id, "displayName", manifest.Name, issues);
        AddScalarMismatch(entry, id, "version", manifest.Version, issues);
        AddScalarMismatch(entry, id, "gameDatabaseVersion", manifest.GameDatabaseVersion, issues);

        YamlMappingNode? safety = TryGetChild(entry, "safetyFlags", out var s) ? s as YamlMappingNode : null;
        AddSafetyMismatch(safety, id, "requiresNewGame", manifest.RequiresNewGame, issues);
        AddSafetyMismatch(safety, id, "safeToRemove", manifest.SafeToRemove, issues);
        AddSafetyMismatch(safety, id, "multiplayerSafe", manifest.MultiplayerSafe, issues);
        AddSafetyMismatch(safety, id, "campaignSafe", manifest.CampaignSafe, issues);

        AddContentHashMismatch(entry, id, modDirectory, issues);
    }

    // contentHash is a COMPUTED mirror field (sha256 of the mod's logical payload), not a copy of a
    // mod.yaml value. When the entry carries it, drift means the content changed under a possibly
    // unchanged version — the exact same-version footgun the hash exists to catch. An absent
    // contentHash is "not advertised" (left for `index build` to insert, not flagged by Check).
    private static void AddContentHashMismatch(YamlMappingNode entry, string id, string modDirectory, List<MirrorIssue> issues)
    {
        var node = TryGetChild(entry, "contentHash", out var n) ? n as YamlScalarNode : null;
        if (node?.Value is not { Length: > 0 } stored)
        {
            return;
        }

        var computed = ContentHash.OfModPayload(modDirectory);
        if (computed is null)
        {
            return; // no readable mod.yaml — other checks own that
        }

        if (!string.Equals(stored, computed, StringComparison.Ordinal))
        {
            issues.Add(new MirrorIssue(IssueKind.MirrorMismatch, id, "contentHash", stored, computed, node));
        }
    }

    // The index is a curated SUBSET: an entry may legitimately omit a field (the catalog just won't
    // surface it). So drift is "the index carries this field and its value disagrees with the
    // manifest" — a present-but-wrong copy, the case that misleads a browsing user. An absent index
    // field is a curation choice, not drift, and is left alone.
    private static void AddScalarMismatch(YamlMappingNode entry, string id, string field, string manifestValue, List<MirrorIssue> issues)
    {
        var node = TryGetChild(entry, field, out var n) ? n as YamlScalarNode : null;
        if (node?.Value is not { } indexValue)
        {
            return;
        }

        var modValue = string.IsNullOrEmpty(manifestValue) ? null : manifestValue;
        if (!string.Equals(indexValue, modValue, StringComparison.Ordinal))
        {
            issues.Add(new MirrorIssue(IssueKind.MirrorMismatch, id, field, indexValue, modValue, node));
        }
    }

    private static void AddSafetyMismatch(YamlMappingNode? safety, string id, string field, SafetyState? manifestState, List<MirrorIssue> issues)
    {
        YamlScalarNode? node = safety is not null && TryGetChild(safety, field, out var n) ? n as YamlScalarNode : null;
        if (node?.Value is not { } indexValue)
        {
            return;
        }

        var modValue = SafetyText(manifestState);
        if (!string.Equals(indexValue, modValue, StringComparison.Ordinal))
        {
            issues.Add(new MirrorIssue(IssueKind.MirrorMismatch, id, $"safetyFlags.{field}", indexValue, modValue, node));
        }
    }

    private static PatchDiagnostic IssueToDiagnostic(MirrorIssue issue, string indexPath) => issue.Kind switch
    {
        // contentHash is computed, not mirrored from mod.yaml — a drift means the mod's content
        // changed (possibly under an unchanged version). Word it as such, not "vs mod.yaml".
        IssueKind.MirrorMismatch when issue.Field == "contentHash" => Error(
            DiagnosticCodes.IndexMirrorMismatch,
            $"'{issue.EntryId}' contentHash is stale (index={Show(issue.IndexValue)} vs computed={Show(issue.ModValue)}) — the mod's content changed; run `index build` to refresh it.",
            indexPath),
        IssueKind.MirrorMismatch => Error(
            DiagnosticCodes.IndexMirrorMismatch,
            $"'{issue.EntryId}' {issue.Field}: index={Show(issue.IndexValue)} vs mod.yaml={Show(issue.ModValue)}.",
            indexPath),
        IssueKind.OrphanEntry => Error(
            DiagnosticCodes.IndexEntryOrphaned,
            $"'{issue.EntryId}' lists path '{issue.IndexValue}' but no mod.yaml exists there.",
            indexPath),
        IssueKind.MissingEntry => Error(
            DiagnosticCodes.IndexEntryMissing,
            $"mod '{issue.ModValue}' has a mod.yaml on disk but no index.yaml entry — it won't appear in the catalog.",
            indexPath),
        IssueKind.IdMismatch => Error(
            DiagnosticCodes.IndexEntryIdMismatch,
            $"index entry id '{issue.IndexValue}' does not match its mod.yaml id '{issue.ModValue}'.",
            indexPath),
        _ => Error(DiagnosticCodes.IndexMirrorMismatch, issue.Field, indexPath),
    };

    private static bool TryGetChild(YamlMappingNode map, string key, out YamlNode? value)
    {
        if (map.Children.TryGetValue(new YamlScalarNode(key), out var node))
        {
            value = node;
            return true;
        }

        value = null;
        return false;
    }

    private static string? ScalarValue(YamlMappingNode map, string key)
        => TryGetChild(map, key, out var node) && node is YamlScalarNode scalar ? scalar.Value : null;

    private static string? SafetyText(SafetyState? state) => state switch
    {
        SafetyState.Yes => "true",
        SafetyState.No => "false",
        SafetyState.Unknown => "unknown",
        _ => null,
    };

    // The flat safety flags (recorded as "safetyFlags.<name>") are YAML booleans (true/false) or the
    // bare word "unknown" — they must stay plain so the index keeps the type the schema expects. Every
    // other mirrored field (displayName, version, gameDatabaseVersion) is a free-form string, so a
    // value that would otherwise resolve to a bool/null/number must be quoted to stay a string.
    private static string FormatScalar(string value, ScalarStyle style, string field) => style switch
    {
        ScalarStyle.SingleQuoted => "'" + value.Replace("'", "''") + "'",
        ScalarStyle.DoubleQuoted => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
        // Plain (or any other) style: emit the value verbatim only when it is safe as a plain
        // scalar. Otherwise single-quote it, so a colon, '#', leading dash/space, or (for a string
        // field) a value that would resolve to a bool/null/number can't break or change the index.
        _ => NeedsPlainQuoting(value, quoteScalarResolvable: !field.StartsWith("safetyFlags.", StringComparison.Ordinal))
            ? "'" + value.Replace("'", "''") + "'"
            : value,
    };

    private static bool NeedsPlainQuoting(string value, bool quoteScalarResolvable)
    {
        if (value.Length == 0)
            return true;
        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
            return true;
        if ("-?:,[]{}#&*!|>'\"%@`".IndexOf(value[0]) >= 0)
            return true;
        if (value.Contains(": ", StringComparison.Ordinal)
            || value.EndsWith(':')
            || value.Contains(" #", StringComparison.Ordinal))
            return true;
        foreach (var c in value)
        {
            if (c is '\n' or '\t' || char.IsControl(c))
                return true;
        }
        return quoteScalarResolvable && IsYamlBoolNullOrNumber(value);
    }

    private static bool IsYamlBoolNullOrNumber(string value)
    {
        if (value.ToLowerInvariant() is "true" or "false" or "yes" or "no" or "on" or "off" or "null" or "~")
            return true;
        return double.TryParse(
            value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out _);
    }

    private static string Show(string? value) => value is null ? "(unset)" : $"'{value}'";

    private static PatchDiagnostic Error(string code, string message, string? path = null)
        => new(PatchDiagnosticSeverity.Error, code, message, path);

    private static PatchDiagnostic Info(string code, string message, string? path = null)
        => new(PatchDiagnosticSeverity.Info, code, message, path);
}
