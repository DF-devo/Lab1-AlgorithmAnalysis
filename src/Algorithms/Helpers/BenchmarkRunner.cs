namespace Algorithms.Helpers;

using Core.Interfaces;
using System.Diagnostics;

public class BenchmarkRunner
{
    private const int MinRuns = 10;
    private const int MaxRuns = 1000;
    private const double TargetDurationMs = 50.0; // Целевое время замера

    public static async Task<List<AlgorithmExecutionResult>> RunAsync(
        IAlgorithm algorithm,
        IAlgorithmInput input,
        int initialRuns = MinRuns,
        CancellationToken cancellationToken = default)
    {
        var results = new List<AlgorithmExecutionResult>();

        // Прогрев JIT
        _ = algorithm.Execute(input);

        int runs = initialRuns;
        double totalMs = 0;

        while (runs <= MaxRuns && totalMs < TargetDurationMs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchResults = new List<AlgorithmExecutionResult>();
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < runs; i++)
            {
                var result = algorithm.Execute(input);
                batchResults.Add(result);
            }

            sw.Stop();
            totalMs = sw.Elapsed.TotalMilliseconds;

            results.AddRange(batchResults);

            // Если замер слишком короткий — увеличиваем количество прогонов
            if (totalMs < TargetDurationMs / 2 && runs < MaxRuns)
            {
                runs = Math.Min(runs * 2, MaxRuns);
            }
            else
            {
                break;
            }
        }

        return results;
    }

    public static ExperimentSummary Summarize(List<AlgorithmExecutionResult> results)
    {
        if (results.Count == 0)
            return new ExperimentSummary();

        var times = results.Select(r => r.TimeMs).OrderBy(t => t).ToList();
        var steps = results.Select(r => r.Steps).ToList();

        return new ExperimentSummary
        {
            Count = results.Count,
            MeanTimeMs = times.Average(),
            MedianTimeMs = times[times.Count / 2],
            MinTimeMs = times.First(),
            MaxTimeMs = times.Last(),
            StdDevTimeMs = CalculateStdDev(times),
            MeanSteps = steps.Any() ? steps.Average() : 0
        };
    }

    private static double CalculateStdDev(List<double> values)
    {
        if (values.Count <= 1) return 0;
        double avg = values.Average();
        double sumOfSquares = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }
}

public class ExperimentSummary
{
    public int Count { get; set; }
    public double MeanTimeMs { get; set; }
    public double MedianTimeMs { get; set; }
    public double MinTimeMs { get; set; }
    public double MaxTimeMs { get; set; }
    public double StdDevTimeMs { get; set; }
    public double MeanSteps { get; set; }
}