namespace Algorithms.IndividualTask;

public class LevenshteinAlgorithm : IAlgorithm
{
    public string Name => "Levenshtein Distance";
    public string Description => "Расстояние Левенштейна между двумя строками (динамическое программирование).";
    public ComplexityType TheoreticalComplexity => ComplexityType.Quadratic;
    public ExperimentType ExperimentType => ExperimentType.TimeMeasurement;
    public bool SupportsDataType => false;
    public bool SupportsMatrixDimensions => false;
    public bool SupportsStepCounting => false;

    public AlgorithmExecutionResult Execute(IAlgorithmInput input)
    {
        var (s1, s2) = ((string, string))input.Data;

        // Прогрев
        _ = ComputeDistance(s1, s2);

        // Замер
        var sw = Stopwatch.StartNew();
        _ = ComputeDistance(s1, s2);
        sw.Stop();

        return new AlgorithmExecutionResult { TimeMs = sw.Elapsed.TotalMilliseconds, Steps = 0 };
    }

    private static int ComputeDistance(string s1, string s2)
    {
        int n = s1.Length;
        int m = s2.Length;
        var dp = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) dp[i, 0] = i;
        for (int j = 0; j <= m; j++) dp[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                if (s1[i - 1] == s2[j - 1])
                    dp[i, j] = dp[i - 1, j - 1];
                else
                    dp[i, j] = 1 + Math.Min(dp[i - 1, j], Math.Min(dp[i, j - 1], dp[i - 1, j - 1]));
            }
        }

        return dp[n, m];
    }
}