namespace Algorithms.MatrixOperations;

using Algorithms.DataGenerators;

public class MatrixMultiplication : IAlgorithm
{
    public string Name => "Matrix Multiplication";
    public string Description => "Классическое умножение матриц A(n×m) × B(m×n) = C(n×n) тройным циклом.";
    public ComplexityType TheoreticalComplexity => ComplexityType.Cubic;
    public ExperimentType ExperimentType => ExperimentType.TimeMeasurement;
    public bool SupportsDataType => false;
    public bool SupportsMatrixDimensions => true;
    public bool SupportsStepCounting => false;

    public AlgorithmExecutionResult Execute(IAlgorithmInput input)
    {
        int n = input.N;
        int m = input.M ?? n;

        // Генерируем матрицы
        var A = MatrixGenerator.GenerateRandom(n, m);
        var B = MatrixGenerator.GenerateRandom(m, n);

        // Прогрев
        _ = Multiply(A, B, n, m);

        // Замер
        var sw = Stopwatch.StartNew();
        _ = Multiply(A, B, n, m);
        sw.Stop();

        return new AlgorithmExecutionResult { TimeMs = sw.Elapsed.TotalMilliseconds, Steps = 0 };
    }

    private static double[,] Multiply(double[,] A, double[,] B, int n, int m)
    {
        var C = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                for (int k = 0; k < m; k++)
                    C[i, j] += A[i, k] * B[k, j];
        return C;
    }
}