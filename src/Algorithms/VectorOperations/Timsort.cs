namespace Algorithms.VectorOperations;

public class Timsort : IAlgorithm
{
    public string Name => "Timsort (Array.Sort)";
    public string Description => "Стандартная сортировка массива .NET (IntroSort, гибрид QuickSort, HeapSort и InsertionSort).";
    public ComplexityType TheoreticalComplexity => ComplexityType.Linearithmic;
    public ExperimentType ExperimentType => ExperimentType.TimeMeasurement;
    public bool SupportsDataType => true;
    public bool SupportsMatrixDimensions => false;
    public bool SupportsStepCounting => false;

    public AlgorithmExecutionResult Execute(IAlgorithmInput input)
    {
        var arr = (double[])input.Data;

        var warmup = (double[])arr.Clone();
        Array.Sort(warmup);

        var copy = (double[])arr.Clone();
        var sw = Stopwatch.StartNew();
        Array.Sort(copy);
        sw.Stop();

        return new AlgorithmExecutionResult { TimeMs = sw.Elapsed.TotalMilliseconds, Steps = 0 };
    }
}