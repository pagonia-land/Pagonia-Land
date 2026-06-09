using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace PagoniaLand.Patcher;

/// <summary>
/// Validates mod / patch / collection / lockfile YAML files against the canonical JSON Schemas
/// shipped under <c>schemas/mod-patches/</c>. The schemas are embedded into this assembly at build
/// time (see <c>PagoniaLand.Patcher.Core.csproj</c>'s <c>&lt;EmbeddedResource&gt;</c> block) so the
/// AOT-published binary doesn't need to find the repo's <c>schemas/</c> folder at runtime.
///
/// <para>
/// This is a second validation layer on top of <see cref="ManifestValidator"/>. <see cref="ManifestValidator"/>
/// expresses the patcher's runtime expectations in C# code; the JSON Schemas express the public
/// contract third-party tools (mod managers, IDE plugins, future EE tooling) read. Running both
/// makes the two stay in sync — any divergence shows up as a failed schema-validate run.
/// </para>
/// </summary>
public sealed class SchemaValidator
{
    private const string ResourcePrefix = "PagoniaLand.Patcher.Schemas.";

    private readonly JsonSchema _modSchema;
    private readonly JsonSchema _patchFileSchema;
    private readonly JsonSchema _collectionSchema;
    private readonly JsonSchema _collectionLockSchema;
    private readonly JsonSchema _repoIndexSchema;
    private readonly JsonSchema _catalogSchema;

    public SchemaValidator()
    {
        _modSchema = LoadEmbeddedSchema("mod.schema.json");
        _patchFileSchema = LoadEmbeddedSchema("patch-file.schema.json");
        _collectionSchema = LoadEmbeddedSchema("collection.schema.json");
        _collectionLockSchema = LoadEmbeddedSchema("collection-lock.schema.json");
        _repoIndexSchema = LoadEmbeddedSchema("repo-index.schema.json");
        _catalogSchema = LoadEmbeddedSchema("catalog.schema.json");
    }

    /// <summary>
    /// Validate a mod's <c>mod.yaml</c> and every patch file it references.
    /// Returns one info-level <see cref="DiagnosticCodes.SchemaValidationOk"/> per validated file
    /// when the file conforms, and one error-level <see cref="DiagnosticCodes.SchemaValidationFailed"/>
    /// per schema violation otherwise.
    /// </summary>
    public IReadOnlyList<PatchDiagnostic> ValidateMod(string modDirectory)
    {
        var diagnostics = new List<PatchDiagnostic>();
        var modYamlPath = Path.Combine(modDirectory, "mod.yaml");

        if (!File.Exists(modYamlPath))
        {
            diagnostics.Add(Error(DiagnosticCodes.SchemaValidationFailed, $"mod.yaml not found under '{modDirectory}'.", modYamlPath));
            return diagnostics;
        }

        ValidateFile(modYamlPath, _modSchema, "mod.yaml", diagnostics);

        // Discover referenced patch files by re-parsing the mod manifest minimally. We deliberately
        // do not depend on ManifestReader / LoadedMod here so this validator can run even when the
        // manifest's shape is invalid — the schema check is meant to surface those problems clearly.
        foreach (var patchPath in EnumeratePatchPaths(modYamlPath))
        {
            var resolved = Path.Combine(modDirectory, patchPath);
            if (!File.Exists(resolved))
            {
                diagnostics.Add(Error(DiagnosticCodes.SchemaValidationFailed, $"Patch file '{patchPath}' referenced by mod.yaml not found.", resolved));
                continue;
            }

            ValidateFile(resolved, _patchFileSchema, patchPath, diagnostics);
        }

        return diagnostics;
    }

    /// <summary>
    /// Validate a single collection YAML file against <c>collection.schema.json</c>.
    /// </summary>
    public IReadOnlyList<PatchDiagnostic> ValidateCollection(string collectionYamlPath)
    {
        var diagnostics = new List<PatchDiagnostic>();
        if (!File.Exists(collectionYamlPath))
        {
            diagnostics.Add(Error(DiagnosticCodes.SchemaValidationFailed, "Collection file not found.", collectionYamlPath));
            return diagnostics;
        }

        ValidateFile(collectionYamlPath, _collectionSchema, Path.GetFileName(collectionYamlPath), diagnostics);
        return diagnostics;
    }

    /// <summary>
    /// Validate a single collection-lock YAML file against <c>collection-lock.schema.json</c>.
    /// </summary>
    public IReadOnlyList<PatchDiagnostic> ValidateCollectionLock(string lockYamlPath)
    {
        var diagnostics = new List<PatchDiagnostic>();
        if (!File.Exists(lockYamlPath))
        {
            diagnostics.Add(Error(DiagnosticCodes.SchemaValidationFailed, "Lockfile not found.", lockYamlPath));
            return diagnostics;
        }

        ValidateFile(lockYamlPath, _collectionLockSchema, Path.GetFileName(lockYamlPath), diagnostics);
        return diagnostics;
    }

    /// <summary>
    /// Validate a repository <c>index.yaml</c> file against <c>repo-index.schema.json</c>.
    /// Used by mod authors publishing via Git to verify their catalog stays
    /// well-formed before the manager consumes it.
    /// </summary>
    public IReadOnlyList<PatchDiagnostic> ValidateRepoIndex(string repoIndexPath)
    {
        var diagnostics = new List<PatchDiagnostic>();
        if (!File.Exists(repoIndexPath))
        {
            diagnostics.Add(Error(DiagnosticCodes.SchemaValidationFailed, "Repo index file not found.", repoIndexPath));
            return diagnostics;
        }

        ValidateFile(repoIndexPath, _repoIndexSchema, Path.GetFileName(repoIndexPath), diagnostics);
        return diagnostics;
    }

    /// <summary>
    /// Validate a <c>catalog.yaml</c> against <c>catalog.schema.json</c>. A
    /// catalog is a curated list of mod-distribution repos plus optional
    /// federated references to other catalogs; the manager aggregates across
    /// every subscribed catalog with cycle / depth / dedup protection.
    /// </summary>
    public IReadOnlyList<PatchDiagnostic> ValidateCatalog(string catalogPath)
    {
        var diagnostics = new List<PatchDiagnostic>();
        if (!File.Exists(catalogPath))
        {
            diagnostics.Add(Error(DiagnosticCodes.SchemaValidationFailed, "Catalog file not found.", catalogPath));
            return diagnostics;
        }

        ValidateFile(catalogPath, _catalogSchema, Path.GetFileName(catalogPath), diagnostics);
        return diagnostics;
    }

    private void ValidateFile(string filePath, JsonSchema schema, string displayPath, List<PatchDiagnostic> diagnostics)
    {
        string yamlText;
        try
        {
            yamlText = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(DiagnosticCodes.SchemaValidationFailed, $"Cannot read file: {ex.Message}", filePath));
            return;
        }

        JsonNode? node;
        try
        {
            node = YamlToJsonNode(yamlText);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(DiagnosticCodes.SchemaValidationFailed, $"YAML parse failed: {ex.Message}", filePath));
            return;
        }

        EvaluationResults results;
        try
        {
            // JsonSchema.Net 9 evaluates a JsonElement (was JsonNode in 7.x). Round-trip the
            // typed JsonNode we built from YAML through its JSON text — ToJsonString() preserves
            // the bool / int / number / null typing resolved above, and both calls are
            // reflection-free so the AOT binary stays clean. The document stays alive across the
            // Evaluate call; we only read IsValid / Errors / Details afterwards, never the input.
            using var doc = JsonDocument.Parse(node is null ? "null" : node.ToJsonString());
            results = schema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(DiagnosticCodes.SchemaValidationFailed, $"Schema evaluation failed: {ex.Message}", filePath));
            return;
        }

        if (results.IsValid)
        {
            diagnostics.Add(Info(DiagnosticCodes.SchemaValidationOk, $"{displayPath} conforms to schema.", filePath));
            return;
        }

        // Walk every node in the result tree and emit one diagnostic per recorded
        // error, deduped by (location, keyword, message).
        //
        // We deliberately do NOT prune valid subtrees here, even though that
        // looks like a reasonable optimisation. JsonSchema.Net's hierarchical
        // output reports the `not` keyword's child as IsValid=true exactly when
        // the inner schema matched — which is the case the parent `not` is
        // rejecting. Pruning by IsValid skips those child nodes and loses the
        // real failure messages. A schema like `{not: {pattern: "X"}}` against
        // a string that matches X otherwise validates to "Failed" at the top
        // level with zero error messages collected. Visit-all-and-emit-on-error
        // is the simplest fix that handles both `not` and the original
        // oneOf/anyOf concern (those branches just have no errors to emit).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        CollectAllErrors(results, displayPath, filePath, seen, diagnostics);
    }

    private static void CollectAllErrors(
        EvaluationResults result,
        string displayPath,
        string filePath,
        HashSet<string> seen,
        List<PatchDiagnostic> diagnostics)
    {
        if (result.Errors is { Count: > 0 })
        {
            var instanceLocation = result.InstanceLocation.ToString();
            var locationHint = string.IsNullOrEmpty(instanceLocation) ? "(root)" : instanceLocation;

            foreach (var (keyword, message) in result.Errors)
            {
                var key = $"{locationHint}|{keyword}|{message}";
                if (!seen.Add(key))
                {
                    continue;
                }

                diagnostics.Add(Error(
                    DiagnosticCodes.SchemaValidationFailed,
                    $"{displayPath} {locationHint}: {message} [{keyword}]",
                    filePath));
            }
        }

        foreach (var child in result.Details ?? [])
        {
            CollectAllErrors(child, displayPath, filePath, seen, diagnostics);
        }
    }

    private static IEnumerable<string> EnumeratePatchPaths(string modYamlPath)
    {
        // Lightweight pre-parse: load the YAML as a generic object so we can pluck patches: and
        // patchSets[].patches even if other fields are malformed.
        object? root;
        try
        {
            var yamlText = File.ReadAllText(modYamlPath);
            root = new DeserializerBuilder().Build().Deserialize<object?>(yamlText);
        }
        catch
        {
            yield break;
        }

        if (root is not IDictionary<object, object> map)
        {
            yield break;
        }

        if (map.TryGetValue("patches", out var patches) && patches is IList<object> patchList)
        {
            foreach (var item in patchList)
            {
                if (item is string s && !string.IsNullOrWhiteSpace(s))
                {
                    yield return s;
                }
            }
        }

        if (map.TryGetValue("patchSets", out var patchSets) && patchSets is IList<object> patchSetList)
        {
            foreach (var patchSet in patchSetList)
            {
                if (patchSet is not IDictionary<object, object> patchSetMap)
                {
                    continue;
                }

                if (!patchSetMap.TryGetValue("patches", out var nestedPatches) || nestedPatches is not IList<object> nestedList)
                {
                    continue;
                }

                foreach (var item in nestedList)
                {
                    if (item is string s && !string.IsNullOrWhiteSpace(s))
                    {
                        yield return s;
                    }
                }
            }
        }
    }

    private static JsonNode? YamlToJsonNode(string yamlText)
    {
        // We parse the YAML into the representation model (not the deserializer) because the
        // generic <c>Deserialize&lt;object?&gt;</c> path collapses every plain scalar to a string —
        // <c>requiresNewGame: false</c> becomes the string "false" and the schema rejects it as
        // not-a-boolean. By walking the YamlStream ourselves we can apply YAML 1.2 core-schema
        // type resolution (the same rules the JSON encoding of YAML uses) and produce a JsonNode
        // tree with proper boolean / integer / number / null typing.
        var yamlStream = new YamlStream();
        yamlStream.Load(new StringReader(yamlText));
        if (yamlStream.Documents.Count == 0)
        {
            return null;
        }

        return ConvertYamlNode(yamlStream.Documents[0].RootNode);
    }

    private static JsonNode? ConvertYamlNode(YamlNode node) => node switch
    {
        YamlScalarNode scalar => ConvertYamlScalar(scalar),
        YamlSequenceNode sequence => ConvertYamlSequence(sequence),
        YamlMappingNode mapping => ConvertYamlMapping(mapping),
        _ => null,
    };

    private static JsonNode? ConvertYamlScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;
        if (value is null)
        {
            return null;
        }

        // Single- or double-quoted scalars are explicitly strings; never type-infer them.
        if (scalar.Style == ScalarStyle.SingleQuoted || scalar.Style == ScalarStyle.DoubleQuoted)
        {
            return JsonValue.Create(value);
        }

        // Explicit tags from the YAML core schema win over implicit inference. Non-specific tags
        // ("?" and "!") have no .Value and throw if read — guard with IsEmpty.
        if (!scalar.Tag.IsEmpty && !scalar.Tag.IsNonSpecific)
        {
            switch (scalar.Tag.Value)
            {
                case "tag:yaml.org,2002:null":
                    return null;
                case "tag:yaml.org,2002:bool":
                    return JsonValue.Create(string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
                case "tag:yaml.org,2002:int":
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var taggedInt))
                    {
                        return JsonValue.Create(taggedInt);
                    }
                    break;
                case "tag:yaml.org,2002:float":
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var taggedFloat))
                    {
                        return JsonValue.Create(taggedFloat);
                    }
                    break;
                case "tag:yaml.org,2002:str":
                    return JsonValue.Create(value);
            }
        }

        // Plain scalar — apply YAML 1.2 core schema resolution: null / bool / int / float / str.
        if (value.Length == 0 || value == "~" || value.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return JsonValue.Create(true);
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return JsonValue.Create(false);
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue))
        {
            return JsonValue.Create(integerValue);
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue)
            && !double.IsNaN(doubleValue)
            && !double.IsInfinity(doubleValue))
        {
            return JsonValue.Create(doubleValue);
        }

        return JsonValue.Create(value);
    }

    private static JsonArray ConvertYamlSequence(YamlSequenceNode sequence)
    {
        var array = new JsonArray();
        foreach (var child in sequence.Children)
        {
            array.Add(ConvertYamlNode(child));
        }
        return array;
    }

    private static JsonObject ConvertYamlMapping(YamlMappingNode mapping)
    {
        var obj = new JsonObject();
        foreach (var (keyNode, valueNode) in mapping.Children)
        {
            // JSON object keys must be strings; non-scalar keys are rare in YAML config files
            // and are stringified for schema purposes.
            var key = keyNode is YamlScalarNode keyScalar ? keyScalar.Value ?? string.Empty : keyNode.ToString();
            obj[key] = ConvertYamlNode(valueNode);
        }
        return obj;
    }

    private static JsonSchema LoadEmbeddedSchema(string fileName)
    {
        var assembly = typeof(SchemaValidator).Assembly;
        var resourceName = ResourcePrefix + fileName;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded schema '{resourceName}' not found. Check the EmbeddedResource entries in PagoniaLand.Patcher.Core.csproj.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var schemaText = reader.ReadToEnd();
        // JsonSchema.Net 9 registers each evaluated schema by $id and refuses to overwrite it with
        // a different document. A second SchemaValidator instance re-parsing these schemas would
        // otherwise throw "Overwriting registered schemas is not permitted". Build into a fresh
        // local registry (which still falls back to the global one) so each parse is isolated;
        // these schemas are self-contained (only internal #/$defs refs).
        return JsonSchema.FromText(schemaText, new BuildOptions { SchemaRegistry = new SchemaRegistry() });
    }

    private static PatchDiagnostic Error(string code, string message, string? path = null) =>
        new(PatchDiagnosticSeverity.Error, code, message, path);

    private static PatchDiagnostic Info(string code, string message, string? path = null) =>
        new(PatchDiagnosticSeverity.Info, code, message, path);
}
