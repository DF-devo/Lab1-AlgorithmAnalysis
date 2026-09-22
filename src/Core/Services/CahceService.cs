using System.Diagnostics;
using Core.Interfaces;
using Core.Models;

namespace Core.Services;

public class CacheService : ICacheService
{
    private readonly IDatabaseService _databaseService;

    public CacheService(IDatabaseService databaseService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    }

    public async Task<List<ExperimentResult>> GetOrComputeAsync(
        int algorithmId, int n, DataType? dataType, int? m, int runsCount,
        Func<(Experiment Experiment, List<ExperimentResult> Results)> computeFunc, 
        bool forceRecalculate)
    {
        if (runsCount <= 0) throw new ArgumentException("RunsCount must be greater than 0", nameof(runsCount));

        try
        {
            // 1. Проверяем кэш, если не форсирован пересчет
            if (!forceRecalculate)
            {
                bool hasCache = await _databaseService.HasCachedResultsAsync(algorithmId, n, dataType, m, runsCount);
                if (hasCache)
                {
                    Debug.WriteLine($"[CACHE] Hit for Algo={algorithmId}, N={n}, DataType={dataType}");
                    return await _databaseService.GetCachedResultsAsync(algorithmId, n, dataType, m);
                }
            }

            // 2. Кэш промах или forceRecalculate = true
            Debug.WriteLine($"[CACHE] Miss or Force. Computing for Algo={algorithmId}, N={n}");
            
            // Вызываем функцию вычисления
            var (experiment, newResults) = computeFunc();

            // 3. Сохраняем в БД (добавляем новые запуски, не перезаписывая старые, если они были частично)
            await _databaseService.SaveExperimentAsync(experiment, newResults);
            
            Debug.WriteLine($"[CACHE] Saved {newResults.Count} new results for Algo={algorithmId}, N={n}");
            return newResults;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CACHE ERROR] GetOrComputeAsync failed: {ex.Message}");
            throw;
        }
    }
}