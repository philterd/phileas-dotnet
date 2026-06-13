# PhEye Filter - AI-Powered Named Entity Recognition

The `PhEye` filter provides AI-powered named entity recognition (NER) for detecting persons,
organizations, locations, and other entities in text. It runs in one of two modes: it connects to a remote
[PhEye](https://github.com/philterd/pheye) NLP service via HTTP, or, when `ModelPath` is set, it runs a local
GLiNER model entirely in-process with no network call (see [Local GLiNER inference](#local-gliner-inference)).

## Features

- **Named Entity Recognition**: Detects persons, organizations, locations, and custom entity types
- **Local or remote**: Run a local GLiNER model in-process, or call a remote PhEye service
- **Confidence Scoring**: Provides confidence scores for each detection
- **Configurable Thresholds**: Filter entities based on confidence levels per label
- **Bearer Token Authentication**: Secure communication with remote PhEye services

## Setup

Deploy a [PhEye](https://github.com/philterd/pheye) service or use an existing endpoint, then point the
filter at it.

### Configuration

```csharp
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using PhileasPolicy = Phileas.Policy.Policy;

var policy = new PhileasPolicy
{
    Name = "pheye-policy",
    Identifiers = new Identifiers
    {
        PhEyes = new List<PhEye>
        {
            new PhEye
            {
                PhEyeConfiguration = new PhEyeConfiguration
                {
                    Endpoint = "http://localhost:8080",
                    BearerToken = "your-api-token",  // Optional
                    Timeout = 30,                     // Seconds
                    Labels = new List<string> { "PERSON", "ORG", "LOC" }
                }
            }
        }
    }
};
```

### JSON Configuration

```json
{
  "identifiers": {
    "pheye": [
      {
        "phEyeConfiguration": {
          "endpoint": "http://localhost:8080",
          "bearerToken": "your-api-token",
          "timeout": 30,
          "labels": ["PERSON", "ORG", "LOC"]
        },
        "removePunctuation": false
      }
    ]
  }
}
```

### Usage Example

```csharp
var filterService = new FilterService();

var result = filterService.Filter(
    policy: policy,
    context: "default",
    piece: 0,
    input: "John Smith joined the meeting."
);

Console.WriteLine(result.FilteredText);
// Output: {{{REDACTED-person}}} joined the meeting.
```

## Local GLiNER inference

Set `ModelPath` to run a local [GLiNER](https://github.com/urchade/GLiNER) model in-process instead of calling a
remote service. Detection happens entirely on-device, so no text leaves the host and no PhEye service is required.
This is the mode produced by the PhiSQL `MODEL` clause (policy schema 1.1.0).

The path must point at a GLiNER model directory containing:

- an exported ONNX graph: `model.onnx` or `model_quantized.onnx` (an `onnx/` subdirectory is also searched),
- the SentencePiece tokenizer `spm.model`, and
- `gliner_config.json`.

```csharp
PhEyes = new List<PhEye>
{
    new PhEye
    {
        PhEyeConfiguration = new PhEyeConfiguration
        {
            ModelPath = "/models/ph-eye-pii-base",
            Labels    = new List<string> { "person", "email address", "social security number" },
            Threshold = 0.5
        }
    }
};
```

```json
{
  "identifiers": {
    "pheye": [
      {
        "phEyeConfiguration": {
          "modelPath": "/models/ph-eye-pii-base",
          "labels": ["person", "email address", "social security number"],
          "threshold": 0.5
        }
      }
    ]
  }
}
```

Notes:

- GLiNER is zero-shot, so `Labels` is a free-text prompt: any entity description works, not a fixed enum.
- `Threshold` (default `0.5`) is the minimum span confidence; raise it for higher precision, lower it for higher recall.
- When `ModelPath` is set, `Endpoint`, `BearerToken`, and `Timeout` are ignored.
- The model loads once on first use and is reused across calls. Call `Dispose()` on the filter to release it.

## Configuration Options

### PhEyeConfiguration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Endpoint` | `string` | `"http://localhost:8080"` | Base URL of the PhEye service (remote mode) |
| `BearerToken` | `string?` | `null` | Bearer token for API authentication (remote mode) |
| `Timeout` | `int` | `30` | Request timeout in seconds (remote mode) |
| `Labels` | `List<string>` | `["Person"]` | Entity labels to detect. In local mode this is the GLiNER prompt |
| `ModelPath` | `string?` | `null` | Path to a local GLiNER model directory. When set, the filter runs on-device and ignores `Endpoint` |
| `Threshold` | `double` | `0.5` | Minimum span confidence for local inference (local mode only) |

### PhEye Filter Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RemovePunctuation` | `bool` | `false` | Strip punctuation before processing |
| `Strategies` | `List<PhEyeFilterStrategy>` | `[REDACT]` | Replacement strategies for detected entities |
| `Ignored` | `List<string>` | `[]` | Terms to ignore during detection |
| `IgnoredPatterns` | `List<IgnoredPattern>` | `[]` | Regex patterns to ignore |
| `Priority` | `int` | `0` | Filter priority for overlapping spans |

## Supported Entity Types

Detected entities are mapped to a Phileas `FilterType`:

| Entity Label | FilterType | Description |
|--------------|------------|-------------|
| `PERSON` (case-insensitive) | `FilterType.Person` | Person names |
| Any other label | `FilterType.Other` | All other entity types |

The original service label is preserved on each span's `Classification`.

## Confidence Thresholds

Use a per-label minimum confidence (via a strategy condition) to filter out low-confidence predictions:

```csharp
PhEyes = new List<PhEye>
{
    new PhEye
    {
        PhEyeConfiguration = new PhEyeConfiguration
        {
            Endpoint = "http://localhost:8080",
            Labels = new List<string> { "PERSON" }
        },
        Strategies = new List<PhEyeFilterStrategy>
        {
            new PhEyeFilterStrategy
            {
                Strategy = "REDACT",
                Condition = "confidence >= 0.90"  // Minimum confidence
            }
        }
    }
}
```

## Multiple PhEye Configurations

You can configure multiple PhEye instances in a single policy, each pointing at a different endpoint:

```csharp
PhEyes = new List<PhEye>
{
    new PhEye
    {
        PhEyeConfiguration = new PhEyeConfiguration
        {
            Endpoint = "http://pheye-persons:8080",
            Labels = new List<string> { "PERSON" }
        }
    },
    new PhEye
    {
        PhEyeConfiguration = new PhEyeConfiguration
        {
            Endpoint = "http://pheye-orgs:8080",
            Labels = new List<string> { "ORGANIZATION" }
        }
    }
}
```

## Filter Strategies

The PhEye filter supports all standard Phileas strategies:

```csharp
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;

PhEyes = new List<PhEye>
{
    new PhEye
    {
        PhEyeConfiguration = new PhEyeConfiguration
        {
            Endpoint = "http://localhost:8080",
            Labels = new List<string> { "PERSON" }
        },
        Strategies = new List<PhEyeFilterStrategy>
        {
            // Mask person names
            new PhEyeFilterStrategy { Strategy = "MASK" },

            // Or use static replacement
            new PhEyeFilterStrategy
            {
                Strategy = "STATIC_REPLACE",
                StaticReplacement = "[NAME REMOVED]"
            }
        }
    }
}
```

See [Filter Strategies](filter-strategies.md) for all available options.

## Ignored Terms

Configure terms that should not be redacted:

```csharp
PhEyes = new List<PhEye>
{
    new PhEye
    {
        PhEyeConfiguration = new PhEyeConfiguration
        {
            Endpoint = "http://localhost:8080",
            Labels = new List<string> { "PERSON" }
        },
        Ignored = new List<string> { "John", "Microsoft", "MIT" }
    }
}
```

## Performance Considerations

- **Network Latency**: Processing time depends on network speed and service location.
- **Scalability**: The PhEye service can be scaled horizontally.
- **Resource Usage**: Minimal local resources are required.
- **Throughput**: Depends on service capacity and configuration.

## Example Scenarios

### Multi-Language Support

```csharp
PhEyes = new List<PhEye>
{
    new PhEye
    {
        PhEyeConfiguration = new PhEyeConfiguration
        {
            Endpoint = "http://pheye-english:8080",
            Labels = new List<string> { "PERSON", "ORG", "LOC" }
        }
    },
    new PhEye
    {
        PhEyeConfiguration = new PhEyeConfiguration
        {
            Endpoint = "http://pheye-spanish:8080",
            Labels = new List<string> { "PERSON", "ORG", "LOC" }
        }
    }
}
```

## Troubleshooting

**Connection Timeout**
- Verify the endpoint URL is correct and accessible.
- Check network connectivity and firewall rules.
- Increase the `Timeout` value if the service is slow.

**Authentication Errors**
- Ensure the `BearerToken` is correct.
- Verify the token has not expired.

**No Entities Detected**
- Confirm the `Labels` list matches the service's output labels.
- Check the service logs for errors.

## Resource Cleanup

The PhEye filter implements `IDisposable` for proper resource cleanup:

```csharp
using var filter = new PhEyeFilter(config, phEyeConfig, false, thresholds);
// Use the filter...
// Automatically disposes the HTTP client and, in local mode, the loaded GLiNER model.
```

## Integration with Phileas Pipeline

The PhEye filter integrates seamlessly with other Phileas filters:

```csharp
var policy = new PhileasPolicy
{
    Name = "comprehensive-pii",
    Identifiers = new Identifiers
    {
        // AI-powered entity detection
        PhEyes = new List<PhEye>
        {
            new PhEye
            {
                PhEyeConfiguration = new PhEyeConfiguration
                {
                    Endpoint = "http://localhost:8080",
                    Labels = new List<string> { "PERSON", "ORG" }
                }
            }
        },

        // Pattern-based detectors
        EmailAddress = new EmailAddress(),
        PhoneNumber = new PhoneNumber(),
        Ssn = new Ssn(),
        CreditCard = new CreditCard()
    }
};
```

## Next Steps

- Read about [Filter Strategies](filter-strategies.md) to customize redaction behavior.
- Learn about [Filter Conditions](filter-conditions.md) for conditional redaction.
- Explore the [API Reference](api-reference.md) for detailed method documentation.
- Check out the [PhEye service documentation](https://github.com/philterd/pheye) for service setup.

## Questions?

Visit the [Phileas documentation](https://philterd.github.io/phileas-dotnet/) or the [GitHub repository](https://www.github.com/philterd/phileas-dotnet) for more information.
