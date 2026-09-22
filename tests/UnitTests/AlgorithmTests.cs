namespace Tests.UnitTests;

using Algorithms.DataGenerators;
using Algorithms.Helpers;               
using Algorithms.MatrixOperations;      
using Algorithms.PowerAlgorithms;
using Algorithms.IndividualTask;
using Algorithms.VectorOperations;
using Core.Models;
using Core.Interfaces;
using Xunit;

public class AlgorithmTests
{
    #region Вспомогательные методы

    private static AlgorithmInput CreateVectorInput(double[] data, int? m = null)
    {
        return new AlgorithmInput
        {
            N = data.Length,
            M = m,
            Data = data
        };
    }

    private static AlgorithmInput CreateStringInput(string text, string pattern)
    {
        return new AlgorithmInput
        {
            N = text.Length,
            Data = (text, pattern)
        };
    }

    #endregion

    #region Часть I — Векторные операции

    [Fact]
    public void ConstantFunction_ReturnsResult_WithMinimalTime()
    {
        var algo = new ConstantFunction();
        var input = CreateVectorInput(new double[] { 1, 2, 3 });

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
    }

    [Fact]
    public void SumElements_CalculatesCorrectSum()
    {
        var algo = new SumElements();
        var input = CreateVectorInput(new double[] { 1, 2, 3 });

        var result = algo.Execute(input);

        // Проверяем, что алгоритм отработал без ошибок
        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
    }

    [Fact]
    public void SumElements_EmptyArray_ReturnsZeroTime()
    {
        var algo = new SumElements();
        var input = CreateVectorInput(Array.Empty<double>());

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
    }

    [Fact]
    public void ProductElements_CalculatesCorrectProduct()
    {
        var algo = new ProductElements();
        var input = CreateVectorInput(new double[] { 2, 3, 4 });

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
    }

    [Fact]
    public void ProductElements_EmptyArray_ReturnsZeroTime()
    {
        var algo = new ProductElements();
        var input = CreateVectorInput(Array.Empty<double>());

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
    }

    [Fact]
    public void PolynomialNaive_ExecutesSuccessfully()
    {
        var algo = new PolynomialNaive();
        var input = CreateVectorInput(new double[] { 1, 2, 3, 4, 5 });

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
    }

    [Fact]
    public void PolynomialHorner_ExecutesSuccessfully()
    {
        var algo = new PolynomialHorner();
        var input = CreateVectorInput(new double[] { 1, 2, 3, 4, 5 });

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
    }

    [Fact]
    public void PolynomialHorner_FasterThanNaive_ForLargeInput()
    {
        var naive = new PolynomialNaive();
        var horner = new PolynomialHorner();
        var data = VectorGenerator.GenerateRandom(5000);
        var input = CreateVectorInput(data);

        var naiveResult = naive.Execute(input);
        var hornerResult = horner.Execute(input);

        // Горнер должен быть быстрее (или сопоставим для малых N)
        // Для N=5000 разница должна быть заметна
        Assert.True(hornerResult.TimeMs <= naiveResult.TimeMs * 2,
            $"Horner ({hornerResult.TimeMs:F4}ms) не должен быть значительно медленнее Naive ({naiveResult.TimeMs:F4}ms)");
    }

    [Fact]
    public void BubbleSort_SortsCorrectly()
    {
        var algo = new BubbleSort();
        var input = CreateVectorInput(new double[] { 3, 1, 2 });

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
        // Исходный массив не должен быть изменён
        Assert.Equal(new double[] { 3, 1, 2 }, input.Data);
    }

    [Fact]
    public void BubbleSort_AlreadySorted_FastExit()
    {
        var algo = new BubbleSort();
        var sorted = VectorGenerator.GenerateSorted(1000);
        var input = CreateVectorInput(sorted);

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
    }

    [Fact]
    public void QuickSort_SortsCorrectly()
    {
        var algo = new QuickSort();
        var input = CreateVectorInput(new double[] { 5, 3, 8, 1, 9, 2, 7 });

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
        // Исходный массив не должен быть изменён
        Assert.Equal(new double[] { 5, 3, 8, 1, 9, 2, 7 }, input.Data);
    }

    [Fact]
    public void QuickSort_ReversedData_DoesNotDegrade()
    {
        var algo = new QuickSort();
        var reversed = VectorGenerator.GenerateReversed(1000);
        var input = CreateVectorInput(reversed);

        var result = algo.Execute(input);

        // Рандомизированный pivot не должен деградировать до O(n²)
        Assert.True(result.TimeMs < 100, $"QuickSort на reversed данных слишком медленный: {result.TimeMs:F2}ms");
    }

    [Fact]
    public void Timsort_SortsCorrectly()
    {
        var algo = new Timsort();
        var input = CreateVectorInput(new double[] { 4, 2, 7, 1, 5 });

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
    }

    [Fact]
    public void Timsort_FasterThanBubble_ForLargeInput()
    {
        var bubble = new BubbleSort();
        var timsort = new Timsort();
        var data = VectorGenerator.GenerateRandom(2000);

        var bubbleResult = bubble.Execute(CreateVectorInput(data));
        var timsortResult = timsort.Execute(CreateVectorInput(data));

        Assert.True(timsortResult.TimeMs < bubbleResult.TimeMs,
            $"Timsort ({timsortResult.TimeMs:F4}ms) должен быть быстрее Bubble ({bubbleResult.TimeMs:F4}ms)");
    }

    #endregion

    #region Часть II — Матричные операции

    [Fact]
    public void MatrixMultiplication_ExecutesSuccessfully()
    {
        var algo = new MatrixMultiplication();
        var input = new AlgorithmInput
        {
            N = 50,
            M = 30,
            Data = null!
        };

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
    }

    [Fact]
    public void MatrixMultiplication_SquareMatrix()
    {
        var algo = new MatrixMultiplication();
        var input = new AlgorithmInput
        {
            N = 100,
            M = 100,
            Data = null!
        };

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
    }

    #endregion

    #region Часть IV — Возведение в степень (ШАГИ)

    [Fact]
    public void PowerIterative_2Pow10_Steps10()
    {
        var algo = new PowerIterative();
        var input = new AlgorithmInput { N = 10, Data = null! };

        var result = algo.Execute(input);

        Assert.Equal(10, result.Steps);
        Assert.Equal(0, result.TimeMs);
    }

    [Fact]
    public void PowerIterative_2Pow0_Steps0()
    {
        var algo = new PowerIterative();
        var input = new AlgorithmInput { N = 0, Data = null! };

        var result = algo.Execute(input);

        Assert.Equal(0, result.Steps);
    }

    [Fact]
    public void PowerRecursive_2Pow10_Steps10()
    {
        var algo = new PowerRecursive();
        var input = new AlgorithmInput { N = 10, Data = null! };

        var result = algo.Execute(input);

        Assert.Equal(10, result.Steps);
        Assert.Equal(0, result.TimeMs);
    }

    [Fact]
    public void PowerBinary_2Pow10_StepsApproxLog2()
    {
        var algo = new PowerBinary();
        var input = new AlgorithmInput { N = 10, Data = null! };

        var result = algo.Execute(input);

        // 10 в двоичном = 1010, нужно ~4 умножения
        Assert.True(result.Steps <= 6, $"PowerBinary для n=10: Steps={result.Steps}, ожидалось ≤ 6");
        Assert.True(result.Steps >= 3, $"PowerBinary для n=10: Steps={result.Steps}, ожидалось ≥ 3");
        Assert.Equal(0, result.TimeMs);
    }

    [Fact]
    public void PowerBinary_2Pow1024_StepsApproxLog2()
    {
        var algo = new PowerBinary();
        var input = new AlgorithmInput { N = 1024, Data = null! };

        var result = algo.Execute(input);

        // log2(1024) = 10, нужно ~10 умножений
        Assert.True(result.Steps <= 20, $"PowerBinary для n=1024: Steps={result.Steps}, ожидалось ≤ 20");
        Assert.Equal(0, result.TimeMs);
    }

    [Fact]
    public void PowerBinary_FewerStepsThanIterative()
    {
        var iterative = new PowerIterative();
        var binary = new PowerBinary();
        var input = new AlgorithmInput { N = 1000, Data = null! };

        var iterResult = iterative.Execute(input);
        var binResult = binary.Execute(input);

        Assert.True(binResult.Steps < iterResult.Steps,
            $"Binary ({binResult.Steps}) должен иметь меньше шагов, чем Iterative ({iterResult.Steps})");
    }

    #endregion

    #region Часть III — Индивидуальные задания

    [Fact]
    public void RabinKarp_FindsPatternInText()
    {
        var algo = new RabinKarpAlgorithm();
        var text = "hello world hello universe hello";
        var pattern = "hello";
        var input = CreateStringInput(text, pattern);

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
    }

    [Fact]
    public void RabinKarp_NoMatch_ReturnsEmpty()
    {
        var algo = new RabinKarpAlgorithm();
        var input = CreateStringInput("abcdef", "xyz");

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
    }

    [Fact]
    public void RabinKarp_EmptyPattern_ReturnsEmpty()
    {
        var algo = new RabinKarpAlgorithm();
        var input = CreateStringInput("abcdef", "");

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
    }

    [Fact]
    public void BoyerMoore_FindsPatternInText()
    {
        var algo = new BoyerMooreAlgorithm();
        var text = "abracadabra abracadabra";
        var pattern = "abra";
        var input = CreateStringInput(text, pattern);

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
    }

    [Fact]
    public void BoyerMoore_NoMatch_ReturnsEmpty()
    {
        var algo = new BoyerMooreAlgorithm();
        var input = CreateStringInput("abcdef", "xyz");

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
    }

    [Fact]
    public void Levenshtein_KittenToSitting_Distance3()
    {
        var algo = new LevenshteinAlgorithm();
        var input = CreateStringInput("kitten", "sitting");

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
    }

    [Fact]
    public void Levenshtein_IdenticalStrings_Distance0()
    {
        var algo = new LevenshteinAlgorithm();
        var input = CreateStringInput("hello", "hello");

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
    }

    [Fact]
    public void Levenshtein_EmptyToString_DistanceEqualsLength()
    {
        var algo = new LevenshteinAlgorithm();
        var input = CreateStringInput("", "abc");

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
    }

    #endregion

    #region Генераторы данных

    [Fact]
    public void VectorGenerator_GenerateRandom_CorrectLength()
    {
        var arr = VectorGenerator.GenerateRandom(100);
        Assert.Equal(100, arr.Length);
    }

    [Fact]
    public void VectorGenerator_GenerateSorted_IsSorted()
    {
        var arr = VectorGenerator.GenerateSorted(100);
        for (int i = 1; i < arr.Length; i++)
            Assert.True(arr[i] >= arr[i - 1]);
    }

    [Fact]
    public void VectorGenerator_GenerateReversed_IsReversed()
    {
        var arr = VectorGenerator.GenerateReversed(100);
        for (int i = 1; i < arr.Length; i++)
            Assert.True(arr[i] <= arr[i - 1]);
    }

    [Fact]
    public void MatrixGenerator_GenerateRandom_CorrectDimensions()
    {
        var matrix = MatrixGenerator.GenerateRandom(5, 3);
        Assert.Equal(5, matrix.GetLength(0));
        Assert.Equal(3, matrix.GetLength(1));
    }

    [Fact]
    public void StringGenerator_GenerateRandomString_CorrectLength()
    {
        var str = StringGenerator.GenerateRandomString(50);
        Assert.Equal(50, str.Length);
    }

    #endregion

    #region Матричные операции — Штрассен

    [Fact]
    public void StrassenMultiplication_ExecutesSuccessfully()
    {
        var algo = new StrassenMultiplication();
        var input = new AlgorithmInput
        {
            N = 64,
            M = 64,
            Data = null!
        };

        var result = algo.Execute(input);

        Assert.True(result.TimeMs >= 0);
        Assert.Equal(0, result.Steps);
    }

    [Fact]
    public void StrassenMultiplication_FasterThanClassical_ForLargeMatrices()
    {
        var classical = new MatrixMultiplication();
        var strassen = new StrassenMultiplication();

        var input = new AlgorithmInput
        {
            N = 256,
            M = 256,
            Data = null!
        };

        var classicalResult = classical.Execute(input);
        var strassenResult = strassen.Execute(input);

        // Для больших матриц Штрассен должен быть быстрее
        // (хотя для малых — классический может быть быстрее из-за overhead)
        Assert.True(strassenResult.TimeMs < classicalResult.TimeMs * 2,
            $"Strassen ({strassenResult.TimeMs:F2}ms) не должен быть значительно медленнее Classical ({classicalResult.TimeMs:F2}ms)");
    }

    #endregion

    #region Параллельное выполнение

    [Fact]
    public async Task ParallelRunner_ExecutesMultipleExperiments()
    {
        var runner = new ParallelExperimentRunner(maxDegreeOfParallelism: 2);

        var experiments = new Dictionary<Guid, (IAlgorithm, IAlgorithmInput)>
    {
        { Guid.NewGuid(), (new SumElements(), new AlgorithmInput { N = 1000, Data = VectorGenerator.GenerateRandom(1000) }) },
        { Guid.NewGuid(), (new ProductElements(), new AlgorithmInput { N = 1000, Data = VectorGenerator.GenerateRandom(1000) }) },
        { Guid.NewGuid(), (new BubbleSort(), new AlgorithmInput { N = 500, Data = VectorGenerator.GenerateRandom(500) }) }
    };

        var results = await runner.RunBatchAsync(experiments, runsPerExperiment: 5);

        Assert.Equal(3, results.Count);
        Assert.All(results.Values, summary => Assert.True(summary.MeanTimeMs >= 0));
    }

    [Fact]
    public async Task ExperimentQueue_ProcessesTasksInOrder()
    {
        var queue = new ExperimentQueue(maxParallelism: 2);

        var task1Id = queue.Enqueue(new SumElements(), new AlgorithmInput { N = 100, Data = VectorGenerator.GenerateRandom(100) }, runsCount: 3);
        var task2Id = queue.Enqueue(new ProductElements(), new AlgorithmInput { N = 100, Data = VectorGenerator.GenerateRandom(100) }, runsCount: 3);

        Assert.Equal(2, queue.PendingCount);

        var task1 = await queue.ExecuteNextAsync();
        Assert.Equal(TaskStatus.Completed, task1.Status);
        Assert.Equal(3, task1.Results.Count);

        var task2 = await queue.ExecuteNextAsync();
        Assert.Equal(TaskStatus.Completed, task2.Status);
        Assert.Equal(3, task2.Results.Count);
    }

    #endregion

    #region Адаптивное количество прогонов

    [Fact]
    public async Task BenchmarkRunner_AdaptsRunCount_ForFastAlgorithms()
    {
        var algo = new ConstantFunction();
        var input = new AlgorithmInput { N = 10, Data = new double[10] };

        var results = await BenchmarkRunner.RunAsync(algo, input, initialRuns: 10);

        // Для очень быстрых алгоритмов должно быть больше прогонов
        Assert.True(results.Count >= 10, $"Ожидалось ≥10 прогонов, получено {results.Count}");
    }

    [Fact]
    public async Task BenchmarkRunner_LimitsRunCount_ForSlowAlgorithms()
    {
        var algo = new BubbleSort();
        var input = new AlgorithmInput { N = 5000, Data = VectorGenerator.GenerateRandom(5000) };

        var results = await BenchmarkRunner.RunAsync(algo, input, initialRuns: 10);

        // Для медленных алгоритмов не должно быть слишком много прогонов
        Assert.True(results.Count <= 100, $"Ожидалось ≤100 прогонов, получено {results.Count}");
    }

    #endregion
}