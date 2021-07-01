using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qs.EventGrid.Emulator
{
    [DebuggerStepThrough]
    public static class JsonHelpers
    {
        /// <summary>Parse a string into a JsonElement, or out the JsonException.</summary>
        public static JsonElement ParseJson(this string @string, out JsonException exception)
        {
            exception = null;

            try { return JsonDocument.Parse(@string).RootElement; }
            catch (JsonException ex)
            {
                exception = ex;
                return new JsonElement();
            }
        }

        /// <summary>Get a JsonDocument for the object.</summary>
        public static JsonDocument ToJsonDocument(this object @object)
            => JsonDocument.Parse(JsonSerializer.Serialize(@object, JsonSerializerOptions));

        /// <summary>Get a json (string) for the object.</summary>
        public static string ToJson(this object @object)
            => JsonSerializer.Serialize(@object, JsonSerializerOptions);

        /// <summary>Get an object from the json string.</summary>
        public static T FromJson<T>(this string @string)
         => JsonSerializer.Deserialize<T>(@string, JsonSerializerOptions);

        /// <summary>Get an object from the json document.</summary>
        public static T FromJson<T>(this JsonDocument document)
         => document.RootElement.FromJson<T>();

        /// <summary>Get an object from the json element.</summary>
        public static T FromJson<T>(this JsonElement element)
         => JsonSerializer.Deserialize<T>(element.ToString(), JsonSerializerOptions);

        /// <summary>Default options for System.Text.Json serialisation.</summary>
        public readonly static JsonSerializerOptions JsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            IgnoreNullValues = true,
        };
    }
}
