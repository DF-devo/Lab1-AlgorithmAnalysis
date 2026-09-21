using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Интерфейс, описывающий входные данные для выполнения алгоритма.
/// </summary>
public interface IAlgorithmInput
{
    int N { get; }
    int? M { get; }
    DataType? DataType { get; }
    
    /// <summary>
    /// Сами данные (например, int[], double[,] и т.д.).
    /// </summary>
    object Data { get; }
}