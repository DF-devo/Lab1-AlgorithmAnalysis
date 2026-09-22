namespace Algorithms.PowerAlgorithms;

public class PowerRecursive : IAlgorithm
{
    public string Name => "Power Recursive";
    public string Description => "Рекурсивное возведение в степень: x^n = x * x^(n-1).";
    public ComplexityType TheoreticalComplexity => ComplexityType.Linear;
    public ExperimentType ExperimentType => ExperimentType.StepCounting;
    public bool SupportsDataType => false;
    public bool SupportsMatrixDimensions => false;
    public bool SupportsStepCounting => true;

    private const double X = 2.0;

    public AlgorithmExecutionResult Execute(IAlgorithmInput input)
    {
        int n = input.N;
        int stepCounter = 0;

        double result = PowerRec(X, n, ref stepCounter);

        return new AlgorithmExecutionResult { TimeMs = 0, Steps = stepCounter };
    }

    private static double PowerRec(double x, int n, ref int stepCounter)
    {
        if (n == 0) return 1.0;
        stepCounter++;
        return x * PowerRec(x, n - 1, ref stepCounter);
    }
}