using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Базовый интерфейс для всех анализируемых алгоритмов.
/// </summary>
public interface IAlgorithm
{
    string Name { get; }
    string Description { get; }
    ComplexityType TheoreticalComplexity { get; }
    ExperimentType ExperimentType { get; }
    bool SupportsDataType { get; }
    bool SupportsMatrixDimensions { get; }
    bool SupportsStepCounting { get; }

    /// <summary>
    /// Выполняет алгоритм на предоставленных входных данных.
    /// </summary>
    /// <param name="input">Входные данные и параметры запуска.</param>
    /// <returns>Результат выполнения, включающий метрики и выходные данные.</returns>
    AlgorithmExecutionResult Execute(IAlgorithmInput input);
}