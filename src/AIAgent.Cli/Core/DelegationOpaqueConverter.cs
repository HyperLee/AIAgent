using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIAgent.Cli.Core;

internal sealed class DelegationOpaqueConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => true;
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(Blocked<>).MakeGenericType(typeToConvert), nonPublic: true)!;
    private sealed class Blocked<T> : JsonConverter<T>
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException("委派物件不提供一般序列化");
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => throw new NotSupportedException("委派物件不提供一般序列化");
    }
}