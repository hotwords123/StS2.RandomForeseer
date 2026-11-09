using System.Text.Json.Nodes;

namespace RandomForeseer.Data.Migrations;

internal static class JsonObjectExtensions
{
    extension(JsonObject data)
    {
        public bool? GetBoolean(string propertyName)
        {
            return data.TryGetPropertyValue(propertyName, out var value)
                ? value?.GetValue<bool>()
                : null;
        }

        public void MoveProperty(string legacyName, string currentName)
        {
            if (data.TryGetPropertyValue(legacyName, out var value))
            {
                data.SetIfMissing(currentName, value);
                data.Remove(legacyName);
            }
        }

        public void SetIfMissing(string propertyName, JsonNode? value)
        {
            if (!data.ContainsKey(propertyName))
            {
                data[propertyName] = value?.DeepClone();
            }
        }

        public void SetIfMissing(string propertyName, bool value)
        {
            if (!data.ContainsKey(propertyName))
            {
                data[propertyName] = value;
            }
        }
    }
}
