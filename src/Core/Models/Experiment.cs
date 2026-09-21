using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models;

/// <summary>
/// Модель эксперимента, описывающая параметры серии замеров.
/// </summary>
public class Experiment
{
    public int Id { get; set; }

    public int AlgorithmId { get; set; }

    [ForeignKey(nameof(AlgorithmId))]
    public Algorithm Algorithm { get; set; } = null!;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Максимальный размер входных данных.
    /// </summary>
    public int N_max { get; set; }

    /// <summary>
    /// Шаг изменения размера входных данных.
    /// </summary>
    public int Step { get; set; }

    /// <summary>
    /// Количество повторных запусков для каждого размера N (для усреднения).
    /// </summary>
    public int RunsCount { get; set; }

    /// <summary>
    /// Игнорировать кэш и принудительно пересчитать результаты.
    /// </summary>
    public bool ForceRecalculate { get; set; }

    /// <summary>
    /// Тип входных данных (nullable, так как применимо не ко всем алгоритмам).
    /// </summary>
    public DataType? DataType { get; set; }
}