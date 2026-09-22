namespace Algorithms.VectorOperations;

public class ConstantFunction : IAlgorithm
{
    public string Name => "Constant Function";
    public string Description => "Возвращает константу 1. Используется для замера накладных расходов вызова метода.";
    public ComplexityType TheoreticalComplexity => ComplexityType.Constant;
    public ExperimentType ExperimentType => ExperimentType.TimeMeasurement;
    public bool SupportsDataType => false;
    public bool SupportsMatrixDimensions => false;
    public bool SupportsStepCounting => false;

    public AlgorithmExecutionResult Execute(IAlgorithmInput input)
    {
        _ = Compute(); // Прогрев JIT
        var sw = Stopwatch.StartNew();
        _ = Compute();
        sw.Stop();

        return new AlgorithmExecutionResult { TimeMs = sw.Elapsed.TotalMilliseconds, Steps = 0 };
    }

    private static int Compute() => 1;
}