using System.Globalization;
using System.Text.RegularExpressions;

namespace PagoniaLand.Patcher;

public sealed partial class ManifestValidator
{
    private static readonly HashSet<string> KnownPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "core",
        "decorations1",
        "dlc1",
        "tools",
    };

    private static readonly HashSet<string> KnownTweakTypes = new(StringComparer.Ordinal)
    {
        "number",
        "integer",
        "boolean",
        "enum",
    };

    public IReadOnlyList<PatchDiagnostic> ValidateMod(LoadedMod mod)
    {
        var diagnostics = new List<PatchDiagnostic>();
        var manifest = mod.Manifest;

        RequireValue(manifest.PatchFormatVersion, DiagnosticCodes.MissingPatchFormatVersion, "patchFormatVersion is required.", diagnostics);
        RequireId(manifest.Id, "id", diagnostics);
        RequireValue(manifest.Name, DiagnosticCodes.MissingName, "name is required.", diagnostics);
        RequireValue(manifest.Version, DiagnosticCodes.MissingVersion, "version is required.", diagnostics);
        RequireValue(manifest.Author, DiagnosticCodes.MissingAuthor, "author is required.", diagnostics);
        RequireGameDatabaseVersion(manifest.GameDatabaseVersion, diagnostics);
        RequireValue(manifest.Description, DiagnosticCodes.MissingDescription, "description is required.", diagnostics);

        if (manifest.RequiredPackages.Count == 0)
        {
            diagnostics.Add(Error(DiagnosticCodes.MissingRequiredPackages, "requiredPackages must contain at least one package."));
        }

        foreach (var package in manifest.RequiredPackages.Concat(manifest.OptionalPackages))
        {
            if (!KnownPackages.Contains(package))
            {
                diagnostics.Add(Error(DiagnosticCodes.UnknownPackage, $"Unknown package '{package}'."));
            }
        }

        foreach (var patchPath in manifest.Patches)
        {
            ValidateRelativePath(patchPath, diagnostics);
        }

        foreach (var patchSet in manifest.PatchSets)
        {
            RequireValue(patchSet.Id, DiagnosticCodes.MissingPatchSetId, "patchSet id is required.", diagnostics);

            foreach (var package in patchSet.RequiresPackages)
            {
                if (!KnownPackages.Contains(package))
                {
                    diagnostics.Add(Error(DiagnosticCodes.UnknownPackage, $"Unknown package '{package}' in patchSet '{patchSet.Id}'."));
                }
            }

            foreach (var patchPath in patchSet.Patches)
            {
                ValidateRelativePath(patchPath, diagnostics);
            }
        }

        var operationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in mod.PatchFiles.SelectMany(file => file.PatchFile.Operations))
        {
            RequireValue(operation.Id, DiagnosticCodes.MissingOperationId, "Patch operation id is required.", diagnostics);

            if (!string.IsNullOrWhiteSpace(operation.Id) && !operationIds.Add(operation.Id))
            {
                diagnostics.Add(Error(DiagnosticCodes.DuplicateOperationId, $"Duplicate patch operation id '{operation.Id}'."));
            }

            ValidateArithmeticClamp(operation, diagnostics);
        }

        ValidateTweaks(mod, diagnostics);

        if (diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.ModManifestValid,
                $"Mod manifest '{manifest.Id}' passed basic validation."));
        }

        return diagnostics;
    }

    // A clampMin above clampMax can't be expressed as a JSON Schema cross-field rule and is an easy
    // slip — every result would be forced down to clampMax. Warn explicitly; literal placeholders are
    // skipped (they resolve at plan time, not here). Mirrors the tweak min>max lint.
    private static void ValidateArithmeticClamp(PatchOperation operation, List<PatchDiagnostic> diagnostics)
    {
        if (operation.ClampMin is null || operation.ClampMax is null)
        {
            return;
        }

        if (double.TryParse(operation.ClampMin, NumberStyles.Float, CultureInfo.InvariantCulture, out var min)
            && double.TryParse(operation.ClampMax, NumberStyles.Float, CultureInfo.InvariantCulture, out var max)
            && min > max)
        {
            diagnostics.Add(Warning(DiagnosticCodes.ClampMinGreaterThanMax,
                $"operation '{operation.Id}' has clampMin {min.ToString(CultureInfo.InvariantCulture)} greater than clampMax {max.ToString(CultureInfo.InvariantCulture)}."));
        }
    }

    private static void ValidateTweaks(LoadedMod mod, List<PatchDiagnostic> diagnostics)
    {
        var manifest = mod.Manifest;

        // Every id and alias claimed across all tweaks has to be unique within the mod, so a stored
        // override can never resolve ambiguously to two declarations. iMYA's experience is that a
        // changed/colliding ExposeID silently drops the user's value — we surface it at author time.
        var claimedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tweak in manifest.Tweaks)
        {
            var label = string.IsNullOrWhiteSpace(tweak.Id) ? "(unnamed)" : tweak.Id;

            if (string.IsNullOrWhiteSpace(tweak.Id))
            {
                diagnostics.Add(Error(DiagnosticCodes.InvalidTweakId, "tweak id is required."));
            }
            else if (!IsValidTweakId(tweak.Id))
            {
                diagnostics.Add(Error(DiagnosticCodes.InvalidTweakId, $"tweak id '{tweak.Id}' must use lowercase letters, numbers, or hyphens (max 40 characters)."));
            }
            else if (!claimedNames.Add(tweak.Id))
            {
                diagnostics.Add(Error(DiagnosticCodes.DuplicateTweakId, $"Duplicate tweak id '{tweak.Id}' (an id or alias is claimed more than once)."));
            }

            if (!KnownTweakTypes.Contains(tweak.Type))
            {
                diagnostics.Add(Error(DiagnosticCodes.InvalidTweakType, $"tweak '{label}' has unknown type '{tweak.Type}'. Expected number, integer, boolean, or enum."));
            }

            foreach (var alias in tweak.Aliases)
            {
                if (!IsValidTweakId(alias))
                {
                    diagnostics.Add(Error(DiagnosticCodes.InvalidTweakAlias, $"tweak '{label}' alias '{alias}' must use lowercase letters, numbers, or hyphens (max 40 characters)."));
                    continue;
                }

                if (!claimedNames.Add(alias))
                {
                    diagnostics.Add(Error(DiagnosticCodes.DuplicateTweakId, $"Duplicate tweak id '{alias}' (an id or alias is claimed more than once)."));
                }
            }

            switch (tweak.Type)
            {
                case "number":
                case "integer":
                    ValidateNumericTweakDefault(tweak, label, diagnostics);
                    // min > max can't be expressed as a JSON Schema cross-field rule, and it's an
                    // easy slip. Warn explicitly — the default-out-of-range error already fires too,
                    // but this names the real cause.
                    if (tweak.Min is { } min && tweak.Max is { } max && min > max)
                    {
                        diagnostics.Add(Warning(DiagnosticCodes.TweakMinGreaterThanMax,
                            $"tweak '{label}' has min {min.ToString(CultureInfo.InvariantCulture)} greater than max {max.ToString(CultureInfo.InvariantCulture)}."));
                    }

                    // A non-positive step makes the wizard's stepper meaningless (no movement, or
                    // backwards). The schema can't express "> 0" cleanly, so lint it here.
                    if (tweak.Step is { } step && step <= 0)
                    {
                        diagnostics.Add(Warning(DiagnosticCodes.TweakStepInvalid,
                            $"tweak '{label}' has a non-positive step {step.ToString(CultureInfo.InvariantCulture)}; step must be greater than zero."));
                    }

                    break;
                case "enum":
                    ValidateEnumTweakDefault(tweak, label, diagnostics);
                    break;
            }
        }

        LintTweakUsage(mod, diagnostics);
    }

    // Speculative author-lint over the patch operations that reference tweaks. These are warnings,
    // not errors: they flag likely typos that still parse + resolve. Both top-level `patches:` and
    // `patchSets:` operations (everything loaded into mod.PatchFiles) are scanned.
    private static void LintTweakUsage(LoadedMod mod, List<PatchDiagnostic> diagnostics)
    {
        var tweaks = mod.Manifest.Tweaks;
        if (tweaks.Count == 0)
        {
            return;
        }

        var resolver = new TweakResolver();
        var referencedIds = new HashSet<string>(StringComparer.Ordinal);
        var typeById = tweaks
            .Where(tweak => !string.IsNullOrWhiteSpace(tweak.Id))
            .GroupBy(tweak => tweak.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Type, StringComparer.Ordinal);

        foreach (var operation in mod.PatchFiles.SelectMany(file => file.PatchFile.Operations))
        {
            foreach (var field in new[] { operation.Value, operation.Factor, operation.Delta, operation.ExpectedOldValue, operation.Xml, operation.ExpectedOldXml })
            {
                foreach (var reference in resolver.ExtractReferences(field))
                {
                    referencedIds.Add(reference.TweakId);

                    // A ternary only makes sense on a boolean tweak; on a number/integer/enum it
                    // silently treats every non-"true" value as the false branch.
                    if (reference.IsTernary
                        && typeById.TryGetValue(reference.TweakId, out var type)
                        && type != "boolean")
                    {
                        diagnostics.Add(Warning(DiagnosticCodes.TweakTernaryOnNonBoolean,
                            $"tweak '{reference.TweakId}' is used with the boolean ternary form in operation '{operation.Id}', but it is declared as '{type}'."));
                    }
                }
            }
        }

        foreach (var tweak in tweaks)
        {
            if (string.IsNullOrWhiteSpace(tweak.Id))
            {
                continue;
            }

            // A {{ tweaks.* }} placeholder only resolves by the tweak's CURRENT id — aliases are
            // followed for stored override *values* (ResolveFromSelection), not for placeholders,
            // where an alias throws tweakUndeclared at plan time. So the unused-lint counts only
            // the current id; a tweak "used" solely via an alias placeholder is genuinely broken.
            var referenced = referencedIds.Contains(tweak.Id);
            if (!referenced)
            {
                diagnostics.Add(Warning(DiagnosticCodes.TweakDeclaredButUnused,
                    $"tweak '{tweak.Id}' is declared but never referenced by a {{{{ tweaks.{tweak.Id} }}}} placeholder."));
            }
        }
    }

    private static void ValidateNumericTweakDefault(TweakDeclaration tweak, string label, List<PatchDiagnostic> diagnostics)
    {
        if (!double.TryParse(tweak.Default, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            // The schema already enforces a numeric default; skip the range check when the literal
            // can't be parsed so we don't double-report what schema-validate already flags.
            return;
        }

        // An `integer` tweak's default must be a whole number — the float parse above happily
        // accepts "3.5", which would substitute a fractional value into an integer game field.
        if (tweak.Type == "integer" && value != Math.Floor(value))
        {
            diagnostics.Add(Error(DiagnosticCodes.TweakDefaultNotInteger,
                $"integer tweak '{label}' default {tweak.Default} is not a whole number."));
        }

        var belowMin = tweak.Min is { } min && value < min;
        var aboveMax = tweak.Max is { } max && value > max;
        if (belowMin || aboveMax)
        {
            var low = tweak.Min is { } mn ? mn.ToString(CultureInfo.InvariantCulture) : "-∞";
            var high = tweak.Max is { } mx ? mx.ToString(CultureInfo.InvariantCulture) : "+∞";
            diagnostics.Add(Error(DiagnosticCodes.TweakDefaultOutOfRange,
                $"tweak '{label}' default {tweak.Default} is outside the declared range [{low}, {high}]."));
        }
    }

    private static void ValidateEnumTweakDefault(TweakDeclaration tweak, string label, List<PatchDiagnostic> diagnostics)
    {
        if (tweak.Values.Count == 0)
        {
            diagnostics.Add(Error(DiagnosticCodes.TweakEnumMissingValues, $"enum tweak '{label}' must declare at least one value."));
            return;
        }

        if (!tweak.Values.Any(value => string.Equals(value.Value, tweak.Default, StringComparison.Ordinal)))
        {
            diagnostics.Add(Error(DiagnosticCodes.TweakDefaultNotEnumValue,
                $"tweak '{label}' default '{tweak.Default}' is not one of its declared values."));
        }
    }

    private static bool IsValidTweakId(string value)
        => !string.IsNullOrEmpty(value) && value.Length <= 40 && TweakIdPattern().IsMatch(value);

    private static void RequireId(string value, string fieldName, List<PatchDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(Error(DiagnosticCodes.MissingId, $"{fieldName} is required."));
            return;
        }

        if (!ModIdPattern().IsMatch(value))
        {
            diagnostics.Add(Error(DiagnosticCodes.InvalidId, $"{fieldName} '{value}' must use lowercase letters, numbers, dots, underscores, or hyphens."));
        }
    }

    private static void RequireGameDatabaseVersion(string value, List<PatchDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(Error(DiagnosticCodes.MissingGameDatabaseVersion, "gameDatabaseVersion is required."));
            return;
        }

        if (!GameDatabaseVersionPattern().IsMatch(value))
        {
            diagnostics.Add(Error(DiagnosticCodes.InvalidGameDatabaseVersion, $"gameDatabaseVersion '{value}' must look like 1.3.0-11694+192849."));
        }
    }

    private static void RequireValue(string value, string code, string message, List<PatchDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(Error(code, message));
        }
    }

    private static void ValidateRelativePath(string path, List<PatchDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            diagnostics.Add(Error(DiagnosticCodes.EmptyPatchPath, "Patch paths must not be empty."));
            return;
        }

        if (Path.IsPathRooted(path) || path.Contains("..", StringComparison.Ordinal) || path.Contains('\\', StringComparison.Ordinal))
        {
            diagnostics.Add(Error(DiagnosticCodes.UnsafePatchPath, $"Patch path '{path}' must be a safe relative path using forward slashes."));
        }
    }

    private static PatchDiagnostic Error(string code, string message)
        => new(PatchDiagnosticSeverity.Error, code, message);

    private static PatchDiagnostic Warning(string code, string message)
        => new(PatchDiagnosticSeverity.Warning, code, message);

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*[a-z0-9]$")]
    private static partial Regex ModIdPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$")]
    private static partial Regex TweakIdPattern();

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+-[0-9]+\\+[0-9]+$")]
    private static partial Regex GameDatabaseVersionPattern();
}
