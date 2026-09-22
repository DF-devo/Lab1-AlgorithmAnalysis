using Core.Models;

namespace Core.Services;

/// <summary>
/// Интерфейс для генерации входных данных алгоритмов.
/// </summary>
public interface IDataGenerator
{
    /// <summary>
    /// Генерирует входные данные для алгоритма.
    /// </summary>
    /// <param name="n">Размер данных (количество элементов).</param>
    /// <param name="m">Вторая размерность (для матриц), nullable.</param>
    /// <param name="dataType">Тип данных (Random, Sorted, Reversed), nullable.</param>
    /// <returns>Сгенерированные данные (массив, матрица и т.д.).</returns>
    object Generate(int n, int? m, DataType? dataType);
}