# PhEye Filter - AI-Powered Named Entity Recognition

The `PhEye` filter provides flexible AI-powered named entity recognition (NER) for detecting persons, organizations, locations, and other custom entities in text. It supports two modes of operation:

1. **Remote Service Mode**: Connects to a remote [PhEye](https://github.com/philterd/pheye) NLP service via HTTP
2. **Local Model Mode**: Uses a local ONNX BERT-based NER model for offline inference

## Features

- **Dual Operation Modes**: Choose between remote service or local model inference
- **Named Entity Recognition**: Detects persons, organizations, locations, and custom entity types
- **BERT Tokenization**: Built-in WordPiece tokenizer for BERT models (local mode)
- **ONNX Runtime**: Fast local inference using Microsoft's ONNX Runtime
- **Entity Grouping**: Automatically groups B-* and I-* tags into complete entities
- **Confidence Scoring**: Provides confidence scores for each detection
- **Configurable Thresholds**: Filter entities based on confidence levels per label
- **Bearer Token Authentication**: Secure communication with remote PhEye services

## Remote Service Mode

### Setup

Deploy a [PhEye](https://github.com/philterd/pheye) service or use an existing endpoint.

### Configuration

```csharp
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using PhileasPolicy = Phileas.Policy.Policy;

var policy = new PhileasPolicy
{
    Name = "pheye-remote-policy",
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
  "name": "pheye-remote-policy",
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
    input: "John Smith works at Microsoft in Seattle."
);

Console.WriteLine(result.FilteredText);
// Output: {{{REDACTED-person}}} works at {{{REDACTED-other}}} in {{{REDACTED-location-city}}}.
```

## Local Model Mode

### Setup

#### 1. Download the ONNX Model

Download a BERT NER ONNX model from HuggingFace, such as [protectai/bert-base-NER-onnx](https://huggingface.co/protectai/bert-base-NER-onnx):

```bash
# Clone the model repository
git clone https://huggingface.co/protectai/bert-base-NER-onnx

# Or download specific files:
# - model.onnx (the ONNX model file)
# - vocab.txt (the BERT vocabulary file)
```

#### 2. Configure the Filter

```csharp
var policy = new PhileasPolicy
{
    Name = "pheye-local-policy",
    Identifiers = new Identifiers
    {
        PhEyes = new List<PhEye>
        {
            new PhEye
            {
                PhEyeConfiguration = new PhEyeConfiguration
                {
                    ModelPath = "C:\\models\\model.onnx",
                    VocabPath = "C:\\models\\vocab.txt",
                    Labels = new List<string> { "PER", "ORG", "LOC", "MISC" }
                }
            }
        }
    }
};
```

#### 3. JSON Configuration

```json
{
  "name": "pheye-local-policy",
  "identifiers": {
    "pheye": [
      {
        "phEyeConfiguration": {
          "modelPath": "C:\\models\\model.onnx",
          "vocabPath": "C:\\models\\vocab.txt",
          "labels": ["PER", "ORG", "LOC", "MISC"]
        },
        "removePunctuation": false
      }
    ]
  }
}
```

### Mixed Configuration

If both `modelPath`/`vocabPath` **and** `endpoint` are provided, the filter will prefer the local model. If only `modelPath` or only `vocabPath` is set (but not both), the filter falls back to the remote service.

```csharp
new PhEyeConfiguration
{
    ModelPath = "C:\\models\\model.onnx",
    VocabPath = "C:\\models\\vocab.txt",
    Endpoint = "http://localhost:8080",  // Fallback if local model fails to load
    Labels = new List<string> { "PERSON" }
}
```

## Configuration Options

### PhEyeConfiguration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Endpoint` | `string` | `"http://localhost:8080"` | Base URL of the PhEye service (remote mode) |
| `BearerToken` | `string?` | `null` | Bearer token for API authentication (remote mode) |
| `Timeout` | `int` | `30` | Request timeout in seconds (remote mode) |
| `Labels` | `List<string>` | `["Person"]` | Entity labels to detect |
| `ModelPath` | `string?` | `null` | Path to ONNX model file (local mode) |
| `VocabPath` | `string?` | `null` | Path to BERT vocabulary file (local mode) |

### PhEye Filter Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RemovePunctuation` | `bool` | `false` | Strip punctuation before processing |
| `Strategies` | `List<PhEyeFilterStrategy>` | `[REDACT]` | Replacement strategies for detected entities |
| `Ignored` | `List<string>` | `[]` | Terms to ignore during detection |
| `IgnoredPatterns` | `List<IgnoredPattern>` | `[]` | Regex patterns to ignore |
| `Priority` | `int` | `0` | Filter priority for overlapping spans |

## Supported Entity Types

The filter maps entity labels to Phileas `FilterType` enums:

| Entity Label | FilterType | Description |
|--------------|------------|-------------|
| `PERSON`, `PER` | `FilterType.Person` | Person names |
| `LOCATION`, `LOC` | `FilterType.LocationCity` | Location names |
| `ORGANIZATION`, `ORG` | `FilterType.Other` | Organization names |
| `MISC` | `FilterType.Other` | Miscellaneous entities |

Custom labels not in this list are mapped to `FilterType.Other`.

## Confidence Thresholds

You can set minimum confidence thresholds per label to filter out low-confidence predictions:

```csharp
var thresholds = new Dictionary<string, double>
{
    { "PERSON", 0.90 },
    { "ORG", 0.85 },
    { "LOC", 0.80 }
};

// Note: Thresholds are typically configured via filter strategies
// or custom filter initialization when using the PhEye filter directly
```

When using filter strategies:

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

You can configure multiple PhEye instances in a single policy, each with different endpoints or model configurations:

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

### Remote Service Mode

- **Network Latency**: Processing time depends on network speed and service location
- **Scalability**: PhEye service can be scaled horizontally
- **Resource Usage**: Minimal local resources required
- **Throughput**: Depends on service capacity and configuration

### Local Model Mode

- **Model Size**: BERT-base models are typically ~400MB
- **Memory Usage**: Model must be loaded into memory (~400MB RAM)
- **Inference Speed**: Processing time depends on text length and CPU/GPU
- **Token Limit**: Maximum sequence length is 512 tokens (BERT limit)
- **No Network**: Operates completely offline

### Choosing a Mode

| Factor | Remote Service | Local Model |
|--------|----------------|-------------|
| Setup Complexity | Easy | Moderate |
| Network Required | Yes | No |
| Privacy | Data leaves host | Data stays local |
| Scalability | High | Limited by host resources |
| Latency | Variable | Consistent |
| Resource Usage | Low | Moderate-High |

## Example Scenarios

### Medical Records Processing

```csharp
var policy = new PhileasPolicy
{
    Name = "medical-ner",
    Identifiers = new Identifiers
    {
        PhEyes = new List<PhEye>
        {
            new PhEye
            {
                PhEyeConfiguration = new PhEyeConfiguration
                {
                    ModelPath = "C:\\models\\medical-ner.onnx",
                    VocabPath = "C:\\models\\vocab.txt",
                    Labels = new List<string> { "PERSON", "CONDITION", "MEDICATION", "PROCEDURE" }
                }
            }
        }
    }
};

var text = "Dr. Sarah Johnson prescribed metformin to treat the patient's diabetes.";
var result = new FilterService().Filter(policy, "ctx", 0, text);
```

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

### Remote Service Issues

**Connection Timeout**
- Verify the endpoint URL is correct and accessible
- Check network connectivity and firewall rules
- Increase the `Timeout` value if the service is slow

**Authentication Errors**
- Ensure the `BearerToken` is correct
- Verify the token has not expired

**No Entities Detected**
- Confirm the `Labels` list matches the model's output labels
- Check service logs for errors

### Local Model Issues

**Model Loading Errors**
- Verify the `ModelPath` and `VocabPath` are correct
- Ensure the ONNX model format is compatible with ONNX Runtime 1.20.1+
- Check file permissions

**OutOfMemoryException**
- The BERT model requires ~400MB RAM minimum
- Close other applications or increase available memory

**Poor Detection Quality**
- Verify the model matches your text domain (general, medical, legal, etc.)
- Adjust confidence thresholds
- Consider fine-tuning the model on domain-specific data

**Partial Configuration Fallback**
- If only `ModelPath` or `VocabPath` is set, the filter uses remote service
- Ensure both paths are provided for local inference

## Resource Cleanup

The PhEye filter implements `IDisposable` for proper resource cleanup:

```csharp
using var filterService = new FilterService();
// Filter operations...
// Resources automatically cleaned up
```

When manually creating filters:

```csharp
using var filter = new PhEyeFilter(config, phEyeConfig, false, thresholds);
// Use the filter...
// Automatically disposes ONNX session and HTTP client
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

- Read about [Filter Strategies](filter-strategies.md) to customize redaction behavior
- Learn about [Filter Conditions](filter-conditions.md) for conditional redaction
- Explore the [API Reference](api-reference.md) for detailed method documentation
- Check out the [PhEye service documentation](https://github.com/philterd/pheye) for service setup

## Model Information

### Recommended Models

- **General NER**: [protectai/bert-base-NER-onnx](https://huggingface.co/protectai/bert-base-NER-onnx)
- **Medical NER**: Fine-tuned BERT models for medical domain
- **Custom Models**: Train and export your own BERT NER models to ONNX format

### Model Requirements

- Format: ONNX
- Architecture: BERT-based token classification
- Input tensors: `input_ids`, `attention_mask`, `token_type_ids`
- Output tensor: `logits` with shape `[batch_size, sequence_length, num_labels]`
- Vocabulary: WordPiece vocabulary file compatible with BERT tokenizer

## Questions?

Visit the [Phileas documentation](https://philterd.github.io/phileas-net/) or the [GitHub repository](https://github.com/philterd/phileas-net) for more information.
