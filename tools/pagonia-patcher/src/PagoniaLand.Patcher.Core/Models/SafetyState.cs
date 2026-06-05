using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace PagoniaLand.Patcher;

public enum SafetyState
{
    Yes,
    No,
    Unknown,
}

public sealed class SafetyStateYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
        => type == typeof(SafetyState) || type == typeof(SafetyState?);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (!parser.TryConsume<Scalar>(out var scalar))
        {
            throw new YamlException("Expected a scalar value for a safety field.");
        }

        return scalar.Value.ToLowerInvariant() switch
        {
            "true" => SafetyState.Yes,
            "false" => SafetyState.No,
            "unknown" => SafetyState.Unknown,
            _ => throw new YamlException(
                scalar.Start,
                scalar.End,
                $"Invalid safety value '{scalar.Value}'. Expected true, false, or unknown.")
        };
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is null)
        {
            emitter.Emit(new Scalar(string.Empty));
            return;
        }

        var state = (SafetyState)value;
        var text = state switch
        {
            SafetyState.Yes => "true",
            SafetyState.No => "false",
            SafetyState.Unknown => "unknown",
            _ => throw new InvalidOperationException($"Unsupported SafetyState value: {state}.")
        };

        emitter.Emit(new Scalar(text));
    }
}
