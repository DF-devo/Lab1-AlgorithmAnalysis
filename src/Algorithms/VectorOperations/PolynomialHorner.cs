namespace Algorithms.VectorOperations;

public class PolynomialHorner : IAlgorithm
{
    public string Name => "Polynomial Horner";
    public string Description => "Вычисление многочлена P(x) при x=1.5 по схеме Горнера.";
    public ComplexityType TheoreticalComplexity => ComplexityType.Linear;
    public ExperimentType ExperimentType => ExperimentType.TimeMeasurement;
    public bool SupportsDataType => false;
    public bool SupportsMatrixDimensions => false;
    public bool SupportsStepCounting => false;

    private const double X = 1.5;

    public AlgorithmExecutionResult Execute(IAlgorithmInput input)
    {
        var arr = (double[])input.Data;
        _ = Compute(arr); // Прогрев

        var sw = Stopwatch.StartNew();
        _ = Compute(arr);
        sw.Stop();

        return new AlgorithmExecutionResult { TimeMs = sw.Elapsed.TotalMilliseconds, Steps = 0 };
    }

    private static double Compute(double[] v)
    {
        int n = v.Length;
        if (n == 0) return 0.0;
        double result = v[n - 1];
        for (int k = n - 2; k >= 0; k--) result = v[k] + X * result;
        return result;
    }
}