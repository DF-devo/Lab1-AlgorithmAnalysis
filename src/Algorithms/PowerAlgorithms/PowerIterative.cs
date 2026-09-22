namespace Algorithms.PowerAlgorithms;

public class PowerIterative : IAlgorithm
{
    public string Name => "Power Iterative";
    public string Description => "Итеративное возведение в степень: n умножений в цикле.";
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

        double result = 1.0;
        for (int i = 0; i < n; i++)
        {
            result *= X;
            stepCounter++;
        }

        return new AlgorithmExecutionResult { TimeMs = 0, Steps = stepCounter };
    }
}