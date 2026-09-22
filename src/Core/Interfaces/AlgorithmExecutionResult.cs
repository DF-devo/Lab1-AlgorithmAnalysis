namespace Core.Interfaces;

/// <summary>
/// Результат выполнения алгоритма, возвращаемый методом Execute.
/// </summary>
public class AlgorithmExecutionResult
{
    /// <summary>
    /// Затраченное время в миллисекундах.
    /// </summary>
    public double TimeMs { get; set; }

    /// <summary>
    /// Количество выполненных шагов (0, если подсчет не поддерживается).
    /// </summary>
    public long Steps { get; set; }

    /// <summary>
    /// Результирующие данные (если алгоритм модифицирует или возвращает данные, например, отсортированный массив).
    /// </summary>
    public object? ResultData { get; set; }
}