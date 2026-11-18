using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesignApp.Infrastructure.Database;

/// <summary>How the jsonb columns of a built room (its objects and its finish) are written: camelCase, enums by name, no nulls.</summary>
internal static class JsonColumnOptions
{
    /// <summary>The options.</summary>
    public static readonly JsonSerializerOptions Value = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
