namespace Algorithms.DataGenerators;

using Core;

public static class VectorGenerator
{
    public static double[] GenerateRandom(int n, Random? rng = null)
    {
        rng ??= new Random();
        var arr = new double[n];
        for (int i = 0; i < n; i++)
            arr[i] = rng.NextDouble();
        return arr;
    }

    public static double[] GenerateSorted(int n)
    {
        var arr = new double[n];
        for (int i = 0; i < n; i++)
            arr[i] = i + 1; // 1, 2, ..., n
        return arr;
    }

    public static double[] GenerateReversed(int n)
    {
        var arr = new double[n];
        for (int i = 0; i < n; i++)
            arr[i] = n - i; // n, n-1, ..., 1
        return arr;
    }

    public static double[] GenerateForDataType(int n, DataType dataType)
    {
        return dataType switch
        {
            DataType.Random => GenerateRandom(n),
            DataType.Sorted => GenerateSorted(n),
            DataType.Reversed => GenerateReversed(n),
            _ => throw new ArgumentOutOfRangeException(
                nameof(dataType), dataType, "Неизвестный тип данных")
        };
    }
}