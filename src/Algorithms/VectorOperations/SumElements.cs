namespace Algorithms.VectorOperations;

public class SumElements : IAlgorithm
{
    public string Name => "Sum Elements";
    public string Description => "Вычисляет сумму всех элементов вектора за один проход.";
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
        double sum = 0;
        for (int i = 0; i < arr.Length; i++) sum += arr[i];
        return sum;
    }
}