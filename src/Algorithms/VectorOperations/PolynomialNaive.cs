namespace Algorithms.VectorOperations;

public class PolynomialNaive : IAlgorithm
{
    public string Name => "Polynomial Naive";
    public string Description => "Наивное вычисление многочлена P(x) при x=1.5. Степень вычисляется в цикле.";
    public ComplexityType TheoreticalComplexity => ComplexityType.Quadratic;
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
        if (v.Length == 0) return 0.0;
        double sum = 0;
        int n = v.Length;
        for (int k = 1; k <= n; k++)
        {
            double power = 1.0;
            for (int j = 0; j < k - 1; j++) power *= X; // Квадратичность здесь
            sum += v[k - 1] * power;
        }
        return sum;
    }
}