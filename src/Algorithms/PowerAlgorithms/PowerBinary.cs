namespace Algorithms.PowerAlgorithms;

public class PowerBinary : IAlgorithm
{
    public string Name => "Power Binary";
    public string Description => "Быстрое возведение в степень через двоичное представление: O(log n) умножений.";
    public ComplexityType TheoreticalComplexity => ComplexityType.Logarithmic;
    public ExperimentType ExperimentType => ExperimentType.StepCounting;
    public bool SupportsDataType => false;
    public bool SupportsMatrixDimensions => false;
    public bool SupportsStepCounting => true;

    private const double X = 2.0;

    public AlgorithmExecutionResult Execute(IAlgorithmInput input)
    {
        int n = input.N;
        int stepCounter = 0;

        double result = PowerBin(X, n, ref stepCounter);

        return new AlgorithmExecutionResult { TimeMs = 0, Steps = stepCounter };
    }

    private static double PowerBin(double x, int n, ref int stepCounter)
    {
        if (n == 0) return 1.0;
        if (n % 2 == 0)
        {
            double half = PowerBin(x, n / 2, ref stepCounter);
            stepCounter++; // умножение half * half
            return half * half;
        }
        else
        {
            stepCounter++; // умножение x * PowerBin(x, n-1)
            return x * PowerBin(x, n - 1, ref stepCounter);
        }
    }
}