using System;
using System.Buffers;
using System.Text.Json;
using RPC.Library.Abstractions;

namespace RPC.Library.Serialization
{
    /// <summary>
    /// System.Text.Json straight to and from UTF-8 bytes, no intermediate strings
    /// </summary>
    public sealed class JsonRpcSerializer : IRpcSerializer
    {
        private static readonly object[] NO_ARGUMENTS = new object[0];

        public static readonly JsonRpcSerializer Default = new JsonRpcSerializer();

        private readonly JsonSerializerOptions options;

        public JsonRpcSerializer(JsonSerializerOptions options = null)
        {
            this.options = options ?? new JsonSerializerOptions(JsonSerializerDefaults.General);
        }

        public void Serialize(IBufferWriter<byte> writer, object value, Type type)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (type == null) throw new ArgumentNullException(nameof(type));

            using (Utf8JsonWriter json = new Utf8JsonWriter(writer))
            {
                JsonSerializer.Serialize(json, value, type, this.options);
            }
        }

        public object Deserialize(ReadOnlyMemory<byte> data, Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            return JsonSerializer.Deserialize(data.Span, type, this.options);
        }

        public void SerializeArguments(IBufferWriter<byte> writer, object[] arguments, Type[] types)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (types == null) throw new ArgumentNullException(nameof(types));
            if (types.Length == 0) return;
            if (arguments == null || arguments.Length != types.Length)
                throw new ArgumentException($"Expected {types.Length} arguments", nameof(arguments));

            using (Utf8JsonWriter json = new Utf8JsonWriter(writer))
            {
                json.WriteStartArray();
                for (int i = 0; i < types.Length; i++)
                {
                    JsonSerializer.Serialize(json, arguments[i], types[i], this.options);
                }

                json.WriteEndArray();
            }
        }

        public object[] DeserializeArguments(ReadOnlyMemory<byte> data, Type[] types)
        {
            if (types == null) throw new ArgumentNullException(nameof(types));
            if (types.Length == 0) return NO_ARGUMENTS;
            if (data.IsEmpty) throw new JsonException($"Expected {types.Length} arguments but the payload is empty");

            object[] arguments = new object[types.Length];
            using (JsonDocument document = JsonDocument.Parse(data))
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != types.Length)
                    throw new JsonException($"Expected an array of {types.Length} arguments");

                int i = 0;
                foreach (JsonElement element in root.EnumerateArray())
                {
                    arguments[i] = JsonSerializer.Deserialize(element, types[i], this.options);
                    i++;
                }
            }

            return arguments;
        }
    }
}
