namespace Algorithms.DataGenerators;

public static class MatrixGenerator
{
    public static double[,] GenerateRandom(int n, int m, Random? rng = null)
    {
        rng ??= new Random();
        var matrix = new double[n, m];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                matrix[i, j] = rng.NextDouble() * 100;
        return matrix;
    }
}