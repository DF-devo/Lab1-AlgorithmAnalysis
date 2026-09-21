using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Интерфейс сервиса кэширования результатов экспериментов.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Получает результаты из кэша или вычисляет их заново, если кэш отсутствует или устарел.
    /// </summary>
    /// <param name="algorithmId">Идентификатор алгоритма.</param>
    /// <param name="n">Размер данных.</param>
    /// <param name="dataType">Тип данных.</param>
    /// <param name="m">Вторая размерность (для матриц).</param>
    /// <param name="runsCount">Количество запусков.</param>
    /// <param name="computeFunc">Функция для вычисления результатов, если их нет в кэше.</param>
    /// <param name="forceRecalculate">Флаг принудительного пересчета, игнорирующий кэш.</param>
    /// <returns>Список результатов эксперимента.</returns>
    Task<List<ExperimentResult>> GetOrComputeAsync(
        int algorithmId, 
        int n, 
        DataType? dataType, 
        int? m, 
        int runsCount,
        Func<(Experiment Experiment, List<ExperimentResult> Results)> computeFunc, 
        bool forceRecalculate);
}