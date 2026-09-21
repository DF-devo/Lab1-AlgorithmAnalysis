using Core.Models;

namespace Core.Services;

public interface IDataGenerator
{
    object Generate(int n, int? m, DataType? dataType);
}

public class DataGenerator : IDataGenerator
{
    private readonly Random _random = new();

    public object Generate(int n, int? m, DataType? dataType)
    {
        if (m.HasValue)
        {
            // Генерация матрицы N x M
            var matrix = new double[n, m.Value];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m.Value; j++)
                    matrix[i, j] = _random.NextDouble() * 100;
            return matrix;
        }

        // Генерация массива
        var array = new int[n];
        for (int i = 0; i < n; i++) array[i] = _random.Next(1, 10000);

        return dataType switch
        {
            DataType.Sorted => array.OrderBy(x => x).ToArray(),
            DataType.Reversed => array.OrderByDescending(x => x).ToArray(),
            _ => array // Random по умолчанию
        };
    }
}