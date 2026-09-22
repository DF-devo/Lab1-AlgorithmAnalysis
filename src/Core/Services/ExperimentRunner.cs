using System.Diagnostics;
using Core.Interfaces;
using Core.Models;

namespace Core.Services;

public class ExperimentRunner
{
    private readonly ICacheService _cacheService;
    private readonly IDatabaseService _databaseService;
    private readonly IDataGenerator _dataGenerator;

    public ExperimentRunner(ICacheService cacheService, IDatabaseService databaseService, IDataGenerator dataGenerator)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _dataGenerator = dataGenerator ?? throw new ArgumentNullException(nameof(dataGenerator));
    }

    public async Task<List<ExperimentResult>> RunExperimentAsync(
        IAlgorithm algorithm, int n, int? m, DataType? dataType, int runsCount, bool forceRecalculate)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "N must be greater than 0");
        if (runsCount <= 0) throw new ArgumentOutOfRangeException(nameof(runsCount), "RunsCount must be greater than 0");

        return await _cacheService.GetOrComputeAsync(
            algorithmId: 0, // Будет перезаписано в DatabaseService при сохранении, или можно передать реальный ID из БД
            n: n,
            dataType: dataType,
            m: m,
            runsCount: runsCount,
            computeFunc: () => ComputeExperiment(algorithm, n, m, dataType, runsCount),
            forceRecalculate: forceRecalculate
        );
    }

    private (Experiment Experiment, List<ExperimentResult> Results) ComputeExperiment(
        IAlgorithm algorithm, int n, int? m, DataType? dataType, int runsCount)
    {
        var results = new List<ExperimentResult>();
        var experiment = new Experiment
        {
            // AlgorithmId должен быть установлен вызывающим кодом, здесь заглушка
            AlgorithmId = 0, 
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