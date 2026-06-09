using YamlDotNet.Serialization;

namespace PagoniaLand.Patcher;

public sealed class PatchFile
{
    [YamlMember(Alias = "operations")]
    public List<PatchOperation> Operations { get; init; } = [];
}

public sealed class PatchOperation
{
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    [YamlMember(Alias = "operation")]
    public string Operation { get; init; } = string.Empty;

    [YamlMember(Alias = "risk")]
    public string Risk { get; init; } = string.Empty;

    [YamlMember(Alias = "reason")]
    public string Reason { get; init; } = string.Empty;

    [YamlMember(Alias = "target")]
    public PatchTarget Target { get; init; } = new();

    [YamlMember(Alias = "expectedOldValue")]
    public string? ExpectedOldValue { get; init; }

    [YamlMember(Alias = "value")]
    public string? Value { get; init; }

    // Arithmetic operands (multiplyValue / addValue). The new leaf value is computed at plan time
    // from expectedOldValue combined with one of these; both may carry a {{ tweaks.<id> }} placeholder
    // so one shared tweak can scale many targets relative to their vanilla values.
    [YamlMember(Alias = "factor")]
    public string? Factor { get; init; }

    [YamlMember(Alias = "delta")]
    public string? Delta { get; init; }

    // Rounding policy for the computed result (game-database values are integers). round | floor | ceil;
    // null defaults to round. ClampMin/ClampMax optionally bound the rounded result. These are static
    // policy fields, not templated.
    [YamlMember(Alias = "rounding")]
    public string? Rounding { get; init; }

    [YamlMember(Alias = "clampMin")]
    public string? ClampMin { get; init; }

    [YamlMember(Alias = "clampMax")]
    public string? ClampMax { get; init; }

    [YamlMember(Alias = "attribute")]
    public string? Attribute { get; init; }

    [YamlMember(Alias = "xml")]
    public string? Xml { get; init; }

    [YamlMember(Alias = "expectedOldXml")]
    public string? ExpectedOldXml { get; init; }
}

public sealed class PatchTarget
{
    [YamlMember(Alias = "file")]
    public string File { get; init; } = string.Empty;

    [YamlMember(Alias = "entityGuid")]
    public string EntityGuid { get; init; } = string.Empty;

    [YamlMember(Alias = "entityName")]
    public string EntityName { get; init; } = string.Empty;

    [YamlMember(Alias = "component")]
    public string Component { get; init; } = string.Empty;

    [YamlMember(Alias = "path")]
    public string Path { get; init; } = string.Empty;
}
