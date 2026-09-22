namespace Algorithms.MatrixOperations;

using Algorithms.DataGenerators;

public class StrassenMultiplication : IAlgorithm
{
    public string Name => "Strassen Multiplication";
    public string Description => "Быстрое умножение матриц алгоритмом Штрассена: O(n^2.807) вместо O(n³).";
    public ComplexityType TheoreticalComplexity => ComplexityType.Quadratic; // ≈ O(n^2.807)
    public ExperimentType ExperimentType => ExperimentType.TimeMeasurement;
    public bool SupportsDataType => false;
    public bool SupportsMatrixDimensions => true;
    public bool SupportsStepCounting => false;

    private const int Threshold = 64; // Порог переключения на классическое умножение

    public AlgorithmExecutionResult Execute(IAlgorithmInput input)
    {
        int n = input.N;
        int m = input.M ?? n;

        // Для Штрассена нужны квадратные матрицы размера степени 2
        int size = NextPowerOfTwo(Math.Max(n, m));

        var A = MatrixGenerator.GenerateRandom(size, size);
        var B = MatrixGenerator.GenerateRandom(size, size);

        // Прогрев
        _ = Multiply(A, B);

        // Замер
        var sw = Stopwatch.StartNew();
        _ = Multiply(A, B);
        sw.Stop();

        return new AlgorithmExecutionResult { TimeMs = sw.Elapsed.TotalMilliseconds, Steps = 0 };
    }

    private static double[,] Multiply(double[,] A, double[,] B)
    {
        int n = A.GetLength(0);
        if (n <= Threshold)
            return ClassicalMultiply(A, B);

        int half = n / 2;

        var A11 = SubMatrix(A, 0, 0, half);
        var A12 = SubMatrix(A, 0, half, half);
        var A21 = SubMatrix(A, half, 0, half);
        var A22 = SubMatrix(A, half, half, half);

        var B11 = SubMatrix(B, 0, 0, half);
        var B12 = SubMatrix(B, 0, half, half);
        var B21 = SubMatrix(B, half, 0, half);
        var B22 = SubMatrix(B, half, half, half);

        var M1 = Multiply(Add(A11, A22), Add(B11, B22));
        var M2 = Multiply(Add(A21, A22), B11);
        var M3 = Multiply(A11, Subtract(B12, B22));
        var M4 = Multiply(A22, Subtract(B21, B11));
        var M5 = Multiply(Add(A11, A12), B22);
        var M6 = Multiply(Subtract(A21, A11), Add(B11, B12));
        var M7 = Multiply(Subtract(A12, A22), Add(B21, B22));

        var C11 = Add(Subtract(Add(M1, M4), M5), M7);
        var C12 = Add(M3, M5);
        var C21 = Add(M2, M4);
        var C22 = Add(Subtract(Add(M1, M3), M2), M6);

        return Merge(C11, C12, C21, C22, n);
    }

    private static double[,] ClassicalMultiply(double[,] A, double[,] B)
    {
        int n = A.GetLength(0);
        var C = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                for (int k = 0; k < n; k++)
                    C[i, j] += A[i, k] * B[k, j];
        return C;
    }

    private static double[,] Add(double[,] A, double[,] B)
    {
        int n = A.GetLength(0);
        var C = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                C[i, j] = A[i, j] + B[i, j];
        return C;
    }

    private static double[,] Subtract(double[,] A, double[,] B)
    {
        int n = A.GetLength(0);
        var C = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                C[i, j] = A[i, j] - B[i, j];
        return C;
    }

    private static double[,] SubMatrix(double[,] A, int rowStart, int colStart, int size)
    {
        var sub = new double[size, size];
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                sub[i, j] = A[rowStart + i, colStart + j];
        return sub;
    }

    private static double[,] Merge(double[,] C11, double[,] C12, double[,] C21, double[,] C22, int n)
    {
        var C = new double[n, n];
        int half = n / 2;
        for (int i = 0; i < half; i++)
            for (int j = 0; j < half; j++)
            {
                C[i, j] = C11[i, j];
                C[i, j + half] = C12[i, j];
                C[i + half, j] = C21[i, j];
                C[i + half, j + half] = C22[i, j];
            }
        return C;
    }

    private static int NextPowerOfTwo(int n)
    {
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }
}