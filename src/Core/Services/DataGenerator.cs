using System;
using Core.Models;

namespace Core.Services;

/// <summary>
/// Реализация генератора входных данных для алгоритмов.
/// </summary>
public class DataGenerator : IDataGenerator
{
    private readonly Random _random = new();

    public object Generate(int n, int? m, DataType? dataType)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "Размер данных N должен быть больше 0");

        // Если указана вторая размерность M — генерируем матрицу
        if (m.HasValue)
        {
            if (m.Value <= 0) throw new ArgumentOutOfRangeException(nameof(m), "Размер M должен быть больше 0");

            var matrix = new double[n, m.Value];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m.Value; j++)
                {
                    matrix[i, j] = _random.NextDouble() * 100;
                }
            }
            return matrix;
        }

        // Иначе генерируем одномерный массив
        var array = new int[n];
        for (int i = 0; i < n; i++)
        {
            array[i] = _random.Next(1, 10000);
        }

        // Применяем сортировку в зависимости от типа данных
        return dataType switch
        {
            DataType.Sorted => SortArray(array, ascending: true),
            DataType.Reversed => SortArray(array, ascending: false),
            _ => array // DataType.Random или null
        };
    }

    /// <summary>
    /// Сортирует массив по возрастанию или убыванию.
    /// </summary>
    private int[] SortArray(int[] array, bool ascending)
    {
        var sorted = new int[array.Length];
        Array.Copy(array, sorted, array.Length);

        Array.Sort(sorted);

        if (!ascending)
        {
            Array.Reverse(sorted);
        }

        return sorted;
    }
}