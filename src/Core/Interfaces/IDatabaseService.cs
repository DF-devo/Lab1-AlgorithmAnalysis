using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Интерфейс сервиса для работы с базой данных (SQLite через EF Core).
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Сохраняет эксперимент и его результаты в базу данных в рамках одной транзакции.
    /// </summary>
    Task SaveExperimentAsync(Experiment exp, List<ExperimentResult> results);

    /// <summary>
    /// Получает список всех экспериментов для указанного алгоритма.
    /// </summary>
    Task<List<Experiment>> GetExperimentsByAlgorithmAsync(int algorithmId);

    /// <summary>
    /// Получает закэшированные результаты для конкретных параметров (без учета RunsCount).
    /// </summary>
    Task<List<ExperimentResult>> GetCachedResultsAsync(int algorithmId, int n, DataType? dataType, int? m);

    /// <summary>
    /// Проверяет наличие полного набора закэшированных результатов для заданных параметров и количества запусков.
    /// </summary>
    Task<bool> HasCachedResultsAsync(int algorithmId, int n, DataType? dataType, int? m, int runsCount);

    /// <summary>
    /// Получает все результаты, привязанные к конкретному эксперименту.
    /// </summary>
    Task<List<ExperimentResult>> GetResultsForExperimentAsync(int experimentId);
}