namespace Algorithms.VectorOperations;

public class ProductElements : IAlgorithm
{
    public string Name => "Product Elements";
    public string Description => "Вычисляет произведение всех элементов вектора за один проход.";
    public ComplexityType TheoreticalComplexity => ComplexityType.Linear;
    public ExperimentType ExperimentType => ExperimentType.TimeMeasurement;
    public bool SupportsDataType => false;
    public bool SupportsMatrixDimensions => false;
    public bool SupportsStepCounting => false;

    public AlgorithmExecutionResult Execute(IAlgorithmInput input)
    {
        var arr = (double[])input.Data;
        _ = Compute(arr); // Прогрев

        var sw = Stopwatch.StartNew();
        _ = Compute(arr);
        sw.Stop();

        return new AlgorithmExecutionResult { TimeMs = sw.Elapsed.TotalMilliseconds, Steps = 0 };
    }

    private static double Compute(double[] arr)
    {
        if (arr.Length == 0) return 1.0;
        double product = 1.0;
        for (int i = 0; i < arr.Length; i++) product *= arr[i];
        return product;
    }
}