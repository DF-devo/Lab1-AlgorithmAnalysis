namespace Algorithms.VectorOperations;

public class BubbleSort : IAlgorithm
{
    public string Name => "Bubble Sort";
    public string Description => "Сортировка пузырьком с оптимизацией (флаг раннего выхода).";
    public ComplexityType TheoreticalComplexity => ComplexityType.Quadratic;
    public ExperimentType ExperimentType => ExperimentType.TimeMeasurement;
    public bool SupportsDataType => true;
    public bool SupportsMatrixDimensions => false;
    public bool SupportsStepCounting => false;

    public AlgorithmExecutionResult Execute(IAlgorithmInput input)
    {
        var arr = (double[])input.Data;

        var warmup = (double[])arr.Clone();
        Sort(warmup);

        var copy = (double[])arr.Clone();
        var sw = Stopwatch.StartNew();
        Sort(copy);
        sw.Stop();

        return new AlgorithmExecutionResult { TimeMs = sw.Elapsed.TotalMilliseconds, Steps = 0 };
    }

    private static void Sort(double[] arr)
    {
        int n = arr.Length;
        for (int i = 0; i < n - 1; i++)
        {
            bool swapped = false;
            for (int j = 0; j < n - 1 - i; j++)
            {
                if (arr[j] > arr[j + 1])
                {
                    (arr[j], arr[j + 1]) = (arr[j + 1], arr[j]);
                    swapped = true;
                }
            }
            if (!swapped) break;
        }
    }
}