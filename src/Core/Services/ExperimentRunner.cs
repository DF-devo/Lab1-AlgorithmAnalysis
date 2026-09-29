using Core.Data;
using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Core.Services;

public class ExperimentRunner
{
    private readonly ICacheService _cacheService;
    private readonly IDatabaseService _databaseService;
    private readonly IDataGenerator _dataGenerator;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ExperimentRunner(
        ICacheService cacheService,
        IDatabaseService databaseService,
        IDataGenerator dataGenerator,
        IDbContextFactory<AppDbContext> contextFactory)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _dataGenerator = dataGenerator ?? throw new ArgumentNullException(nameof(dataGenerator));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<List<ExperimentResult>> RunExperimentAsync(
        IAlgorithm algorithm, int n, int? m, DataType? dataType, int runsCount, bool forceRecalculate)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "N must be greater than 0");
        if (runsCount <= 0) throw new ArgumentOutOfRangeException(nameof(runsCount), "RunsCount must be greater than 0");

        // Реальный Id из БД: без него запросы кэша и внешний ключ Experiment ссылаются на несуществующий Algorithm
        var dbAlgorithm = await ResolveDbAlgorithmAsync(algorithm);

        return await _cacheService.GetOrComputeAsync(
            algorithmId: dbAlgorithm.Id,
            n: n,
            dataType: dataType,
            m: m,
            runsCount: runsCount,
            computeFunc: () => ComputeExperiment(algorithm, dbAlgorithm, n, m, dataType, runsCount),
            forceRecalculate: forceRecalculate
        );
    }

    /// <summary>
    /// Находит запись алгоритма в таблице Algorithms по имени или создаёт её.
    /// Возвращает сущность с заполненным Id — она нужна и для кэша, и для Experiment.Algorithm.
    /// </summary>
    private async Task<Algorithm> ResolveDbAlgorithmAsync(IAlgorithm algorithm)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var existing = await context.Algorithms.FirstOrDefaultAsync(a => a.Name == algorithm.Name);
        if (existing is not null)
            return existing;

        var entity = new Algorithm
        {
            Name = algorithm.Name,
            Description = algorithm.Description,
            TheoreticalComplexity = algorithm.TheoreticalComplexity,
            ExperimentType = algorithm.ExperimentType,
            SupportsDataType = algorithm.SupportsDataType,
            SupportsMatrixDimensions = algorithm.SupportsMatrixDimensions,
            SupportsStepCounting = algorithm.SupportsStepCounting
        };

        context.Algorithms.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    private (Experiment Experiment, List<ExperimentResult> Results) ComputeExperiment(
        IAlgorithm algorithm, Algorithm dbAlgorithm, int n, int? m, DataType? dataType, int runsCount)
    {
        var results = new List<ExperimentResult>();
        var experiment = new Experiment
        {
            AlgorithmId = dbAlgorithm.Id,
            Algorithm = dbAlgorithm, // DatabaseService.attach-ит эту сущность при сохранении
            Date = DateTime.UtcNow,
            N_max = n,
            Step = n,
            RunsCount = runsCount,
            ForceRecalculate = false,
            DataType = dataType
        };

        for (int run = 1; run <= runsCount; run++)
        {
            var inputData = new AlgorithmInputStub(n, m, dataType, _dataGenerator.Generate(n, m, dataType));
            var executionResult = algorithm.Execute(inputData);

            results.Add(new ExperimentResult
            {
                N = n,
                M = m,
                RunNumber = run,
                TimeMs = executionResult.TimeMs,
                Steps = executionResult.Steps
            });
        }

        return (experiment, results);
    }

    public async Task<List<ExperimentResult>> RunExperimentSeriesAsync(
        IAlgorithm algorithm, int nMax, int step, int runsCount,
        DataType? dataType, bool forceRecalculate, IProgress<double>? progress = null)
    {
        if (nMax <= 0) throw new ArgumentOutOfRangeException(nameof(nMax));
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (runsCount <= 0) throw new ArgumentOutOfRangeException(nameof(runsCount));

        var allResults = new List<ExperimentResult>();
        int totalSteps = nMax / step;
        int currentStep = 0;

        for (int n = step; n <= nMax; n += step)
        {
            var results = await RunExperimentAsync(algorithm, n, null, dataType, runsCount, forceRecalculate);
            allResults.AddRange(results);

            currentStep++;
            progress?.Report((double)currentStep / totalSteps * 100.0);
        }

        return allResults;
    }
}

// Простая реализация IAlgorithmInput для внутреннего использования
internal class AlgorithmInputStub : Core.Interfaces.IAlgorithmInput
{
    public int N { get; }
    public int? M { get; }
    public DataType? DataType { get; }
    public object Data { get; }

    public AlgorithmInputStub(int n, int? m, DataType? dataType, object data)
    {
        N = n; M = m; DataType = dataType; Data = data;
    }
}
