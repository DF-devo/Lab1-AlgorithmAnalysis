namespace Algorithms.IndividualTask;

public class RabinKarpAlgorithm : IAlgorithm
{
    public string Name => "Rabin-Karp Algorithm";
    public string Description => "Поиск подстроки через скользящий хеш. O(n+m) в среднем.";
    public ComplexityType TheoreticalComplexity => ComplexityType.Linear;
    public ExperimentType ExperimentType => ExperimentType.TimeMeasurement;
    public bool SupportsDataType => false;
    public bool SupportsMatrixDimensions => false;
    public bool SupportsStepCounting => false;

    private const long BASE = 256;
    private const long MOD = 1_000_000_007;

    public AlgorithmExecutionResult Execute(IAlgorithmInput input)
    {
        var (text, pattern) = ((string, string))input.Data;

        // Прогрев
        _ = Search(text, pattern);

        // Замер
        var sw = Stopwatch.StartNew();
        _ = Search(text, pattern);
        sw.Stop();

        return new AlgorithmExecutionResult { TimeMs = sw.Elapsed.TotalMilliseconds, Steps = 0 };
    }

    private static List<int> Search(string text, string pattern)
    {
        var result = new List<int>();
        int n = text.Length;
        int m = pattern.Length;

        if (m == 0 || n < m) return result;

        // Вычисляем BASE^(m-1) mod MOD
        long h = 1;
        for (int i = 0; i < m - 1; i++)
            h = (h * BASE) % MOD;

        // Вычисляем начальные хеши
        long patternHash = 0;
        long textHash = 0;
        for (int i = 0; i < m; i++)
        {
            patternHash = (BASE * patternHash + pattern[i]) % MOD;
            textHash = (BASE * textHash + text[i]) % MOD;
        }

        // Скользящее окно
        for (int i = 0; i <= n - m; i++)
        {
            // Если хеши совпадают — проверяем посимвольно
            if (patternHash == textHash)
            {
                bool match = true;
                for (int j = 0; j < m; j++)
                {
                    if (text[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) result.Add(i);
            }

            // Обновляем хеш для следующего окна
            if (i < n - m)
            {
                textHash = (BASE * (textHash - text[i] * h) + text[i + m]) % MOD;
                if (textHash < 0) textHash += MOD;
            }
        }

        return result;
    }
}
