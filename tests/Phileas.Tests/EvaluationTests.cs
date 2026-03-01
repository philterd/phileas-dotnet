using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Model.Filtering;
using Phileas;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class EvaluationTests
{
    private const int FloatingPointPrecision = 6;
    private static PhileasPolicy EmailPolicy() => new PhileasPolicy
    {
        Name = "test",
        Identifiers = new Identifiers { EmailAddress = new EmailAddress() }
    };

    [Fact]
    public void Evaluate_PerfectDetection_ReturnsPrecisionRecallF1OfOne()
    {
        const string input = "Contact john.doe@example.com for help.";
        var filterResult = FilterPolicyLoader.Filter(EmailPolicy(), "test", 0, input);
        var groundTruth = filterResult.Spans.ToList();

        var evaluation = FilterPolicyLoader.Evaluate(EmailPolicy(), "test", 0, input, groundTruth);

        Assert.Equal(1.0, evaluation.Precision);
        Assert.Equal(1.0, evaluation.Recall);
        Assert.Equal(1.0, evaluation.F1);
    }

    [Fact]
    public void Evaluate_NoDetectionNoGroundTruth_ReturnsZeroMetrics()
    {
        const string input = "No PII here.";
        var groundTruth = new List<Span>();

        var evaluation = FilterPolicyLoader.Evaluate(EmailPolicy(), "test", 0, input, groundTruth);

        Assert.Equal(0.0, evaluation.Precision);
        Assert.Equal(0.0, evaluation.Recall);
        Assert.Equal(0.0, evaluation.F1);
    }

    [Fact]
    public void Evaluate_FalseNegative_LowRecall()
    {
        // Ground truth says there's an email but the policy won't detect it
        // (simulate by providing a span that doesn't match any filter result)
        const string input = "No PII here.";
        var groundTruth = new List<Span>
        {
            new Span { CharacterStart = 0, CharacterEnd = 10 }
        };

        var evaluation = FilterPolicyLoader.Evaluate(EmailPolicy(), "test", 0, input, groundTruth);

        Assert.Equal(0.0, evaluation.Precision);  // no detections at all → precision = 0
        Assert.Equal(0.0, evaluation.Recall);     // missed the ground truth span → recall = 0
        Assert.Equal(0.0, evaluation.F1);
    }

    [Fact]
    public void Evaluate_FalsePositive_LowPrecision()
    {
        // Policy detects an email, but ground truth says there is none
        const string input = "Contact john.doe@example.com for help.";
        var groundTruth = new List<Span>(); // intentionally empty

        var evaluation = FilterPolicyLoader.Evaluate(EmailPolicy(), "test", 0, input, groundTruth);

        Assert.Equal(0.0, evaluation.Precision);  // detection with no ground truth → precision = 0
        Assert.Equal(0.0, evaluation.Recall);     // no ground truth spans at all → recall = 0
        Assert.Equal(0.0, evaluation.F1);
    }

    [Fact]
    public void Evaluate_F1IsHarmonicMeanOfPrecisionAndRecall()
    {
        // Two ground-truth spans, policy detects one correctly and misses the other.
        // First, detect what the filter finds for a text with two emails.
        const string input = "a@a.com and b@b.com";
        var both = FilterPolicyLoader.Filter(EmailPolicy(), "test", 0, input).Spans;
        Assert.Equal(2, both.Count);

        // Ground truth contains both spans; provide a policy that only detects one
        // by constructing the ground truth manually with an extra phantom span.
        var groundTruth = new List<Span>(both)
        {
            new Span { CharacterStart = 100, CharacterEnd = 110 } // extra phantom → FN
        };

        var evaluation = FilterPolicyLoader.Evaluate(EmailPolicy(), "test", 0, input, groundTruth);

        // TP=2, FP=0, FN=1
        double expectedPrecision = 2.0 / (2 + 0);     // 1.0
        double expectedRecall    = 2.0 / (2 + 1);     // ~0.667
        double expectedF1        = 2.0 * expectedPrecision * expectedRecall / (expectedPrecision + expectedRecall);

        Assert.Equal(expectedPrecision, evaluation.Precision, FloatingPointPrecision);
        Assert.Equal(expectedRecall, evaluation.Recall, FloatingPointPrecision);
        Assert.Equal(expectedF1, evaluation.F1, FloatingPointPrecision);
    }
}
