using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Core.Data;

/// <summary>
/// Реализация сервиса доступа к данным через EF Core.
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public DatabaseService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task SaveExperimentAsync(Experiment exp, List<ExperimentResult> results)
    {
        if (exp == null) throw new ArgumentNullException(nameof(exp));
        if (results == null || results.Count == 0) throw new ArgumentException("Результаты не могут быть null или пустыми", nameof(results));

        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. Сначала сохраняем эксперимент, чтобы EF Core сгенерировал для него Id
                context.Algorithms.Attach(exp.Algorithm);
                context.Experiments.Add(exp);
                await context.SaveChangesAsync(); // <-- ТЕПЕРЬ exp.Id ЗАПОЛНЕН!

                // 2. Явно проставляем внешние ключи и денормализованные поля
                foreach (var result in results)
                {
                    result.ExperimentId = exp.Id;       // <-- КРИТИЧЕСКИ ВАЖНО для связи
                    result.AlgorithmId = exp.AlgorithmId;
                    result.DataType = exp.DataType;
                }

                // 3. Сохраняем результаты
                context.ExperimentResults.AddRange(results);
                await context.SaveChangesAsync();

                await transaction.CommitAsync();
                Debug.WriteLine($"[DB] Успешно сохранен эксперимент ID={exp.Id} с {results.Count} результатами.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Debug.WriteLine($"[DB ERROR] Ошибка транзакции при сохранении: {ex.Message}");
                throw;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DB ERROR] SaveExperimentAsync завершился с ошибкой: {ex.Message}");
            throw;
        }
    }

    public async Task<List<Experiment>> GetExperimentsByAlgorithmAsync(int algorithmId)
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Experiments
                .Where(e => e.AlgorithmId == algorithmId)
                .OrderByDescending(e => e.Date)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DB ERROR] GetExperimentsByAlgorithmAsync: {ex.Message}");
            return new List<Experiment>();
        }
    }

    public async Task<List<ExperimentResult>> GetCachedResultsAsync(int algorithmId, int n, DataType? dataType, int? m)
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Запрос использует составной индекс IX_ExperimentResult_CacheLookup
            return await context.ExperimentResults
                .Where(er => er.AlgorithmId == algorithmId
                          && er.N == n
                          && er.DataType == dataType
                          && er.M == m)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DB ERROR] GetCachedResultsAsync: {ex.Message}");
            return new List<ExperimentResult>();
        }
    }

    public async Task<bool> HasCachedResultsAsync(int algorithmId, int n, DataType? dataType, int? m, int runsCount)
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Считаем количество записей, попадающих под критерии кэша
            int count = await context.ExperimentResults
                .CountAsync(er => er.AlgorithmId == algorithmId
                               && er.N == n
                               && er.DataType == dataType
                               && er.M == m);

            return count >= runsCount;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DB ERROR] HasCachedResultsAsync: {ex.Message}");
            return false;
        }
    }

    public async Task<List<ExperimentResult>> GetResultsForExperimentAsync(int experimentId)
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.ExperimentResults
                .Where(er => er.ExperimentId == experimentId)
                .OrderBy(er => er.N)
                .ThenBy(er => er.RunNumber)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DB ERROR] GetResultsForExperimentAsync: {ex.Message}");
            return new List<ExperimentResult>();
        }
    }
}