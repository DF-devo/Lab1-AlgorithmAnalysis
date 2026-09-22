namespace Algorithms.Helpers;

using Core.Interfaces;
using System.Collections.Concurrent;

public class ParallelExperimentRunner
{
    private readonly int _maxDegreeOfParallelism;

    // ИСПРАВЛЕНО: 4 - это константа времени компиляции. Environment.ProcessorCount использовать нельзя.
    public ParallelExperimentRunner(int maxDegreeOfParallelism = 4)
    {
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    public async Task<ConcurrentDictionary<Guid, ExperimentSummary>> RunBatchAsync(
        Dictionary<Guid, (IAlgorithm Algorithm, IAlgorithmInput Input)> experiments,
        int runsPerExperiment = 10,
        CancellationToken cancellationToken = default)
    {
        var results = new ConcurrentDictionary<Guid, ExperimentSummary>();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(experiments, options, async (kvp, ct) =>
        {
            var (id, (algorithm, input)) = kvp;
            var runResults = await BenchmarkRunner.RunAsync(algorithm, input, runsPerExperiment, ct);
            var summary = BenchmarkRunner.Summarize(runResults);
            results[id] = summary;
        });

        return results;
    }
}