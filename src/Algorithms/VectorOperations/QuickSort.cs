namespace Algorithms.VectorOperations;

public class QuickSort : IAlgorithm
{
    public string Name => "Quick Sort";
    public string Description => "Быстрая сортировка с рандомизированным опорным элементом (схема Lomuto).";
    public ComplexityType TheoreticalComplexity => ComplexityType.Linearithmic;
    public ExperimentType ExperimentType => ExperimentType.TimeMeasurement;
    public bool SupportsDataType => true;
    public bool SupportsMatrixDimensions => false;
    public bool SupportsStepCounting => false;

    private static readonly Random Rng = new Random(42);

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
        if (arr.Length <= 1) return;
        QuickSortInternal(arr, 0, arr.Length - 1);
    }

    private static void QuickSortInternal(double[] arr, int low, int high)
    {
        if (low < high)
        {
            int pi = Partition(arr, low, high);
            QuickSortInternal(arr, low, pi - 1);
            QuickSortInternal(arr, pi + 1, high);
        }
    }

    private static int Partition(double[] arr, int low, int high)
    {
        int pivotIndex = low + Rng.Next(high - low + 1);
        (arr[pivotIndex], arr[high]) = (arr[high], arr[pivotIndex]);

        double pivot = arr[high];
        int i = low - 1;
        for (int j = low; j < high; j++)
        {
            if (arr[j] <= pivot)
            {
                i++;
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }
        (arr[i + 1], arr[high]) = (arr[high], arr[i + 1]);
        return i + 1;
    }
}