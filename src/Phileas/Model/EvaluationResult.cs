namespace Phileas.Model;

/// <summary>
/// Holds the precision, recall, and F1 score produced by evaluating filter output
/// against a set of ground-truth spans.
/// </summary>
public class EvaluationResult
{
    /// <summary>Gets the precision score (true positives / (true positives + false positives)).</summary>
    public double Precision { get; }

    /// <summary>Gets the recall score (true positives / (true positives + false negatives)).</summary>
    public double Recall { get; }

    /// <summary>Gets the F1 score (harmonic mean of precision and recall).</summary>
    public double F1 { get; }

    /// <summary>
    /// Initializes a new <see cref="EvaluationResult"/> with the given metric values.
    /// </summary>
    /// <param name="precision">Precision score in the range [0, 1].</param>
    /// <param name="recall">Recall score in the range [0, 1].</param>
    /// <param name="f1">F1 score in the range [0, 1].</param>
    public EvaluationResult(double precision, double recall, double f1)
    {
        Precision = precision;
        Recall = recall;
        F1 = f1;
    }
}
