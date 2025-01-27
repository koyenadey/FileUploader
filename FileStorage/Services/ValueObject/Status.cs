using System.Text.Json.Serialization;

namespace FileStorage.Services.ValueObject
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Status
    {
        Success,
        Error
    }
}