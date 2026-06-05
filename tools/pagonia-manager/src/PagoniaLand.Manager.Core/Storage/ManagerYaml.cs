using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PagoniaLand.Manager;

internal static class ManagerYaml
{
    public static IDeserializer CreateDeserializer()
        // NullNamingConvention on both ends: every model carries explicit
        // [YamlMember(Alias=...)], so naming is alias-only by design (symmetric with the serializer).
        => new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();

    public static ISerializer CreateSerializer()
        => new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();
}
