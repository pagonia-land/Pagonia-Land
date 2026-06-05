using YamlDotNet.Serialization;

namespace PagoniaLand.Patcher;

/// <summary>
/// One user-adjustable parameter a mod author exposes in <c>mod.yaml</c> under the
/// <c>tweaks:</c> block. The declaration is the contract a future manager surfaces to the
/// user and that the patcher's templating engine (<c>{{ tweaks.&lt;id&gt; }}</c>) resolves against.
/// This step only parses + validates the declaration; nothing consumes the value yet.
/// </summary>
/// <remarks>
/// <see cref="Default"/> is kept as the raw scalar text rather than a typed union: the JSON
/// Schema enforces that the literal matches <see cref="Type"/>, and the few semantic checks the
/// validator runs (range for numbers, membership for enums) parse the text on demand with the
/// invariant culture. Keeping it a string sidesteps a polymorphic YAML converter and stays
/// AOT-trivial.
/// </remarks>
public sealed class TweakDeclaration
{
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>One of <c>number</c>, <c>integer</c>, <c>boolean</c>, <c>enum</c>.</summary>
    [YamlMember(Alias = "type")]
    public string Type { get; init; } = string.Empty;

    [YamlMember(Alias = "label")]
    public string Label { get; init; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string? Description { get; init; }

    /// <summary>The default value as written in YAML (e.g. <c>5</c>, <c>true</c>, <c>standard</c>).</summary>
    [YamlMember(Alias = "default")]
    public string Default { get; init; } = string.Empty;

    [YamlMember(Alias = "min")]
    public double? Min { get; init; }

    [YamlMember(Alias = "max")]
    public double? Max { get; init; }

    [YamlMember(Alias = "step")]
    public double? Step { get; init; }

    /// <summary>Allowed values for an <c>enum</c> tweak. Empty for non-enum types.</summary>
    [YamlMember(Alias = "values")]
    public List<TweakEnumValue> Values { get; init; } = [];

    /// <summary>
    /// Legacy ids this tweak used to carry. The manager maps a stored override under an old id
    /// forward to the current id so renaming a tweak doesn't silently drop the user's choice.
    /// </summary>
    [YamlMember(Alias = "aliases")]
    public List<string> Aliases { get; init; } = [];
}

/// <summary>One option of an <c>enum</c> tweak: the stored <see cref="Value"/> plus a
/// human-readable <see cref="Label"/> the manager shows in its picker.</summary>
public sealed class TweakEnumValue
{
    [YamlMember(Alias = "value")]
    public string Value { get; init; } = string.Empty;

    [YamlMember(Alias = "label")]
    public string Label { get; init; } = string.Empty;
}
