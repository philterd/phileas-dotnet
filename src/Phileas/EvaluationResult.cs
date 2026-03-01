namespace Phileas;

public class EvaluationResult
{
    public double Precision { get; }
    public double Recall { get; }
    public double F1 { get; }

    public EvaluationResult(double precision, double recall, double f1)
    {
        Precision = precision;
        Recall = recall;
        F1 = f1;
    }
}
