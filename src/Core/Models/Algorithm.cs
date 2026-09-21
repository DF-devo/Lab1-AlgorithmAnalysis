using System.ComponentModel.DataAnnotations;

namespace Core.Models;

/// <summary>
/// Модель алгоритма, подлежащего эмпирическому анализу.
/// </summary>
public class Algorithm
{
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public ComplexityType TheoreticalComplexity { get; set; }

    public ExperimentType ExperimentType { get; set; }

    /// <summary>
    /// Поддерживает ли алгоритм выбор типа входных данных (Random, Sorted, Reversed).
    /// </summary>
    public bool SupportsDataType { get; set; }

    /// <summary>
    /// Требует ли алгоритм указания двух размерностей (N и M), например, для операций с матрицами.
    /// </summary>
    public bool SupportsMatrixDimensions { get; set; }

    /// <summary>
    /// Поддерживает ли алгоритм подсчет шагов выполнения (требуется для Части IV).
    /// </summary>
    public bool SupportsStepCounting { get; set; }
}