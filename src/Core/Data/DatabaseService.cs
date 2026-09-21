// ... GetCachedResultsAsync обновлен:
public async Task<List<ExperimentResult>> GetCachedResultsAsync(int algorithmId, int n, DataType? dataType, int? m)
{
    try
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ExperimentResults
            .Where(er => er.AlgorithmId == algorithmId
                      && er.N == n
                      && er.DataType == dataType
                      && er.M == m)
            .ToListAsync();
    }
    catch (Exception ex) { /* логирование */ return new List<ExperimentResult>(); }
}

// ... HasCachedResultsAsync обновлен аналогично:
public async Task<bool> HasCachedResultsAsync(int algorithmId, int n, DataType? dataType, int? m, int runsCount)
{
    try
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        int count = await context.ExperimentResults
            .CountAsync(er => er.AlgorithmId == algorithmId
                           && er.N == n
                           && er.DataType == dataType
                           && er.M == m);
        return count >= runsCount;
    }
    catch (Exception ex) { /* логирование */ return false; }
}

// ... SaveExperimentAsync обновлен (убраны shadow properties):
public async Task SaveExperimentAsync(Experiment exp, List<ExperimentResult> results)
{
    // ... валидация ...
    using var context = await _contextFactory.CreateDbContextAsync();
    using var transaction = await context.Database.BeginTransactionAsync();
    try
    {
        foreach (var result in results)
        {
            result.AlgorithmId = exp.AlgorithmId;
            result.DataType = exp.DataType;
        }
        context.Algorithms.Attach(exp.Algorithm);
        context.Experiments.Add(exp);
        context.ExperimentResults.AddRange(results);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch { await transaction.RollbackAsync(); throw; }
}