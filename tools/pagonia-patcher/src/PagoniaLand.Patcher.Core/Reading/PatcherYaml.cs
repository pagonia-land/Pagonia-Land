using YamlDotNet.Serialization;

namespace PagoniaLand.Patcher;

internal static class PatcherYaml
{
    public static IDeserializer CreateDeserializer()
        => new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithTypeConverter(new SafetyStateYamlConverter())
            .Build();

    public static ISerializer CreateSerializer()
        => new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .WithTypeConverter(new SafetyStateYamlConverter())
            .Build();
}
