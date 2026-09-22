namespace Algorithms.IndividualTask;

public class BoyerMooreAlgorithm : IAlgorithm
{
    public string Name => "Boyer-Moore Algorithm";
    public string Description => "Поиск подстроки через эвристики bad character и good suffix. O(n) в лучшем случае.";
    public ComplexityType TheoreticalComplexity => ComplexityType.Linear;
    public ExperimentType ExperimentType => ExperimentType.TimeMeasurement;
    public bool SupportsDataType => false;
    public bool SupportsMatrixDimensions => false;
    public bool SupportsStepCounting => false;

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

        var badChar = ComputeBadCharacterShift(pattern);
        var goodSuffix = ComputeGoodSuffixShift(pattern);

        int s = 0; // Сдвиг паттерна относительно текста
        while (s <= n - m)
        {
            int j = m - 1;

            // Сравниваем справа налево
            while (j >= 0 && pattern[j] == text[s + j])
            {
                j--;
            }

            if (j < 0)
            {
                // Найдено полное совпадение
                result.Add(s);
                s += goodSuffix[0];
            }
            else
            {
                // Вычисляем сдвиг по правилу плохого символа
                int badCharShift = j - badChar.GetValueOrDefault(text[s + j], -1);

                // Выбираем максимальный безопасный сдвиг
                s += Math.Max(goodSuffix[j + 1], badCharShift);
            }
        }

        return result;
    }

    private static Dictionary<char, int> ComputeBadCharacterShift(string pattern)
    {
        var table = new Dictionary<char, int>();
        for (int i = 0; i < pattern.Length; i++)
        {
            table[pattern[i]] = i;
        }
        return table;
    }

    // Классическая, безопасная реализация правила хорошего суффикса (алгоритм Гасфилда)
    private static int[] ComputeGoodSuffixShift(string pattern)
    {
        int m = pattern.Length;
        int[] shift = new int[m + 1];
        int[] border = new int[m + 1];

        int i = m, j = m + 1;
        border[i] = j;

        while (i > 0)
        {
            while (j <= m && pattern[i - 1] != pattern[j - 1])
            {
                if (shift[j] == 0)
                    shift[j] = j - i;
                j = border[j];
            }
            i--;
            j--;
            border[i] = j;
        }

        j = border[0];
        for (i = 0; i <= m; i++)
        {
            if (shift[i] == 0)
                shift[i] = j;
            if (i == j)
                j = border[j];
        }

        return shift;
    }
}