using System.Text.Json.Serialization;

namespace Phileas.Policy.Filters;

public class PhEyeConfiguration
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "http://localhost:8080";

    [JsonPropertyName("bearerToken")]
    public string? BearerToken { get; set; }

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 30;

    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = new List<string> { "Person" };
}
