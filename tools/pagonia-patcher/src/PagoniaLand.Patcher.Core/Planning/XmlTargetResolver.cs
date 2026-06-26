using System.Xml.Linq;

namespace PagoniaLand.Patcher;

public sealed class XmlTargetResolver
{
    public TargetResolveResult ResolveReplaceValue(string gameRoot, PatchOperation operation)
    {
        var resolution = ResolveTargetNode(gameRoot, operation);
        if (resolution.Failure is not null)
        {
            return resolution.Failure;
        }

        var diagnostics = new List<PatchDiagnostic>(resolution.Diagnostics);
        var target = operation.Target;
        var fullPath = resolution.FullPath;
        var valueElement = resolution.TargetNode!;

        // Guard against pointing replaceValue at a container element. XElement.Value
        // on an element with children returns the concatenated descendant text, and
        // the setter would replace every child with a single text node — silent,
        // hard-to-debug corruption. Require a leaf; direct the author to replaceNode.
        if (valueElement.HasElements)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.ReplaceValueOnContainer,
                $"replaceValue target '{target.Path}' has child elements — setting its value would delete them. Use replaceNode to replace an element that has children.",
                fullPath));
        }

        var oldValue = valueElement.Value;

        if (!string.Equals(oldValue, operation.ExpectedOldValue, StringComparison.Ordinal))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.ExpectedOldValueMismatch,
                $"Expected old value '{operation.ExpectedOldValue}', but found '{oldValue}'.",
                fullPath));
        }

        var write = new PatchWrite(
            operation.Id,
            PatchOperationTypes.ReplaceValue,
            target.File,
            target.EntityGuid,
            target.EntityName,
            target.Component,
            target.Path,
            null,
            oldValue,
            operation.Value ?? string.Empty);

        diagnostics.Add(new PatchDiagnostic(
            PatchDiagnosticSeverity.Info,
            DiagnosticCodes.TargetResolved,
            $"Resolved {target.EntityName}/{target.Component}/{target.Path}: {oldValue} -> {operation.Value}",
            fullPath));

        return new TargetResolveResult(write, diagnostics);
    }

    public TargetResolveResult ResolveMultiplyValue(string gameRoot, PatchOperation operation)
        => ResolveArithmetic(gameRoot, operation, PatchOperationTypes.MultiplyValue, operation.Factor, "factor");

    public TargetResolveResult ResolveAddValue(string gameRoot, PatchOperation operation)
        => ResolveArithmetic(gameRoot, operation, PatchOperationTypes.AddValue, operation.Delta, "delta");

    // Shared body for multiplyValue/addValue. The new value is computed at plan time from the
    // declared expectedOldValue and the operand (factor/delta) — the file read only enforces the
    // same expectedOldValue drift-guard replaceValue uses, so the result is deterministic and never
    // depends on a prior mod's write. The math itself lives in ArithmeticPatchOps so a preview (the
    // manager's tweak wizard) computes identical results; the write is stored as a literal new value
    // so the apply step reuses ApplyReplaceValue verbatim.
    private TargetResolveResult ResolveArithmetic(
        string gameRoot,
        PatchOperation operation,
        string operationType,
        string? operand,
        string operandField)
    {
        if (string.IsNullOrWhiteSpace(operand))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires '{operandField}' for {operationType}."));
        }

        var resolution = ResolveTargetNode(gameRoot, operation);
        if (resolution.Failure is not null)
        {
            return resolution.Failure;
        }

        var diagnostics = new List<PatchDiagnostic>(resolution.Diagnostics);
        var target = operation.Target;
        var fullPath = resolution.FullPath;
        var valueElement = resolution.TargetNode!;

        // Same leaf-only guard as replaceValue: XElement.Value on a container concatenates descendant
        // text, and writing it back would delete the children.
        if (valueElement.HasElements)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.ReplaceValueOnContainer,
                $"{operationType} target '{target.Path}' has child elements — it must point at a leaf value.",
                fullPath));
        }

        var oldValue = valueElement.Value;

        if (!string.Equals(oldValue, operation.ExpectedOldValue, StringComparison.Ordinal))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.ExpectedOldValueMismatch,
                $"Expected old value '{operation.ExpectedOldValue}', but found '{oldValue}'.",
                fullPath));
        }

        if (!ArithmeticPatchOps.TryParse(oldValue, out var oldNumber))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.ArithmeticTargetNotNumeric,
                $"{operationType} target value '{oldValue}' is not numeric.",
                fullPath));
        }

        if (!ArithmeticPatchOps.TryParse(operand, out var operandNumber))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.ArithmeticOperandNotNumeric,
                $"{operationType} {operandField} '{operand}' is not numeric.",
                fullPath));
        }

        double? clampMin = null;
        if (operation.ClampMin is { } rawMin)
        {
            if (!ArithmeticPatchOps.TryParse(rawMin, out var parsedMin))
            {
                return TargetResolveResult.Failed(Error(
                    DiagnosticCodes.ArithmeticOperandNotNumeric,
                    $"{operationType} clampMin '{rawMin}' is not numeric.",
                    fullPath));
            }
            clampMin = parsedMin;
        }

        double? clampMax = null;
        if (operation.ClampMax is { } rawMax)
        {
            if (!ArithmeticPatchOps.TryParse(rawMax, out var parsedMax))
            {
                return TargetResolveResult.Failed(Error(
                    DiagnosticCodes.ArithmeticOperandNotNumeric,
                    $"{operationType} clampMax '{rawMax}' is not numeric.",
                    fullPath));
            }
            clampMax = parsedMax;
        }

        if (!ArithmeticPatchOps.ResultIsFinite(operationType, oldNumber, operandNumber))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.ArithmeticResultNotFinite,
                $"{operationType} on target value '{oldValue}' with {operandField} '{operand}' overflows to a non-finite value.",
                fullPath));
        }

        var newValue = ArithmeticPatchOps.Compute(
            operationType, oldNumber, operandNumber, operation.Rounding, clampMin, clampMax, out var clamped);

        var write = new PatchWrite(
            operation.Id,
            operationType,
            target.File,
            target.EntityGuid,
            target.EntityName,
            target.Component,
            target.Path,
            null,
            oldValue,
            newValue);

        diagnostics.Add(new PatchDiagnostic(
            PatchDiagnosticSeverity.Info,
            DiagnosticCodes.TargetResolved,
            $"Resolved {target.EntityName}/{target.Component}/{target.Path}: {oldValue} -> {newValue}",
            fullPath));

        if (clamped)
        {
            var unclamped = ArithmeticPatchOps.Compute(
                operationType, oldNumber, operandNumber, operation.Rounding, null, null, out _);
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.ArithmeticResultClamped,
                $"{operationType} result {unclamped} was clamped to {newValue} for operation '{operation.Id}'.",
                fullPath));
        }

        return new TargetResolveResult(write, diagnostics);
    }

    public TargetResolveResult ResolveReplaceAttribute(string gameRoot, PatchOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.Attribute))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires 'attribute' for {PatchOperationTypes.ReplaceAttribute}."));
        }

        var resolution = ResolveTargetNode(gameRoot, operation);
        if (resolution.Failure is not null)
        {
            return resolution.Failure;
        }

        var diagnostics = new List<PatchDiagnostic>(resolution.Diagnostics);
        var target = operation.Target;
        var fullPath = resolution.FullPath;
        var targetNode = resolution.TargetNode!;
        var attribute = targetNode.Attribute(operation.Attribute);

        if (attribute is null)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetAttributeMissing,
                $"Attribute '{operation.Attribute}' was not found on '{target.Component}/{target.Path}'.",
                fullPath));
        }

        var oldValue = attribute.Value;

        if (!string.Equals(oldValue, operation.ExpectedOldValue, StringComparison.Ordinal))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.ExpectedOldValueMismatch,
                $"Expected old attribute value '{operation.ExpectedOldValue}', but found '{oldValue}'.",
                fullPath));
        }

        var write = new PatchWrite(
            operation.Id,
            PatchOperationTypes.ReplaceAttribute,
            target.File,
            target.EntityGuid,
            target.EntityName,
            target.Component,
            target.Path,
            operation.Attribute,
            oldValue,
            operation.Value ?? string.Empty);

        diagnostics.Add(new PatchDiagnostic(
            PatchDiagnosticSeverity.Info,
            DiagnosticCodes.TargetResolved,
            $"Resolved {target.EntityName}/{target.Component}/{target.Path}@{operation.Attribute}: {oldValue} -> {operation.Value}",
            fullPath));

        return new TargetResolveResult(write, diagnostics);
    }

    public TargetResolveResult ResolveReplaceNode(string gameRoot, PatchOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.Xml))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires 'xml' for {PatchOperationTypes.ReplaceNode}."));
        }

        XElement parsedReplacement;
        try
        {
            parsedReplacement = XElement.Parse(operation.Xml);
        }
        catch (System.Xml.XmlException exception)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.InvalidPatchOperationXml,
                $"Operation '{operation.Id}' has invalid replacement XML: {exception.Message}."));
        }

        var resolution = ResolveTargetNode(gameRoot, operation);
        if (resolution.Failure is not null)
        {
            return resolution.Failure;
        }

        var diagnostics = new List<PatchDiagnostic>(resolution.Diagnostics);
        var target = operation.Target;
        var fullPath = resolution.FullPath;
        var targetNode = resolution.TargetNode!;
        var currentXml = targetNode.ToString(SaveOptions.DisableFormatting);
        var newXml = parsedReplacement.ToString(SaveOptions.DisableFormatting);

        if (!string.IsNullOrWhiteSpace(operation.ExpectedOldXml))
        {
            var normalisedExpected = NormaliseXml(operation.ExpectedOldXml);
            if (normalisedExpected is null)
            {
                return TargetResolveResult.Failed(Error(
                    DiagnosticCodes.InvalidPatchOperationXml,
                    $"Operation '{operation.Id}' has invalid 'expectedOldXml'."));
            }

            if (!string.Equals(currentXml, normalisedExpected, StringComparison.Ordinal))
            {
                return TargetResolveResult.Failed(Error(
                    DiagnosticCodes.ExpectedOldXmlMismatch,
                    $"Expected old XML did not match the current node at '{target.Component}/{target.Path}'.",
                    fullPath));
            }
        }

        var write = new PatchWrite(
            operation.Id,
            PatchOperationTypes.ReplaceNode,
            target.File,
            target.EntityGuid,
            target.EntityName,
            target.Component,
            target.Path,
            null,
            currentXml,
            newXml);

        diagnostics.Add(new PatchDiagnostic(
            PatchDiagnosticSeverity.Info,
            DiagnosticCodes.TargetResolved,
            $"Resolved {target.EntityName}/{target.Component}/{target.Path}: <node> -> <node>",
            fullPath));

        return new TargetResolveResult(write, diagnostics);
    }

    public TargetResolveResult ResolveAddListItem(string gameRoot, PatchOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.Xml))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires 'xml' for {PatchOperationTypes.AddListItem}."));
        }

        XElement parsedItem;
        try
        {
            parsedItem = XElement.Parse(operation.Xml);
        }
        catch (System.Xml.XmlException exception)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.InvalidPatchOperationXml,
                $"Operation '{operation.Id}' has invalid list item XML: {exception.Message}."));
        }

        var resolution = ResolveTargetNode(gameRoot, operation);
        if (resolution.Failure is not null)
        {
            return resolution.Failure;
        }

        var diagnostics = new List<PatchDiagnostic>(resolution.Diagnostics);
        var target = operation.Target;
        var fullPath = resolution.FullPath;
        var newXml = parsedItem.ToString(SaveOptions.DisableFormatting);

        var write = new PatchWrite(
            operation.Id,
            PatchOperationTypes.AddListItem,
            target.File,
            target.EntityGuid,
            target.EntityName,
            target.Component,
            target.Path,
            null,
            string.Empty,
            newXml);

        diagnostics.Add(new PatchDiagnostic(
            PatchDiagnosticSeverity.Info,
            DiagnosticCodes.TargetResolved,
            $"Resolved {target.EntityName}/{target.Component}/{target.Path}: + <item>",
            fullPath));

        return new TargetResolveResult(write, diagnostics);
    }

    public TargetResolveResult ResolveRemoveListItem(string gameRoot, PatchOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.ExpectedOldXml))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires 'expectedOldXml' for {PatchOperationTypes.RemoveListItem}."));
        }

        var normalisedExpected = NormaliseXml(operation.ExpectedOldXml);
        if (normalisedExpected is null)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.InvalidPatchOperationXml,
                $"Operation '{operation.Id}' has invalid 'expectedOldXml'."));
        }

        var resolution = ResolveTargetNode(gameRoot, operation);
        if (resolution.Failure is not null)
        {
            return resolution.Failure;
        }

        var diagnostics = new List<PatchDiagnostic>(resolution.Diagnostics);
        var target = operation.Target;
        var fullPath = resolution.FullPath;
        var container = resolution.TargetNode!;
        var matchingChild = container.Elements()
            .FirstOrDefault(child => string.Equals(
                child.ToString(SaveOptions.DisableFormatting),
                normalisedExpected,
                StringComparison.Ordinal));

        if (matchingChild is null)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetListItemMissing,
                $"No child of '{target.Component}/{target.Path}' matched the expectedOldXml for '{operation.Id}'.",
                fullPath));
        }

        var write = new PatchWrite(
            operation.Id,
            PatchOperationTypes.RemoveListItem,
            target.File,
            target.EntityGuid,
            target.EntityName,
            target.Component,
            target.Path,
            null,
            normalisedExpected,
            string.Empty);

        diagnostics.Add(new PatchDiagnostic(
            PatchDiagnosticSeverity.Info,
            DiagnosticCodes.TargetResolved,
            $"Resolved {target.EntityName}/{target.Component}/{target.Path}: - <item>",
            fullPath));

        return new TargetResolveResult(write, diagnostics);
    }

    public TargetResolveResult ResolveAddEntity(string gameRoot, PatchOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.Xml))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires 'xml' for {PatchOperationTypes.AddEntity}."));
        }

        XElement parsedEntity;
        try
        {
            parsedEntity = XElement.Parse(operation.Xml);
        }
        catch (System.Xml.XmlException exception)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.InvalidPatchOperationXml,
                $"Operation '{operation.Id}' has invalid entity XML: {exception.Message}."));
        }

        if (!string.Equals(parsedEntity.Name.LocalName, "Entity", StringComparison.Ordinal))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.InvalidPatchOperationEntity,
                $"Operation '{operation.Id}' must add a root <Entity> element, found <{parsedEntity.Name.LocalName}>."));
        }

        var newGuid = (string?)parsedEntity.Attribute("Guid");
        var newName = (string?)parsedEntity.Attribute("Name");

        if (string.IsNullOrWhiteSpace(newGuid) || string.IsNullOrWhiteSpace(newName))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.InvalidPatchOperationEntity,
                $"Operation '{operation.Id}' adds an entity without a Name and Guid attribute."));
        }

        var documentResult = LoadTargetDocument(gameRoot, operation);
        if (documentResult.Failure is not null)
        {
            return documentResult.Failure;
        }

        var target = operation.Target;
        var fullPath = documentResult.FullPath;
        var document = documentResult.Document!;

        if (FindEntity(document, newGuid) is not null)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetEntityAlreadyExists,
                $"An entity with Guid '{newGuid}' already exists in '{target.File}'.",
                fullPath));
        }

        if (string.IsNullOrWhiteSpace(target.EntityName))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires target.entityName as the group name for {PatchOperationTypes.AddEntity}."));
        }

        var group = document.Descendants("Group")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("Name"), target.EntityName, StringComparison.Ordinal));

        if (group?.Element("Entities") is null)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetEntityGroupMissing,
                $"Entity group '{target.EntityName}' was not found in '{target.File}'.",
                fullPath));
        }

        var normalisedXml = parsedEntity.ToString(SaveOptions.DisableFormatting);

        var diagnostics = new List<PatchDiagnostic>
        {
            new(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.TargetResolved,
                $"Resolved addEntity '{newName}' ({newGuid}) into group '{target.EntityName}'.",
                fullPath),
        };

        // For addEntity we store the group name in the Component slot so the applier can find the
        // <Entities> container even after the plan crosses the planner/applier boundary. The new
        // entity's name lives in EntityName for reports.
        var write = new PatchWrite(
            operation.Id,
            PatchOperationTypes.AddEntity,
            target.File,
            newGuid,
            newName,
            target.EntityName,
            string.Empty,
            null,
            string.Empty,
            normalisedXml);

        return new TargetResolveResult(write, diagnostics);
    }

    public TargetResolveResult ResolveRemoveEntity(string gameRoot, PatchOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.ExpectedOldXml))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires 'expectedOldXml' for {PatchOperationTypes.RemoveEntity}."));
        }

        var normalisedExpected = NormaliseXml(operation.ExpectedOldXml);
        if (normalisedExpected is null)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.InvalidPatchOperationXml,
                $"Operation '{operation.Id}' has invalid 'expectedOldXml'."));
        }

        var documentResult = LoadTargetDocument(gameRoot, operation);
        if (documentResult.Failure is not null)
        {
            return documentResult.Failure;
        }

        var target = operation.Target;
        var fullPath = documentResult.FullPath;
        var document = documentResult.Document!;

        if (string.IsNullOrWhiteSpace(target.EntityGuid))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires target.entityGuid for {PatchOperationTypes.RemoveEntity}."));
        }

        var entity = FindEntity(document, target.EntityGuid);
        if (entity is null)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetEntityMissing,
                $"Target entity GUID '{target.EntityGuid}' was not found.",
                fullPath));
        }

        var currentXml = entity.ToString(SaveOptions.DisableFormatting);
        if (!string.Equals(currentXml, normalisedExpected, StringComparison.Ordinal))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.ExpectedOldXmlMismatch,
                $"Expected old XML did not match the current entity '{target.EntityGuid}'.",
                fullPath));
        }

        var diagnostics = new List<PatchDiagnostic>
        {
            new(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.TargetResolved,
                $"Resolved removeEntity '{target.EntityGuid}' from '{target.File}'.",
                fullPath),
        };

        var write = new PatchWrite(
            operation.Id,
            PatchOperationTypes.RemoveEntity,
            target.File,
            target.EntityGuid,
            target.EntityName,
            string.Empty,
            string.Empty,
            null,
            normalisedExpected,
            string.Empty);

        return new TargetResolveResult(write, diagnostics);
    }

    public TargetResolveResult ResolveMergeComponent(string gameRoot, PatchOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.Xml))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires 'xml' for {PatchOperationTypes.MergeComponent}."));
        }

        XElement parsedComponent;
        try
        {
            parsedComponent = XElement.Parse(operation.Xml);
        }
        catch (System.Xml.XmlException exception)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.InvalidPatchOperationXml,
                $"Operation '{operation.Id}' has invalid component XML: {exception.Message}."));
        }

        var target = operation.Target;

        if (string.IsNullOrWhiteSpace(target.Component))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires target.component for {PatchOperationTypes.MergeComponent}."));
        }

        if (!string.Equals(parsedComponent.Name.LocalName, target.Component, StringComparison.Ordinal))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.InvalidPatchOperationEntity,
                $"Operation '{operation.Id}' merge xml root <{parsedComponent.Name.LocalName}> must match target.component '{target.Component}'."));
        }

        if (string.IsNullOrWhiteSpace(target.EntityGuid))
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires target.entityGuid: entityName alone cannot locate an existing entity."));
        }

        var documentResult = LoadTargetDocument(gameRoot, operation);
        if (documentResult.Failure is not null)
        {
            return documentResult.Failure;
        }

        var fullPath = documentResult.FullPath;
        var document = documentResult.Document!;
        var entity = FindEntity(document, target.EntityGuid);

        if (entity is null)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetEntityMissing,
                $"Target entity GUID '{target.EntityGuid}' was not found.",
                fullPath));
        }

        var values = entity.Element("Values");
        if (values is null)
        {
            return TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetComponentMissing,
                $"Entity '{target.EntityGuid}' has no <Values> block.",
                fullPath));
        }

        var existing = values.Element(target.Component);
        var oldXml = existing?.ToString(SaveOptions.DisableFormatting) ?? string.Empty;
        var newXml = parsedComponent.ToString(SaveOptions.DisableFormatting);

        var diagnostics = new List<PatchDiagnostic>
        {
            new(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.TargetResolved,
                existing is null
                    ? $"Resolved mergeComponent: will add new <{target.Component}> to entity '{target.EntityGuid}'."
                    : $"Resolved mergeComponent: will merge {parsedComponent.Elements().Count()} child(ren) into existing <{target.Component}> on entity '{target.EntityGuid}'.",
                fullPath),
        };

        var write = new PatchWrite(
            operation.Id,
            PatchOperationTypes.MergeComponent,
            target.File,
            target.EntityGuid,
            target.EntityName,
            target.Component,
            string.Empty,
            null,
            oldXml,
            newXml);

        return new TargetResolveResult(write, diagnostics);
    }

    public static XElement? ResolveValueElement(XDocument document, string entityGuid, string componentName, string path)
    {
        var entity = FindEntity(document, entityGuid);
        var component = entity?.Element("Values")?.Element(componentName);
        return component is null ? null : ResolvePath(component, path);
    }

    private TargetDocumentResult LoadTargetDocument(string gameRoot, PatchOperation operation)
    {
        var target = operation.Target;
        var relativeFile = PathUtilities.ToGameRelativeFile(target.File);
        var fullPath = Path.Combine(gameRoot, relativeFile);

        if (!File.Exists(fullPath))
        {
            return TargetDocumentResult.Fail(TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetFileMissing,
                $"Target file not found: {fullPath}",
                fullPath)));
        }

        try
        {
            return new TargetDocumentResult(XDocument.Load(fullPath), fullPath, null);
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException)
        {
            return TargetDocumentResult.Fail(TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetFileReadFailed,
                $"Could not read target XML: {exception.Message}",
                fullPath)));
        }
    }

    private readonly record struct TargetDocumentResult(
        XDocument? Document,
        string FullPath,
        TargetResolveResult? Failure)
    {
        public static TargetDocumentResult Fail(TargetResolveResult failure)
            => new(null, string.Empty, failure);
    }

    private TargetNodeResolution ResolveTargetNode(string gameRoot, PatchOperation operation)
    {
        var diagnostics = new List<PatchDiagnostic>();
        var target = operation.Target;
        var relativeFile = PathUtilities.ToGameRelativeFile(target.File);
        var fullPath = Path.Combine(gameRoot, relativeFile);

        if (!File.Exists(fullPath))
        {
            return TargetNodeResolution.Fail(TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetFileMissing,
                $"Target file not found: {fullPath}",
                fullPath)));
        }

        XDocument document;

        try
        {
            document = XDocument.Load(fullPath);
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException)
        {
            return TargetNodeResolution.Fail(TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetFileReadFailed,
                $"Could not read target XML: {exception.Message}",
                fullPath)));
        }

        // entityGuid is the only key FindEntity matches on. A target that carries only entityName
        // (the schema's permissive anyOf allows it, because addEntity uses entityName as the new
        // group name) cannot locate an *existing* entity — surface that precisely instead of the
        // misleading "GUID '' was not found". Mirrors the removeEntity guard.
        if (string.IsNullOrWhiteSpace(target.EntityGuid))
        {
            return TargetNodeResolution.Fail(TargetResolveResult.Failed(Error(
                DiagnosticCodes.MissingPatchOperationField,
                $"Operation '{operation.Id}' requires target.entityGuid: entityName alone cannot locate an existing entity.",
                fullPath)));
        }

        var entity = FindEntity(document, target.EntityGuid);

        if (entity is null)
        {
            return TargetNodeResolution.Fail(TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetEntityMissing,
                $"Target entity GUID '{target.EntityGuid}' was not found.",
                fullPath)));
        }

        if (!string.IsNullOrWhiteSpace(target.EntityName)
            && !string.Equals((string?)entity.Attribute("Name"), target.EntityName, StringComparison.Ordinal))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Warning,
                DiagnosticCodes.TargetEntityNameMismatch,
                $"Target entity name is '{(string?)entity.Attribute("Name")}', expected '{target.EntityName}'.",
                fullPath));
        }

        var component = entity.Element("Values")?.Element(target.Component);

        if (component is null)
        {
            return TargetNodeResolution.Fail(TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetComponentMissing,
                $"Component '{target.Component}' was not found.",
                fullPath)));
        }

        // Catch a malformed path up front so the author gets a precise reason instead of the
        // generic "did not resolve": an empty segment (a leading/trailing/doubled '/') or a
        // predicate with no element name before '[' both otherwise silently match nothing.
        if (TryFindMalformedPath(target.Path, out var malformedReason))
        {
            return TargetNodeResolution.Fail(TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetPathMalformed,
                $"Path '{target.Path}' is malformed: {malformedReason}.",
                fullPath)));
        }

        var targetNode = ResolvePath(component, target.Path);

        if (targetNode is null)
        {
            return TargetNodeResolution.Fail(TargetResolveResult.Failed(Error(
                DiagnosticCodes.TargetPathMissing,
                $"Path '{target.Path}' did not resolve to a value.",
                fullPath)));
        }

        return new TargetNodeResolution(targetNode, fullPath, diagnostics, null);
    }

    private static string? NormaliseXml(string xml)
    {
        try
        {
            return XElement.Parse(xml).ToString(SaveOptions.DisableFormatting);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private readonly record struct TargetNodeResolution(
        XElement? TargetNode,
        string FullPath,
        IReadOnlyList<PatchDiagnostic> Diagnostics,
        TargetResolveResult? Failure)
    {
        public static TargetNodeResolution Fail(TargetResolveResult failure)
            => new(null, string.Empty, [], failure);
    }

    private static XElement? FindEntity(XDocument document, string entityGuid)
        => document.Descendants("Entity")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("Guid"), entityGuid, StringComparison.OrdinalIgnoreCase));

    private static XElement? ResolvePath(XElement root, string path)
    {
        var current = root;

        foreach (var segment in SplitPath(path))
        {
            var next = ResolveSegment(current, segment);

            if (next is null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    // Strip exactly one matching surrounding quote pair, so a genuinely-quoted literal is
    // handled cleanly while a mismatched/duplicated quote isn't silently swallowed (the old
    // char-set Trim removed any run of leading/trailing quotes). Inner doubled quotes are not
    // unescaped — only the outer pair is removed.
    private static string StripOneQuotePair(string value)
        => value.Length >= 2 && (value[0] == '\'' || value[0] == '"') && value[^1] == value[0]
            ? value[1..^1]
            : value;

    private static XElement? ResolveSegment(XElement current, string segment)
    {
        var predicateStart = segment.IndexOf('[', StringComparison.Ordinal);

        if (predicateStart < 0)
        {
            return current.Element(segment);
        }

        var elementName = segment[..predicateStart];
        var predicate = segment[(predicateStart + 1)..^1];
        var predicateParts = predicate.Split('=', 2);

        if (predicateParts.Length != 2)
        {
            return null;
        }

        // Trim whitespace around '=' so a naturally-formatted predicate like
        // `Name = 'Widget'` resolves the same as `Name='Widget'`. The value is
        // trimmed before stripping the surrounding quotes — otherwise a leading
        // space would leave the opening quote in place (" 'Widget" survives a
        // quote-only Trim).
        var predicatePath = predicateParts[0].Trim();
        var expectedValue = StripOneQuotePair(predicateParts[1].Trim());

        return current.Elements(elementName)
            .FirstOrDefault(element => ResolvePath(element, predicatePath)?.Value == expectedValue);
    }

    // Detect a structurally malformed path before resolution: an empty top-level segment
    // (leading/trailing/doubled '/') or a predicate segment with no element name before '['.
    // Both would otherwise resolve to null and surface as the generic "did not resolve".
    private static bool TryFindMalformedPath(string path, out string reason)
    {
        foreach (var segment in SplitPath(path))
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                reason = "it contains an empty path segment (a leading, trailing, or doubled '/')";
                return true;
            }

            var open = segment.IndexOf('[', StringComparison.Ordinal);
            if (open == 0)
            {
                reason = $"the segment '{segment}' has a predicate with no element name before '['";
                return true;
            }

            if (open > 0 && !segment.EndsWith(']'))
            {
                reason = $"the segment '{segment}' has a predicate that is missing its closing ']'";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    private static IEnumerable<string> SplitPath(string path)
    {
        var segmentStart = 0;
        var bracketDepth = 0;
        var quote = '\0'; // the open quote char while inside a predicate value, else '\0'

        for (var index = 0; index < path.Length; index++)
        {
            var character = path[index];

            // Inside a quoted predicate value, '[' / ']' / '/' are literal data — a value like
            // Item[Name=']']/Sub must not be split at the in-quote ']' (which would otherwise drop
            // bracketDepth to 0 and treat the following '/' as a top-level separator).
            if (quote != '\0')
            {
                if (character == quote) { quote = '\0'; }
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (character == '[')
            {
                bracketDepth++;
                continue;
            }

            if (character == ']')
            {
                bracketDepth--;
                continue;
            }

            if (character == '/' && bracketDepth == 0)
            {
                yield return path[segmentStart..index].Trim();
                segmentStart = index + 1;
            }
        }

        if (segmentStart < path.Length)
        {
            yield return path[segmentStart..].Trim();
        }
    }

    private static PatchDiagnostic Error(string code, string message, string? path = null)
        => new(PatchDiagnosticSeverity.Error, code, message, path);
}

public sealed record TargetResolveResult(
    PatchWrite? Write,
    IReadOnlyList<PatchDiagnostic> Diagnostics)
{
    public static TargetResolveResult Failed(params PatchDiagnostic[] diagnostics)
        => new(null, diagnostics);
}
