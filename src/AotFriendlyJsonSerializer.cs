global using JsonArray = System.Collections.Generic.List<object>;
global using JsonObject = System.Collections.Generic.OrderedDictionary<string, object>;

using System.Text;
using System.Text.Json;

namespace Project {

    static class AotFriendlyJsonSerializer {

        public static void Serialize(object obj, Stream stream, int indentSize = 2) {
            var options = new JsonWriterOptions {
                Indented = true,
                IndentSize = indentSize
            };
            using var writer = new Utf8JsonWriter(stream, options);
            Write(writer, obj);
        }

#if DEBUG
        public static string DebugSerialize(object obj) {
            using var mem = new MemoryStream();
            Serialize(obj, mem, 4);
            return Encoding.UTF8.GetString(mem.ToArray());
        }
#endif

        static void Write(Utf8JsonWriter writer, object value) {
            switch(value) {
                case Boolean b:
                    writer.WriteBooleanValue(b);
                    break;

                case String s:
                    writer.WriteStringValue(s);
                    break;

                case Int32 i:
                    writer.WriteNumberValue(i);
                    break;

                case IDictionary<string, object> dict:
                    writer.WriteStartObject();
                    foreach(var (k, v) in dict) {
                        writer.WritePropertyName(k);
                        Write(writer, v);
                    }
                    writer.WriteEndObject();
                    break;

                case System.Collections.IEnumerable list:
                    writer.WriteStartArray();
                    foreach(var item in list) {
                        Write(writer, item);
                    }
                    writer.WriteEndArray();
                    break;

                default:
                    throw new NotSupportedException();
            }
        }
    }
}
