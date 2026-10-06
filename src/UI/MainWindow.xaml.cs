using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Algorithms.DataGenerators;
using Algorithms.Helpers;
using Algorithms.IndividualTask;
using Algorithms.MatrixOperations;
using Algorithms.PowerAlgorithms;
using Algorithms.VectorOperations;
using Core.Interfaces;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace UI;

public partial class MainWindow : Window
{
    private static readonly Brush GridBrush = new SolidColorBrush(Color.FromRgb(36, 48, 70));
    private static readonly Brush AxisBrush = new SolidColorBrush(Color.FromRgb(105, 119, 145));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(137, 149, 173));
    private static readonly Brush TickBrush = new SolidColorBrush(Color.FromRgb(201, 211, 232));
    private static readonly Brush ExperimentBrush = new SolidColorBrush(Color.FromRgb(105, 216, 194));
    private static readonly Brush AreaBrush = new SolidColorBrush(Color.FromArgb(30, 105, 216, 194));
    private static readonly SolidColorBrush SurfaceLow = new(Color.FromRgb(38, 52, 90));
    private static readonly SolidColorBrush SurfaceHigh = new(Color.FromRgb(105, 216, 194));

    private readonly List<IAlgorithm> _algorithms =
    [
        new BubbleSort(),
        new QuickSort(),
        new Timsort(),
        new SumElements(),
        new ProductElements(),
        new ConstantFunction(),
        new PolynomialNaive(),
        new PolynomialHorner(),
        new MatrixMultiplication(),
        new StrassenMultiplication(),
        new PowerIterative(),
        new PowerRecursive(),
        new PowerBinary(),
        new RabinKarpAlgorithm(),
        new BoyerMooreAlgorithm(),
        new LevenshteinAlgorithm()
    ];

    private List<PlotPoint> _points = [];
    private List<PlotPoint> _approximation = [];
    private CancellationTokenSource? _cancellation;
    private bool _usesSteps;

    // Дополнительные серии для режима сравнения (2+ алгоритмов на одном поле)
    private readonly List<ComparisonSeries> _comparison = [];
    private List<IAlgorithm> _compareSelection = [];

    // 3D-режим для матричных алгоритмов: поверхность время(N, M)
    private List<GridPoint> _grid3d = [];

    // Параметры серии, отображённой на графике (нужны для сохранения в БД)
    private List<ExperimentResult> _currentRunResults = [];
    private IAlgorithm? _currentAlgorithmRef;
    private Algorithm? _currentEntity;
    private int _currentMaxN, _currentStep, _currentRuns;
    private DataType? _currentDataType;

    // Очередь серий экспериментов
    private readonly ObservableCollection<QueueItem> _queueItems = [];
    private bool _queueRunning;
    private CancellationTokenSource? _queueCancellation;

    public MainWindow()
    {
        InitializeComponent();
        AlgorithmCombo.ItemsSource = _algorithms;
        AlgorithmCombo.SelectedItem = _algorithms[1];
        QueueList.ItemsSource = _queueItems;

        // Автовыбор алгоритма для автоматизированных прогонов: UI.exe --algo:"Matrix Multiplication"
        var args = Environment.GetCommandLineArgs();
        var algoArg = args.FirstOrDefault(a => a.StartsWith("--algo:", StringComparison.OrdinalIgnoreCase));
        if (algoArg is not null)
        {
            var name = algoArg["--algo:".Length..].Trim('"');
            var match = _algorithms.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                AlgorithmCombo.SelectedItem = match;
        }

        // Предвыбор серий сравнения: UI.exe --compare:"Strassen Multiplication;Bubble Sort"
        var cmpArg = args.FirstOrDefault(a => a.StartsWith("--compare:", StringComparison.OrdinalIgnoreCase));
        if (cmpArg is not null)
        {
            var names = cmpArg["--compare:".Length..].Trim('"').Split(';', StringSplitOptions.TrimEntries);
            _compareSelection = _algorithms.Where(a => names.Contains(a.Name, StringComparer.OrdinalIgnoreCase)).ToList();
            CompareCheck.IsChecked = _compareSelection.Count > 0;
            if (PickCompareButton is not null && _compareSelection.Count > 0)
                PickCompareButton.Content = $"Выбрано для сравнения: {_compareSelection.Count}";
        }
    }

    private IAlgorithm? SelectedAlgorithm => AlgorithmCombo.SelectedItem as IAlgorithm;
    private bool Is3DMode => SelectedAlgorithm?.SupportsMatrixDimensions == true;

    private void AlgorithmCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var algorithm = SelectedAlgorithm;
        if (algorithm is null || PlotCanvas is null)
            return;

        _usesSteps = algorithm.SupportsStepCounting;
        AlgorithmDescription.Text = algorithm.Description;
        var limit = GetMaximumAllowedN(algorithm);

        // Показательные вводные: достаточно большие, чтобы эксперимент шёл секунды
        (MaxNBox.Text, StepBox.Text, RunsBox.Text) = algorithm switch
        {
            BubbleSort => ("12000", "600", "3"),
            QuickSort or Timsort => ("400000", "20000", "3"),
            SumElements or ProductElements or PolynomialHorner => ("2000000", "100000", "3"),
            ConstantFunction => ("1000000", "50000", "3"),
            PolynomialNaive => ("8000", "400", "3"),
            MatrixMultiplication or StrassenMultiplication => ("512", "48", "1"),
            PowerIterative => ("3000000", "150000", "3"),
            PowerRecursive => ("5000", "250", "3"),
            PowerBinary => ("200000", "10000", "3"),
            RabinKarpAlgorithm or BoyerMooreAlgorithm => ("2000000", "100000", "3"),
            LevenshteinAlgorithm => ("4000", "200", "3"),
            _ => ("100000", "5000", "3")
        };

        DataTypeCombo.IsEnabled = algorithm.SupportsDataType;
        ChartSubtitle.Text = algorithm.SupportsMatrixDimensions
            ? $"{algorithm.Name} · 3D-поверхность времени по N и M"
            : algorithm.SupportsStepCounting
                ? $"{algorithm.Name} · рост количества операций, ожидаемая оценка O({ComplexityShortName(algorithm.TheoreticalComplexity)})"
                : $"{algorithm.Name} · время выполнения при увеличении размера входных данных";

        _points.Clear();
        _approximation.Clear();
        _currentRunResults.Clear();
        _grid3d.Clear();
        _currentAlgorithmRef = null;
        _currentEntity = null;
        ResetComparison();
        RunProgress.Value = 0;
        MeanMetric.Text = "—";
        ComplexityMetric.Text = "—";
        ComplexityNote.Text = "ожидаемая сложность";
        PointsMetric.Text = "—";
        PointsNote.Text = "размеров входных данных";
        SaveDbButton.IsEnabled = false;
        EmptyState.Visibility = Visibility.Visible;
        PlotCanvas.Children.Clear();
        StatusText.Text = $"Готов к запуску · предел N = {limit:N0}";
        DrawEmptyChartFrame();
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadRunParameters(out var algorithm, out var maxN, out var step, out var runs, out var dataType, out var error))
        {
            StatusText.Text = error;
            return;
        }

        // Сравнение: отобранные алгоритмы, совместимые с основным по типу замера
        var comparisons = new List<IAlgorithm>();
        if (CompareCheck.IsChecked == true)
        {
            comparisons = _compareSelection
                .Where(a => a.Name != algorithm.Name)
                .Where(a => a.SupportsStepCounting == algorithm.SupportsStepCounting)
                .ToList();
            var skipped = _compareSelection.Count - comparisons.Count;
            if (_compareSelection.Count > 0 && comparisons.Count == 0)
            {
                StatusText.Text = "Выбранные для сравнения алгоритмы несовместимы с основным по типу замера (время/шаги).";
                return;
            }
            if (skipped > 0)
            {
                StatusText.Text = $"Несовместимые по типу замера алгоритмы пропущены ({skipped}).";
            }
        }

        var useCache = UseCacheCheck.IsChecked == true;
        _points.Clear();
        _approximation.Clear();
        _grid3d.Clear();
        ResetComparison();
        EmptyState.Visibility = Visibility.Visible;
        DrawEmptyChartFrame();
        _cancellation = new CancellationTokenSource();
        RunButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        RunProgress.Value = 0;
        StatusText.Text = "Подготовка эксперимента…";

        var token = _cancellation.Token;
        try
        {
            // Кэш и резолв алгоритма работают с БД — она должна быть создана и засеяна
            await UiDatabase.EnsureInitializedAsync();

            if (algorithm.SupportsMatrixDimensions)
            {
                await RunMatrixExperimentAsync(algorithm, maxN, step, runs, comparisons, useCache, token);
            }
            else
            {
                await RunFlatExperimentAsync(algorithm, maxN, step, runs, dataType, comparisons, useCache, token);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Расчёт остановлен.";
            ChartSubtitle.Text = $"{algorithm.Name} · измерения остановлены пользователем";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Не удалось выполнить эксперимент.";
            ChartSubtitle.Text = ex.Message;
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            RunButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }
    }

    /// <summary>Обычный 2D-эксперимент: основная серия + кривые сравнения.</summary>
    private async Task RunFlatExperimentAsync(
        IAlgorithm algorithm, int maxN, int step, int runs, DataType dataType,
        List<IAlgorithm> comparisons, bool useCache, CancellationToken token)
    {
        var (fitN, fitStep, adjusted) = AutoFitSeries(algorithm, maxN, step, runs, dataType);

        // При сравнении берём минимум по всем подборам, чтобы тяжёлый алгоритм не растянул серию
        foreach (var other in comparisons)
        {
            var (otherN, otherStep, _) = AutoFitSeries(other, fitN, fitStep, runs, dataType);
            if (otherN < fitN)
                (fitN, fitStep) = (otherN, otherStep);
        }
        ApplyFittedScale(fitN, fitStep, adjusted);

        var sizes = MakeSizes(fitN, fitStep);
        var entity = await UiDatabase.ResolveAlgorithmAsync(algorithm);
        var progressA = MakeProgress(0, comparisons.Count == 0 ? 1.0 : 0.55, algorithm);
        var (points, results, cachedMain) = await Task.Run(
            () => RunSeries2DAsync(algorithm, entity, sizes, runs, dataType, useCache, token, progressA), token);

        _points = points;
        _currentRunResults = results;
        _usesSteps = algorithm.SupportsStepCounting;
        _lastRunCount = runs;
        _currentAlgorithmRef = algorithm;
        _currentEntity = entity;
        _currentMaxN = fitN;
        _currentStep = fitStep;
        _currentRuns = runs;
        _currentDataType = dataType;
        _approximation = FitExpectedCurve(_points, algorithm.TheoreticalComplexity);

        // Серии сравнения на тех же размерах
        var totalCached = cachedMain;
        for (var i = 0; i < comparisons.Count; i++)
        {
            var other = comparisons[i];
            var otherEntity = await UiDatabase.ResolveAlgorithmAsync(other);
            var share = 0.45 / comparisons.Count;
            var progressB = MakeProgress(0.55 + share * i, share, other);
            var (pointsB, resultsB, cachedB) = await Task.Run(
                () => RunSeries2DAsync(other, otherEntity, sizes, runs, dataType, useCache, token, progressB), token);
            totalCached += cachedB;
            _comparison.Add(new ComparisonSeries
            {
                Algorithm = other,
                Brush = ComparisonPalette()[(i + 1) % ComparisonPalette().Length],
                Points = pointsB,
                Approximation = FitExpectedCurve(pointsB, other.TheoreticalComplexity),
                Results = resultsB
            });
        }

        EmptyState.Visibility = Visibility.Collapsed;
        SaveDbButton.IsEnabled = true;
        UpdateSummary(algorithm);
        UpdateLegend();
        RedrawChart();
        RunProgress.Value = 100;
        StatusText.Text = BuildDoneStatus(_points.Count, totalCached, runs, comparisons.Count);
        ChartSubtitle.Text = comparisons.Count > 0
            ? $"{algorithm.Name} vs {string.Join(" vs ", _comparison.Select(c => c.Algorithm.Name))} · {(_usesSteps ? "число операций" : "время выполнения")}"
            : $"{algorithm.Name} · {(_usesSteps ? "число операций" : "время выполнения")}";
    }

    /// <summary>Матричный 3D-эксперимент: поверхность время(N, M) + диагональные кривые сравнения.</summary>
    private async Task RunMatrixExperimentAsync(
        IAlgorithm algorithm, int maxN, int step, int runs,
        List<IAlgorithm> comparisons, bool useCache, CancellationToken token)
    {
        // Масштаб подбираем по поверхности; кривые сравнения выполняются на тех же N
        // и добавляют своё время поверх серии
        var (fitN, fitStep, adjusted) = AutoFitSeries3D(algorithm, maxN, step, runs);
        ApplyFittedScale(fitN, fitStep, adjusted);

        var entity = await UiDatabase.ResolveAlgorithmAsync(algorithm);
        var progressA = MakeProgress(0, comparisons.Count == 0 ? 1.0 : 0.6, algorithm);
        var (grid, results, cachedCells) = await Task.Run(
            () => RunSeries3DAsync(algorithm, entity, fitN, fitStep, runs, useCache, token, progressA), token);

        _grid3d = grid;
        _currentRunResults = results;
        _usesSteps = false;
        _lastRunCount = runs;
        _currentAlgorithmRef = algorithm;
        _currentEntity = entity;
        _currentMaxN = fitN;
        _currentStep = fitStep;
        _currentRuns = runs;
        _currentDataType = DataType.Random;

        var totalCached = cachedCells;
        for (var i = 0; i < comparisons.Count; i++)
        {
            var other = comparisons[i];
            var otherEntity = await UiDatabase.ResolveAlgorithmAsync(other);
            var share = 0.4 / comparisons.Count;
            var progressB = MakeProgress(0.6 + share * i, share, other);
            // Кривая сравнения идёт по диагонали (N, N) — общий срез с поверхностью
            var (pointsB, resultsB, cachedB) = await Task.Run(
                () => RunSeries2DAsync(other, otherEntity, MakeSizes(fitN, fitStep, minN: 16), runs, DataType.Random, useCache, token, progressB), token);
            totalCached += cachedB;
            _comparison.Add(new ComparisonSeries
            {
                Algorithm = other,
                Brush = ComparisonPalette()[(i + 1) % ComparisonPalette().Length],
                Points = pointsB,
                Approximation = FitExpectedCurve(pointsB, other.TheoreticalComplexity),
                Results = resultsB
            });
        }

        EmptyState.Visibility = Visibility.Collapsed;
        SaveDbButton.IsEnabled = true;
        UpdateSummary(algorithm);
        UpdateLegend();
        RedrawChart();
        RunProgress.Value = 100;
        StatusText.Text = BuildDoneStatus(_grid3d.Count, totalCached, runs, comparisons.Count);
        ChartSubtitle.Text = $"{algorithm.Name} · 3D: время (N × M)" +
                             (comparisons.Count > 0 ? $" · сравнение по диагонали M = N: {string.Join(", ", _comparison.Select(c => c.Algorithm.Name))}" : string.Empty);
    }

    private void ApplyFittedScale(int fitN, int fitStep, bool adjusted)
    {
        if (adjusted || fitN.ToString(CultureInfo.CurrentCulture) != MaxNBox.Text)
        {
            MaxNBox.Text = fitN.ToString(CultureInfo.CurrentCulture);
            StepBox.Text = fitStep.ToString(CultureInfo.CurrentCulture);
        }
    }

    private string BuildDoneStatus(int points, int cached, int runs, int comparisons) =>
        $"Готово · {points} точек{(cached > 0 ? $", {cached} из кэша БД" : string.Empty)}, по {runs} запуск(а/ов) на каждую" +
        (comparisons > 0 ? $" · сравнение: {comparisons + 1} алгоритмов" : string.Empty);

    /// <summary>Прогресс с отображением на отведённый серии диапазон [offset, offset+share].</summary>
    private Progress<(int Completed, int Total, int CurrentN)> MakeProgress(double offset, double share, IAlgorithm owner)
    {
        return new Progress<(int Completed, int Total, int CurrentN)>(p =>
        {
            RunProgress.Value = (offset + share * (double)p.Completed / p.Total) * 100;
            StatusText.Text = $"Измерение · {owner.Name}: N = {p.CurrentN:N0} · {p.Completed} из {p.Total}";
            ChartSubtitle.Text = $"{owner.Name} · {p.Completed} из {p.Total}";
        });
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellation?.Cancel();
        _queueCancellation?.Cancel();
    }

    private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (PlotViewport.Visibility == Visibility.Visible)
            return; // 3D-вьюпорт масштабируется сам
        if (_points.Count > 0)
            RedrawChart();
        else
            DrawEmptyChartFrame();
    }

    // ---------- Измерение серий (с кэшем БД по вводным) ----------

    private async Task<(List<PlotPoint> Points, List<ExperimentResult> Results, int CachedPoints)> RunSeries2DAsync(
        IAlgorithm algorithm,
        Algorithm entity,
        IReadOnlyList<int> sizes,
        int runs,
        DataType dataType,
        bool useCache,
        CancellationToken cancellationToken,
        IProgress<(int Completed, int Total, int CurrentN)> progress)
    {
        var points = new List<PlotPoint>(sizes.Count);
        var freshResults = new List<ExperimentResult>(sizes.Count * runs);
        var cachedPoints = 0;
        var random = new Random(7411);
        var matrix = algorithm.SupportsMatrixDimensions;
        var cacheDataType = algorithm.SupportsDataType ? dataType : DataType.Random;
        var db = UiDatabase.CreateDatabaseService();

        for (var index = 0; index < sizes.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var n = sizes[index];
            int? m = matrix ? n : null;

            // Кэш: тот же алгоритм с теми же вводными (N, M, тип данных) уже выполнялся?
            if (useCache)
            {
                var rows = await db.GetCachedResultsAsync(entity.Id, n, cacheDataType, m);
                if (rows.Count >= runs)
                {
                    var taken = rows.OrderBy(r => r.RunNumber).Take(runs).ToList();
                    cachedPoints++;
                    double total = 0;
                    foreach (var row in taken)
                        total += algorithm.SupportsStepCounting ? row.Steps : row.TimeMs;
                    points.Add(new PlotPoint(n, total / taken.Count, FromCache: true));
                    progress.Report((index + 1, sizes.Count, n));
                    continue;
                }
            }

            var fresh = new List<ExperimentResult>(runs);
            double sum = 0;
            for (var run = 0; run < runs; run++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var input = CreateInput(algorithm, n, m, dataType, random);
                var measurement = algorithm.Execute(input);
                sum += algorithm.SupportsStepCounting ? measurement.Steps : measurement.TimeMs;
                fresh.Add(new ExperimentResult
                {
                    N = n,
                    M = m,
                    RunNumber = run + 1,
                    TimeMs = measurement.TimeMs,
                    Steps = measurement.Steps
                });
            }
            freshResults.AddRange(fresh);

            if (useCache)
            {
                var experiment = new Experiment
                {
                    AlgorithmId = entity.Id,
                    Algorithm = entity,
                    Date = DateTime.UtcNow,
                    N_max = n,
                    Step = n,
                    RunsCount = runs,
                    DataType = cacheDataType
                };
                await db.SaveExperimentAsync(experiment, fresh);
            }

            points.Add(new PlotPoint(n, sum / runs));
            progress.Report((index + 1, sizes.Count, n));
        }

        return (points, freshResults, cachedPoints);
    }

    private async Task<(List<GridPoint> Grid, List<ExperimentResult> Results, int CachedCells)> RunSeries3DAsync(
        IAlgorithm algorithm,
        Algorithm entity,
        int nMax,
        int step,
        int runs,
        bool useCache,
        CancellationToken cancellationToken,
        IProgress<(int Completed, int Total, int CurrentN)> progress)
    {
        var grid = new List<GridPoint>();
        var freshResults = new List<ExperimentResult>();
        var cachedCells = 0;
        var random = new Random(7411);
        var db = UiDatabase.CreateDatabaseService();
        var sizes = MakeSizes(nMax, step, minN: 16);
        var total = sizes.Count * sizes.Count;
        var done = 0;

        foreach (var n in sizes)
        {
            foreach (var m in sizes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (useCache)
                {
                    var rows = await db.GetCachedResultsAsync(entity.Id, n, DataType.Random, m);
                    if (rows.Count > 0)
                    {
                        cachedCells++;
                        // из кэша берём минимум по всем сохранённым прогонам ячейки
                        grid.Add(new GridPoint(n, m, rows.Min(r => r.TimeMs), FromCache: true));
                        done++;
                        progress.Report((done, total, n));
                        continue;
                    }
                }

                var fresh = new List<ExperimentResult>();
                var input = CreateInput(algorithm, n, m, DataType.Random, random);
                var first = algorithm.Execute(input);
                var best = first.TimeMs;
                fresh.Add(new ExperimentResult
                {
                    N = n,
                    M = m,
                    RunNumber = 1,
                    TimeMs = first.TimeMs,
                    Steps = first.Steps
                });

                // Адаптивные прогоны: быструю ячейку измеряем десятки раз, чтобы минимум
                // был устойчив к шуму планировщика и GC (иначе край поверхности «рвётся»)
                var extraRuns = first.TimeMs < 20
                    ? Math.Clamp((int)Math.Ceiling(20.0 / Math.Max(0.05, first.TimeMs)), 2, 24)
                    : 1;
                extraRuns = Math.Max(extraRuns, runs);
                for (var run = 1; run < extraRuns; run++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var measurement = algorithm.Execute(CreateInput(algorithm, n, m, DataType.Random, random));
                    best = Math.Min(best, measurement.TimeMs);
                    fresh.Add(new ExperimentResult
                    {
                        N = n,
                        M = m,
                        RunNumber = run + 1,
                        TimeMs = measurement.TimeMs,
                        Steps = measurement.Steps
                    });
                }
                freshResults.AddRange(fresh);

                if (useCache)
                {
                    var experiment = new Experiment
                    {
                        AlgorithmId = entity.Id,
                        Algorithm = entity,
                        Date = DateTime.UtcNow,
                        N_max = n,
                        Step = n,
                        RunsCount = fresh.Count,
                        DataType = DataType.Random
                    };
                    await db.SaveExperimentAsync(experiment, fresh);
                }

                grid.Add(new GridPoint(n, m, best));
                done++;
                progress.Report((done, total, n));
            }
        }

        return (grid, freshResults, cachedCells);
    }

    private bool TryReadRunParameters(
        [NotNullWhen(true)] out IAlgorithm? algorithm, out int maxN, out int step, out int runs, out DataType dataType, out string? error)
    {
        algorithm = null;
        maxN = step = runs = 0;
        dataType = DataType.Random;
        error = null;

        if (SelectedAlgorithm is not { } algo)
        {
            error = "Выберите алгоритм.";
            return false;
        }
        if (!int.TryParse(MaxNBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxN) ||
            !int.TryParse(StepBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out step) ||
            !int.TryParse(RunsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out runs))
        {
            error = "Введите целые числа для N, шага и количества запусков.";
            return false;
        }

        var maxAllowed = GetMaximumAllowedN(algo);
        if (maxN < 2 || maxN > maxAllowed || step < 1 || step > maxN || runs < 1 || runs > 20)
        {
            error = $"Допустимые значения: N от 2 до {maxAllowed:N0}, шаг от 1 до N, 1–20 запусков.";
            return false;
        }
        if (MakeSizes(maxN, step).Count > (algo.SupportsMatrixDimensions ? 15 : 120))
        {
            error = algo.SupportsMatrixDimensions
                ? "Слишком густая сетка для 3D. Увеличьте шаг (рекомендуется до 10–15 значений на ось)."
                : "Слишком много точек. Увеличьте шаг так, чтобы на графике было не больше 120 размеров.";
            return false;
        }

        algorithm = algo;
        dataType = GetSelectedDataType();
        return true;
    }

    private static AlgorithmInput CreateInput(IAlgorithm algorithm, int n, int? m, DataType dataType, Random random)
    {
        object data = algorithm switch
        {
            MatrixMultiplication or StrassenMultiplication => Array.Empty<double>(),
            RabinKarpAlgorithm or BoyerMooreAlgorithm => CreateSearchInput(n, random),
            LevenshteinAlgorithm => CreateLevenshteinInput(n, random),
            _ => VectorGenerator.GenerateForDataType(n, dataType)
        };

        return new AlgorithmInput
        {
            N = n,
            M = algorithm.SupportsMatrixDimensions ? (m ?? n) : null,
            DataType = dataType,
            Data = data
        };
    }

    private static (string text, string pattern) CreateSearchInput(int n, Random random)
    {
        var text = StringGenerator.GenerateRandomString(n, random);
        var patternLength = Math.Max(1, Math.Min(12, n / 10));
        var pattern = StringGenerator.GenerateRandomString(patternLength, random);
        return (text, pattern);
    }

    private static (string first, string second) CreateLevenshteinInput(int n, Random random) =>
        (StringGenerator.GenerateRandomString(n, random), StringGenerator.GenerateRandomString(n, random));

    /// <summary>
    /// Размеры входных данных: первая точка — N = minN (по умолчанию 1), далее ровный шаг,
    /// без «хвостовой» точки, ломающей равномерность.
    /// </summary>
    private static List<int> MakeSizes(int maxN, int step, int minN = 1)
    {
        var sizes = new List<int>();
        for (var n = minN; n <= maxN; n += step)
            sizes.Add(n);
        if (sizes.Count == 0)
            sizes.Add(maxN);
        return sizes;
    }

    private static List<PlotPoint> FitExpectedCurve(IReadOnlyList<PlotPoint> points, ComplexityType complexity)
    {
        if (points.Count == 0)
            return [];

        var numerator = 0d;
        var denominator = 0d;
        foreach (var point in points)
        {
            var basis = ComplexityBasis(point.N, complexity);
            numerator += basis * point.Value;
            denominator += basis * basis;
        }

        var coefficient = denominator <= double.Epsilon ? 0 : numerator / denominator;
        return points.Select(point => new PlotPoint(point.N, coefficient * ComplexityBasis(point.N, complexity))).ToList();
    }

    // ---------- Автоподбор масштаба (серия ~5–6 секунд) ----------

    /// <summary>
    /// Пилотный замер на малом входе + экстраполяция по теоретической сложности;
    /// N удваивается (вверх) или делится (вниз), пока оценка серии не попадёт в ~5–8 с.
    /// </summary>
    private (int NMax, int Step, bool Adjusted) AutoFitSeries(
        IAlgorithm algorithm, int maxN, int step, int runs, DataType dataType)
    {
        const double targetMinMs = 5000;
        const double targetMaxMs = 10000;
        var cap = GetMaximumAllowedN(algorithm);

        int pilot = Math.Min(PilotSize(algorithm), cap);
        var random = new Random(7411);
        double pilotMs = 0;
        _ = algorithm.Execute(CreateInput(algorithm, pilot, null, dataType, random));
        const int pilotRuns = 3;
        for (var i = 0; i < pilotRuns; i++)
        {
            var sw = Stopwatch.StartNew();
            algorithm.Execute(CreateInput(algorithm, pilot, null, dataType, random));
            pilotMs += sw.Elapsed.TotalMilliseconds;
        }
        pilotMs = Math.Max(0.0001, pilotMs / pilotRuns);

        var complexity = algorithm.TheoreticalComplexity;
        double Basis(int size) => algorithm is StrassenMultiplication
            ? Math.Pow(NextPowerOfTwo(size), 2.81) * 2
            : ComplexityBasis(size, complexity);
        double unit = pilotMs / Math.Max(1e-9, Basis(pilot));

        double Estimate(int n, int st)
        {
            double sum = 0;
            for (var v = 1; v <= n; v += st)
                sum += Basis(v);
            return unit * sum * runs;
        }

        var n = maxN;
        var st = step;
        var est = Estimate(n, st);

        if (est < targetMinMs)
        {
            while (n < cap)
            {
                var nextN = (int)Math.Min((long)n * 2, cap);
                var nextStep = Math.Max(1, (int)Math.Round((double)nextN * st / n));
                n = nextN;
                st = nextStep;
                est = Estimate(n, st);
                if (est >= targetMinMs)
                    break;
            }
        }
        while (est > targetMaxMs && n > 500)
        {
            var nextN = Math.Max(500, (int)(n / 1.6));
            var nextStep = Math.Max(1, (int)Math.Round((double)nextN * st / n));
            n = nextN;
            st = nextStep;
            est = Estimate(n, st);
            if (est <= targetMaxMs)
                break;
        }

        if (n / st > 30)
            st = Math.Max(1, n / 30);
        if (st > n)
            st = n;

        return (n, st, n != maxN || st != step);
    }

    /// <summary>Автоподбор для 3D-сетки матричного алгоритма: база стоимости N²·M (или N^2.81 у Штрассена).</summary>
    private (int NMax, int Step, bool Adjusted) AutoFitSeries3D(IAlgorithm algorithm, int maxN, int step, int runs)
    {
        const double targetMinMs = 6000;
        const double targetMaxMs = 12000;
        var cap = GetMaximumAllowedN(algorithm);

        int pilot = Math.Min(128, cap);
        var random = new Random(7411);
        double pilotMs = 0;
        _ = algorithm.Execute(CreateInput(algorithm, pilot, pilot, DataType.Random, random));
        for (var i = 0; i < 3; i++)
        {
            var sw = Stopwatch.StartNew();
            algorithm.Execute(CreateInput(algorithm, pilot, pilot, DataType.Random, random));
            pilotMs += sw.Elapsed.TotalMilliseconds;
        }
        pilotMs = Math.Max(0.0001, pilotMs / 3);

        double Basis(int n, int m) => algorithm is StrassenMultiplication
            ? Math.Pow(NextPowerOfTwo(Math.Max(n, m)), 2.81) * 2
            : (double)n * n * m;
        double unit = pilotMs / Math.Max(1e-9, Basis(pilot, pilot));

        double Estimate(int n, int st)
        {
            double sum = 0;
            var sizes = MakeSizes(n, st, minN: 16);
            foreach (var nn in sizes)
                foreach (var mm in sizes)
                    sum += Basis(nn, mm);
            return unit * sum * runs;
        }
        var n = maxN;
        var st = step;
        var est = Estimate(n, st);

        if (est < targetMinMs)
        {
            while (n < cap)
            {
                var nextN = (int)Math.Min((long)n * 2, cap);
                var nextStep = Math.Max(1, (int)Math.Round((double)nextN * st / n));
                n = nextN;
                st = nextStep;
                est = Estimate(n, st);
                if (est >= targetMinMs)
                    break;
            }
        }
        while (est > targetMaxMs && n > 64)
        {
            var nextN = Math.Max(64, (int)(n / 1.6));
            var nextStep = Math.Max(1, (int)Math.Round((double)nextN * st / n));
            n = nextN;
            st = nextStep;
            est = Estimate(n, st);
            if (est <= targetMaxMs)
                break;
        }

        // Не гуще 10 значений на ось
        if (n / st > 10)
            st = Math.Max(1, n / 10);
        if (st > n)
            st = n;

        return (n, st, n != maxN || st != step);
    }

    // ---------- Метрики, легенда ----------

    private void UpdateSummary(IAlgorithm algorithm)
    {
        if (_grid3d.Count > 0)
        {
            var mean = _grid3d.Average(p => p.Value);
            MeanMetric.Text = $"{mean:0.####} мс";
            ComplexityMetric.Text = algorithm is StrassenMultiplication ? "O(N^2.81)" : "O(N²·M)";
            ComplexityNote.Text = "теоретическая сложность (N × M)";
            PointsMetric.Text = _grid3d.Count.ToString(CultureInfo.CurrentCulture);
            PointsNote.Text = "ячеек сетки N × M";
            return;
        }

        var comparing = _comparison.Count > 0;
        var meanMain = _points.Average(point => point.Value);
        string Fmt(double v) => _usesSteps ? v.ToString("N0", CultureInfo.CurrentCulture) : v.ToString("0.####", CultureInfo.CurrentCulture);

        if (!comparing)
        {
            MeanMetric.Text = _usesSteps ? $"{meanMain:N0} оп." : $"{meanMain:0.####} мс";
            ComplexityMetric.Text = $"O({ComplexityShortName(algorithm.TheoreticalComplexity)})";
            ComplexityNote.Text = "теоретическая сложность алгоритма";
        }
        else
        {
            var names = new[] { algorithm }.Concat(_comparison.Select(c => c.Algorithm)).Take(3);
            MeanMetric.Text = $"{Fmt(meanMain)} / {string.Join(" / ", _comparison.Take(2).Select(c => Fmt(c.Points.Average(p => p.Value))))}{(_usesSteps ? " оп." : " мс")}";
            ComplexityMetric.Text = string.Join(" / ", names.Select(a => $"O({ComplexityShortName(a.TheoreticalComplexity)})"))
                                   + (_comparison.Count > 2 ? " / …" : string.Empty);
            ComplexityNote.Text = "теоретическая сложность по сериям";
        }
        PointsMetric.Text = _points.Count.ToString(CultureInfo.CurrentCulture);
        PointsNote.Text = $"по {_lastRunCount} запуск(а/ов) на точку";
    }

    /// <summary>Перестраивает легенду под активные серии.</summary>
    private void UpdateLegend()
    {
        LegendPanel.Children.Clear();

        var first = _currentAlgorithmRef ?? SelectedAlgorithm;
        if (first is not null)
            AddLegendItem(first.Name, ExperimentBrush, dashed: false);
        foreach (var series in _comparison)
            AddLegendItem(series.Algorithm.Name, series.Brush, dashed: false);
        if (_grid3d.Count == 0)
            AddLegendItem("Аппроксимация", TextBrush, dashed: true);
    }

    private void AddLegendItem(string name, Brush brush, bool dashed)
    {
        var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 17, 0) };
        if (dashed)
        {
            item.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = 0, X2 = 14, Y1 = 0, Y2 = 0,
                Stroke = brush,
                StrokeThickness = 2.2,
                StrokeDashArray = new DoubleCollection { 2.3, 1.8 },
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0)
            });
        }
        else
        {
            item.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = brush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0)
            });
        }
        item.Children.Add(new TextBlock { Text = name, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xB5, 0xC9)), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        LegendPanel.Children.Add(item);
    }

    // ---------- Отрисовка ----------

    private void RedrawChart()
    {
        if (_grid3d.Count > 0)
        {
            // 3D: настоящий вьюпорт с Z-буфером и орбитальной камерой
            PlotCanvas.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Collapsed;
            PlotViewport.Visibility = Visibility.Visible;
            SurfaceChart3D.Render(SceneRoot, PlotViewport, _grid3d, _comparison, _currentRuns);
            return;
        }

        PlotViewport.Visibility = Visibility.Collapsed;
        RedrawChart2D();
    }

    private void RedrawChart2D()
    {
        if (_points.Count == 0 || PlotCanvas.ActualWidth < 150 || PlotCanvas.ActualHeight < 130)
            return;

        PlotCanvas.Children.Clear();
        var width = PlotCanvas.ActualWidth;
        var height = PlotCanvas.ActualHeight;
        const double left = 96;
        const double right = 24;
        const double top = 29;
        const double bottom = 52;
        var plotWidth = Math.Max(1, width - left - right);
        var plotHeight = Math.Max(1, height - top - bottom);
        var minX = _points.Min(point => point.N);
        var maxX = Math.Max(_points.Max(point => point.N), _comparison.Count == 0 ? 0 : _comparison.Max(c => c.Points.Count == 0 ? 0 : c.Points.Max(p => p.N)));
        if (maxX == minX)
            maxX = minX + 1;

        // Оси с «красивыми» делениями, кратными 5
        var rawMaxY = Math.Max(
            _points.Count == 0 ? 0 : _points.Max(point => point.Value),
            Math.Max(
                _approximation.Count == 0 ? 0 : _approximation.Max(point => point.Value),
                _comparison.Count == 0 ? 0 : _comparison.Max(c => c.Points.Count == 0 || c.Approximation.Count == 0
                    ? 0
                    : Math.Max(c.Points.Max(p => p.Value), c.Approximation.Max(p => p.Value)))));
        var (maxY, yStep, yDecimals) = NiceScale(rawMaxY, divisions: 8);
        var (niceMaxX, xStep, _) = NiceScale(maxX, divisions: 5);
        var domainX = Math.Max(1, niceMaxX - minX);

        AddText(_usesSteps ? "Количество операций" : "Время выполнения · мс", 4, 5, 220, TextBrush, fontSize: 12);

        for (var v = 0d; v <= maxY + yStep * 1e-6; v += yStep)
        {
            var y = top + plotHeight * (1 - v / maxY);
            AddLine(left, y, width - right, y, GridBrush, 1);
            AddText(FormatTick(v, yDecimals), 0, y - 9, 86, TickBrush, TextAlignment.Right, fontSize: 13);
        }

        var firstX = Math.Ceiling(minX / xStep - 1e-9) * xStep;
        for (var v = firstX; v <= niceMaxX + xStep * 1e-6; v += xStep)
        {
            var x = left + (v - minX) / domainX * plotWidth;
            AddLine(x, top, x, top + plotHeight, GridBrush, 1);
            AddText(FormatTick(v, 0), x - 36, top + plotHeight + 8, 72, TickBrush, TextAlignment.Center, fontSize: 13);
        }

        AddLine(left, top, left, top + plotHeight, AxisBrush, 1.2);
        AddLine(left, top + plotHeight, width - right, top + plotHeight, AxisBrush, 1.2);
        AddText("Размер входных данных · N", width - right - 190, height - 24, 190, TextBrush, TextAlignment.Right, fontSize: 12);

        Point Map(PlotPoint point) => new(
            left + (point.N - minX) / domainX * plotWidth,
            top + plotHeight - point.Value / maxY * plotHeight);

        DrawSeries(_points, _approximation, Map, ExperimentBrush, AreaBrush, null);
        foreach (var series in _comparison)
            DrawSeries(series.Points, series.Approximation, Map, series.Brush, series.AreaBrush, series.Algorithm.Name);
    }

    /// <summary>Рисует одну 2D-серию: заливка, линия, пунктир аппроксимации и точки.</summary>
    private void DrawSeries(
        List<PlotPoint> points, List<PlotPoint> approximation, Func<PlotPoint, Point> map,
        Brush lineBrush, Brush areaBrush, string? seriesName)
    {
        if (approximation.Count > 1)
            AddPolyline(approximation.Select(map), lineBrush, 2, dashed: true, opacity: 0.75);

        if (points.Count <= 1)
            return;

        var areaPoints = points.Select(map).ToList();
        var lastX = areaPoints[^1].X;
        areaPoints.Add(new Point(lastX, PlotCanvas.ActualHeight - 52));
        areaPoints.Add(new Point(areaPoints[0].X, PlotCanvas.ActualHeight - 52));
        var area = new Polygon
        {
            Points = new PointCollection(areaPoints),
            Fill = areaBrush,
            StrokeThickness = 0
        };
        Panel.SetZIndex(area, 1);
        PlotCanvas.Children.Add(area);

        AddPolyline(points.Select(map), lineBrush, 2.4);

        var title = seriesName is null ? null : $"{seriesName}{Environment.NewLine}";
        foreach (var point in points)
        {
            var position = map(point);
            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = lineBrush,
                Stroke = new SolidColorBrush(Color.FromRgb(17, 24, 42)),
                StrokeThickness = 1.2,
                ToolTip = $"{title}N = {point.N:N0}{(point.FromCache ? " · из кэша БД" : string.Empty)}{Environment.NewLine}{(_usesSteps ? "Операции" : "Время")}: {(_usesSteps ? point.Value.ToString("N0", CultureInfo.CurrentCulture) : point.Value.ToString("0.####", CultureInfo.CurrentCulture) + " мс")}"
            };
            Canvas.SetLeft(dot, position.X - 3.5);
            Canvas.SetTop(dot, position.Y - 3.5);
            Panel.SetZIndex(dot, 3);
            PlotCanvas.Children.Add(dot);
        }
    }


    private void DrawEmptyChartFrame()
    {
        if (PlotCanvas.ActualWidth < 150 || PlotCanvas.ActualHeight < 130)
            return;
        PlotViewport.Visibility = Visibility.Collapsed;
        if (Is3DMode)
        {
            // Пустой 3D-каркас: подсказка, что для матриц рисуется поверхность
            PlotCanvas.Children.Clear();
            AddText("Поверхность времени · N × M", 4, 5, 260, TextBrush, fontSize: 12);
            AddText("Выберите параметры и запустите серию — появится 3D-поверхность",
                PlotCanvas.ActualWidth / 2 - 190, PlotCanvas.ActualHeight / 2 - 10, 380, TextBrush, TextAlignment.Center, fontSize: 12);
            return;
        }

        PlotCanvas.Children.Clear();
        var width = PlotCanvas.ActualWidth;
        var height = PlotCanvas.ActualHeight;
        const double left = 96;
        const double right = 24;
        const double top = 29;
        const double bottom = 52;
        for (var i = 0; i <= 5; i++)
        {
            var y = top + (height - top - bottom) * i / 5;
            AddLine(left, y, width - right, y, GridBrush, 1);
        }
        AddLine(left, top, left, height - bottom, AxisBrush, 1.2);
        AddLine(left, height - bottom, width - right, height - bottom, AxisBrush, 1.2);
        AddText(_usesSteps ? "Количество операций" : "Время выполнения · мс", 4, 5, 220, TextBrush, fontSize: 12);
        AddText("Размер входных данных · N", width - right - 190, height - 24, 190, TextBrush, TextAlignment.Right, fontSize: 12);
    }

    // ---------- Тултипы 3D-сцены ----------

    private void PlotViewport_MouseMove(object sender, MouseEventArgs e)
    {
        string? tip = null;
        var hitParams = new PointHitTestParameters(e.GetPosition(PlotViewport));
        VisualTreeHelper.HitTest(PlotViewport, null, result =>
        {
            if (result is RayHitTestResult rayHit
                && rayHit.VisualHit is ModelVisual3D model
                && ToolTipService.GetToolTip(model) is string text)
            {
                tip = text;
                return HitTestResultBehavior.Stop;
            }
            return HitTestResultBehavior.Continue;
        }, hitParams);

        if (tip is null)
        {
            SurfaceTip.IsOpen = false;
            return;
        }
        SurfaceTipText.Text = tip;
        SurfaceTip.IsOpen = true;
    }

    private void PlotViewport_MouseLeave(object sender, MouseEventArgs e) => SurfaceTip.IsOpen = false;

    private void AddPolyline(IEnumerable<Point> points, Brush brush, double thickness, bool dashed = false, double opacity = 1)
    {
        var polyline = new Polyline
        {
            Points = new PointCollection(points),
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            SnapsToDevicePixels = true,
            Opacity = opacity
        };
        if (dashed)
            polyline.StrokeDashArray = new DoubleCollection { 2.3, 1.8 };
        Panel.SetZIndex(polyline, 2);
        PlotCanvas.Children.Add(polyline);
    }

    private void AddLine(Point from, Point to, Brush brush, double thickness) =>
        AddLine(from.X, from.Y, to.X, to.Y, brush, thickness);

    private void AddLine(double x1, double y1, double x2, double y2, Brush brush, double thickness)
    {
        var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = thickness };
        PlotCanvas.Children.Add(line);
    }

    private void AddText(string text, double x, double y, double width, Brush brush,
        TextAlignment alignment = TextAlignment.Left, double fontSize = 10)
    {
        var label = new TextBlock
        {
            Text = text,
            Width = width,
            Foreground = brush,
            FontSize = fontSize,
            FontWeight = fontSize >= 13 ? FontWeights.SemiBold : FontWeights.Normal,
            TextAlignment = alignment,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        PlotCanvas.Children.Add(label);
    }

    // ---------- База данных ----------

    private async void SaveDbButton_Click(object sender, RoutedEventArgs e)
    {
        var hasSeries = _grid3d.Count > 0 ? _currentRunResults.Count > 0 : _points.Count > 0 && _currentRunResults.Count > 0;
        if (!hasSeries)
        {
            StatusText.Text = _points.Count > 0 || _grid3d.Count > 0
                ? "Все точки серии уже в кэше БД — сохранять нечего."
                : "Нет результатов для сохранения — сначала постройте график.";
            return;
        }

        SaveDbButton.IsEnabled = false;
        try
        {
            await UiDatabase.EnsureInitializedAsync();
            var db = UiDatabase.CreateDatabaseService();
            var savedIds = new List<int>();

            var entity = _currentEntity ?? await UiDatabase.ResolveAlgorithmAsync(_currentAlgorithmRef!);
            var mainExperiment = new Experiment
            {
                AlgorithmId = entity.Id,
                Algorithm = entity,
                Date = DateTime.UtcNow,
                N_max = _currentMaxN,
                Step = _currentStep,
                RunsCount = _currentRuns,
                DataType = _currentDataType
            };
            await db.SaveExperimentAsync(mainExperiment, _currentRunResults);
            savedIds.Add(mainExperiment.Id);

            foreach (var series in _comparison.Where(s => s.Results.Count > 0))
            {
                var seriesEntity = await UiDatabase.ResolveAlgorithmAsync(series.Algorithm);
                var seriesExperiment = new Experiment
                {
                    AlgorithmId = seriesEntity.Id,
                    Algorithm = seriesEntity,
                    Date = DateTime.UtcNow,
                    N_max = _currentMaxN,
                    Step = _currentStep,
                    RunsCount = _currentRuns,
                    DataType = _currentDataType
                };
                await db.SaveExperimentAsync(seriesExperiment, series.Results);
                savedIds.Add(seriesExperiment.Id);
            }

            StatusText.Text = savedIds.Count == 1
                ? $"Сохранено в БД · эксперимент #{savedIds[0]} · {_currentRunResults.Count} замеров"
                : $"Сохранено в БД · эксперименты {string.Join(", ", savedIds.Select(id => "#" + id))}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Не удалось сохранить в базу: " + ex.Message;
        }
        finally
        {
            SaveDbButton.IsEnabled = true;
        }
    }

    private async void LoadDbButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAlgorithm is not { } algorithm)
            return;

        try
        {
            await UiDatabase.EnsureInitializedAsync();

            Algorithm? entity;
            List<Experiment> experiments;
            using (var context = UiDatabase.CreateContext())
            {
                entity = await context.Algorithms.FirstOrDefaultAsync(a => a.Name == algorithm.Name);
                if (entity is null)
                {
                    StatusText.Text = "В базе нет сохранённых экспериментов для этого алгоритма.";
                    return;
                }
                experiments = await UiDatabase.CreateDatabaseService().GetExperimentsByAlgorithmAsync(entity.Id);
            }

            if (experiments.Count == 0)
            {
                StatusText.Text = "В базе нет сохранённых экспериментов для этого алгоритма.";
                return;
            }

            var dialog = new DbExperimentsDialog(experiments) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.Selected is not { } selected)
                return;

            var db = UiDatabase.CreateDatabaseService();
            var results = await db.GetResultsForExperimentAsync(selected.Id);
            if (results.Count == 0)
            {
                StatusText.Text = $"Эксперимент #{selected.Id} не содержит результатов.";
                return;
            }

            ResetComparison();
            _currentAlgorithmRef = algorithm;
            _currentEntity = entity;
            _currentMaxN = selected.N_max;
            _currentStep = selected.Step;
            _currentRuns = selected.RunsCount;
            _currentRuns = selected.RunsCount;
            _currentDataType = selected.DataType;
            _lastRunCount = selected.RunsCount;

            if (entity.SupportsMatrixDimensions && results.All(r => r.M.HasValue))
            {
                // 3D-эксперимент: собираем поверхность из ячеек (N, M)
                _grid3d = results
                    .GroupBy(r => (N: r.N, M: r.M!.Value))
                    .OrderBy(g => g.Key.N).ThenBy(g => g.Key.M)
                    .Select(g => new GridPoint(g.Key.N, g.Key.M, g.Average(r => r.TimeMs)))
                    .ToList();
                _currentRunResults = results;
                _usesSteps = false;
                EmptyState.Visibility = Visibility.Collapsed;
                SaveDbButton.IsEnabled = true;
                UpdateSummary(algorithm);
                UpdateLegend();
                RedrawChart();
                ChartSubtitle.Text = $"{algorithm.Name} · 3D из БД · эксперимент #{selected.Id} от {selected.Date.ToLocalTime():dd.MM.yyyy HH:mm}";
                StatusText.Text = $"Загружено из БД · {_grid3d.Count} ячеек сетки";
                RunProgress.Value = 0;
                return;
            }

            var usesSteps = results.Any(r => r.Steps > 0);
            var points = results
                .GroupBy(r => r.N)
                .OrderBy(g => g.Key)
                .Select(g => new PlotPoint(g.Key, usesSteps ? g.Average(r => (double)r.Steps) : g.Average(r => r.TimeMs)))
                .ToList();

            _usesSteps = usesSteps;
            _points = points;
            _approximation = FitExpectedCurve(points, entity.TheoreticalComplexity);
            _currentRunResults = results;
            _grid3d.Clear();

            EmptyState.Visibility = Visibility.Collapsed;
            SaveDbButton.IsEnabled = true;
            UpdateSummary(algorithm);
            UpdateLegend();
            RedrawChart();
            ChartSubtitle.Text = $"{algorithm.Name} · из БД · эксперимент #{selected.Id} от {selected.Date.ToLocalTime():dd.MM.yyyy HH:mm}";
            StatusText.Text = $"Загружено из БД · {points.Count} точек, {results.Count} замеров";
            RunProgress.Value = 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Не удалось загрузить из базы: " + ex.Message;
        }
    }

    // ---------- Очередь экспериментов ----------

    private void AddToQueueButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadRunParameters(out var algorithm, out var maxN, out var step, out var runs, out var dataType, out var error))
        {
            QueueStatusText.Text = error;
            return;
        }

        _queueItems.Add(new QueueItem(algorithm, maxN, step, runs, dataType));
        UpdateQueueStatus();
        RunQueueButton.IsEnabled = !_queueRunning;
        QueueStatusText.Text = $"В очереди: {_queueItems.Count}. Нажмите «Выполнить очередь».";
    }

    private void RemoveQueueItem_Click(object sender, RoutedEventArgs e)
    {
        if (_queueRunning)
            return;
        if ((sender as FrameworkElement)?.DataContext is QueueItem item)
        {
            _queueItems.Remove(item);
            UpdateQueueStatus();
        }
    }

    private void UpdateQueueStatus()
    {
        var pending = _queueItems.Count(i => !i.Completed);
        QueueStatusText.Text = _queueItems.Count == 0
            ? "Очередь пуста"
            : pending == 0
                ? $"Все {_queueItems.Count} серий выполнены. Кликните по серии, чтобы открыть её график."
                : $"В очереди {_queueItems.Count} серий, из них ожидают: {pending}.";
        RunQueueButton.IsEnabled = !_queueRunning && pending > 0;
        ClearQueueButton.IsEnabled = !_queueRunning && _queueItems.Count > 0;
    }

    private void ClearQueueButton_Click(object sender, RoutedEventArgs e)
    {
        if (_queueRunning)
            return;
        _queueItems.Clear();
        UpdateQueueStatus();
    }

    private void QueueList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QueueList.SelectedItem is QueueItem { Completed: true } item)
            ViewSeries(item);
        else
            QueueList.SelectedItem = null;
    }

    /// <summary>Отображает серию из элемента очереди на графике и делает её текущей (для сохранения в БД).</summary>
    private void ViewSeries(QueueItem item)
    {
        ResetComparison();
        _grid3d.Clear();
        _points = item.Points!;
        _currentRunResults = item.RunResults!;
        _approximation = item.Approximation!;
        _usesSteps = item.Algorithm.SupportsStepCounting;
        _lastRunCount = item.Runs;
        _currentAlgorithmRef = item.Algorithm;
        _currentEntity = item.SavedEntity;
        _currentMaxN = item.MaxN;
        _currentStep = item.Step;
        _currentRuns = item.Runs;
        _currentDataType = item.DataType;

        EmptyState.Visibility = Visibility.Collapsed;
        SaveDbButton.IsEnabled = true;
        UpdateSummary(item.Algorithm);
        UpdateLegend();
        RedrawChart();
        ChartSubtitle.Text = $"{item.Algorithm.Name} · очередь · {item.ParamsSummary}";
        StatusText.Text = $"Показана серия из очереди · {_points.Count} точек";
    }

    private async void RunQueueButton_Click(object sender, RoutedEventArgs e)
    {
        if (_queueRunning || _queueItems.Count == 0)
            return;

        _queueRunning = true;
        _queueCancellation = new CancellationTokenSource();
        RunQueueButton.IsEnabled = false;
        AddToQueueButton.IsEnabled = false;
        ClearQueueButton.IsEnabled = false;
        RunButton.IsEnabled = false;
        LoadDbButton.IsEnabled = false;
        SaveDbButton.IsEnabled = false;
        CancelButton.IsEnabled = true;

        var useCache = UseCacheCheck.IsChecked == true;
        var token = _queueCancellation.Token;

        try
        {
            await UiDatabase.EnsureInitializedAsync();

            foreach (var item in _queueItems.Where(i => !i.Completed).ToList())
            {
                item.MarkRunning();
                RunProgress.Value = 0;
                QueueStatusText.Text = $"Выполнение: {item.Title}";
                StatusText.Text = $"Очередь: {item.Algorithm.Name} · N ≤ {item.MaxN:N0}";

                try
                {
                    var (fitN, fitStep, _) = AutoFitSeries(item.Algorithm, item.MaxN, item.Step, item.Runs, item.DataType);
                    var entity = await UiDatabase.ResolveAlgorithmAsync(item.Algorithm);
                    var sizes = MakeSizes(fitN, fitStep);

                    var progress = new Progress<(int Completed, int Total, int CurrentN)>(p =>
                    {
                        item.UpdateProgress(p.Completed, p.Total);
                        RunProgress.Value = (double)p.Completed / p.Total * 100;
                    });

                    var (points, runResults, cached) = await Task.Run(
                        () => RunSeries2DAsync(item.Algorithm, entity, sizes, item.Runs, item.DataType, useCache, token, progress),
                        token);

                    item.Points = points;
                    item.RunResults = runResults;
                    item.CachedPoints = cached;
                    item.Approximation = FitExpectedCurve(points, item.Algorithm.TheoreticalComplexity);
                    item.Completed = true;

                    if (QueueSaveCheckBox.IsChecked == true)
                    {
                        var experiment = new Experiment
                        {
                            AlgorithmId = entity.Id,
                            Algorithm = entity,
                            Date = DateTime.UtcNow,
                            N_max = fitN,
                            Step = fitStep,
                            RunsCount = item.Runs,
                            DataType = item.DataType
                        };
                        await UiDatabase.CreateDatabaseService().SaveExperimentAsync(experiment, runResults);
                        item.SavedEntity = entity;
                        item.MarkDoneSaved(experiment.Id);
                    }
                    else
                    {
                        item.MarkDone();
                    }
                }
                catch (OperationCanceledException)
                {
                    item.MarkCanceled();
                    QueueStatusText.Text = "Очередь остановлена пользователем.";
                    break;
                }
                catch (Exception ex)
                {
                    item.MarkFailed(ex.Message);
                    QueueStatusText.Text = $"Ошибка в серии «{item.Algorithm.Name}»: {ex.Message}";
                }
            }

            var lastCompleted = _queueItems.LastOrDefault(i => i.Completed);
            if (lastCompleted is not null)
            {
                QueueList.SelectedItem = lastCompleted;
                ViewSeries(lastCompleted);
            }
        }
        finally
        {
            _queueCancellation.Dispose();
            _queueCancellation = null;
            _queueRunning = false;
            RunButton.IsEnabled = true;
            LoadDbButton.IsEnabled = true;
            AddToQueueButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            RunProgress.Value = 100;
            UpdateQueueStatus();
        }
    }

    // ---------- Интерфейс сравнения ----------

    private void CompareCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (PickCompareButton is not null)
            PickCompareButton.Visibility = CompareCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void PickCompareButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CompareSelectDialog(_algorithms, _compareSelection.Select(a => a.Name), SelectedAlgorithm?.Name ?? string.Empty) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _compareSelection = dialog.Selected;
            PickCompareButton.Content = $"Выбрано для сравнения: {_compareSelection.Count}";
            if (_compareSelection.Count > 0)
                QueueStatusText.Text = $"К сравнению выбрано: {string.Join(", ", _compareSelection.Select(a => a.Name))}";
        }
        await Task.CompletedTask;
    }

    /// <summary>Сбрасывает серии сравнения.</summary>
    private void ResetComparison()
    {
        _comparison.Clear();
        if (LegendPanel is not null)
            UpdateLegend();
    }

    private DataType GetSelectedDataType() => DataTypeCombo.SelectedIndex switch
    {
        1 => DataType.Sorted,
        2 => DataType.Reversed,
        _ => DataType.Random
    };

    // ---------- Вспомогательные ----------

    private static int GetMaximumAllowedN(IAlgorithm algorithm) => algorithm switch
    {
        BubbleSort => 50000,
        MatrixMultiplication or StrassenMultiplication => 2048,
        LevenshteinAlgorithm => 30000,
        PowerRecursive => 20000,
        PowerIterative => 1000000000,
        PowerBinary => 1000000000,
        PolynomialNaive => 50000,
        _ => 20000000
    };

    /// <summary>Размер входа для пилотного замера при подборе масштаба.</summary>
    private static int PilotSize(IAlgorithm algorithm) =>
        algorithm.SupportsMatrixDimensions ? 128 : 1000;

    /// <summary>Степень двойки, не меньше указанного размера (нужно для Штрассена).</summary>
    private static int NextPowerOfTwo(int n)
    {
        var p = 1;
        while (p < n)
            p <<= 1;
        return p;
    }

    /// <summary>Палитра цветов серий сравнения (основная серия — бирюзовая).</summary>
    public static Brush[] ComparisonPalette() =>
    [
        new SolidColorBrush(Color.FromRgb(177, 140, 255)),
        new SolidColorBrush(Color.FromRgb(255, 143, 120)),
        new SolidColorBrush(Color.FromRgb(255, 209, 102)),
        new SolidColorBrush(Color.FromRgb(111, 183, 255)),
        new SolidColorBrush(Color.FromRgb(255, 143, 212)),
        new SolidColorBrush(Color.FromRgb(140, 233, 154))
    ];

    public static string ComplexityShortNamePublic(ComplexityType complexity) => ComplexityShortName(complexity);

    /// <summary>Компактный формат чисел для подписей осей: 1500 → «1,5K», 2 000 000 → «2M».</summary>
    private static string CompactNumber(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1_000_000)
            return (value / 1_000_000).ToString("0.#", CultureInfo.CurrentCulture) + "M";
        if (abs >= 1_000)
            return (value / 1_000).ToString("0.#", CultureInfo.CurrentCulture) + "K";
        return value.ToString("0.##", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Деления оси, кратные 5: шаг из набора {0.005, 0.025, 0.05, 0.1, 0.25, 0.5, 2.5, 5, 10, 25, 50, …},
    /// максимум округляется вверх до кратного шагу значения.
    /// </summary>
    private static (double Max, double Step, int Decimals) NiceScale(double rawMax, int divisions)
    {
        double[] steps =
        [
            0.005, 0.025, 0.05, 0.1, 0.25, 0.5, 2.5, 5, 10, 25, 50, 100, 250, 500,
            1000, 2500, 5000, 10000, 25000, 50000, 100000, 250000, 500000,
            1000000, 2500000, 5000000, 10000000
        ];
        var target = rawMax <= 0 ? 1 : rawMax;
        var step = steps[^1];
        foreach (var s in steps)
        {
            if (s >= target / divisions)
            {
                step = s;
                break;
            }
        }

        var niceMax = Math.Ceiling(target / step - 1e-9) * step;
        int decimals = step < 0.01 ? 3 : step < 1 ? 2 : step < 5 ? 1 : 0;
        return (niceMax, step, decimals);
    }

    private static string FormatTick(double value, int decimals)
    {
        if (value >= 1_000_000)
            return (value / 1_000_000).ToString("0.##", CultureInfo.CurrentCulture) + "M";
        if (decimals > 0)
            return value.ToString("F" + decimals, CultureInfo.CurrentCulture);
        return value.ToString("N0", CultureInfo.CurrentCulture);
    }

    private static double ComplexityBasis(int n, ComplexityType complexity) => complexity switch
    {
        ComplexityType.Constant => 1,
        ComplexityType.Logarithmic => Math.Log(n),
        ComplexityType.Linear => n,
        ComplexityType.Linearithmic => n * Math.Log(n),
        ComplexityType.Quadratic => (double)n * n,
        ComplexityType.Cubic => (double)n * n * n,
        _ => n
    };

    private static string ComplexityShortName(ComplexityType complexity) => complexity switch
    {
        ComplexityType.Constant => "1",
        ComplexityType.Logarithmic => "log N",
        ComplexityType.Linear => "N",
        ComplexityType.Linearithmic => "N log N",
        ComplexityType.Quadratic => "N²",
        ComplexityType.Cubic => "N³",
        _ => "N"
    };

    public readonly record struct PlotPoint(int N, double Value, bool FromCache = false);

    /// <summary>Ячейка 3D-сетки: время умножения A(n×m) × B(m×n).</summary>
    public readonly record struct GridPoint(int N, int M, double Value, bool FromCache = false);

    /// <summary>Серия сравнения на общем поле.</summary>
    public sealed class ComparisonSeries
    {
        public required IAlgorithm Algorithm { get; init; }
        public required Brush Brush { get; init; }
        public List<PlotPoint> Points { get; set; } = [];
        public List<PlotPoint> Approximation { get; set; } = [];
        public List<ExperimentResult> Results { get; set; } = [];

        public Brush AreaBrush => Brush is SolidColorBrush scb
            ? new SolidColorBrush(Color.FromArgb(30, scb.Color.R, scb.Color.G, scb.Color.B))
            : Brushes.Transparent;
    }

    /// <summary>Элемент очереди: одна серия экспериментов со своим статусом выполнения.</summary>
    public sealed class QueueItem : INotifyPropertyChanged
    {
        private static readonly Brush PendingBrush = new SolidColorBrush(Color.FromRgb(137, 149, 173));
        private static readonly Brush RunningBrush = new SolidColorBrush(Color.FromRgb(232, 211, 107));
        private static readonly Brush DoneBrush = new SolidColorBrush(Color.FromRgb(105, 216, 194));
        private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(255, 143, 120));

        public QueueItem(IAlgorithm algorithm, int maxN, int step, int runs, DataType dataType)
        {
            Algorithm = algorithm;
            MaxN = maxN;
            Step = step;
            Runs = runs;
            DataType = dataType;
        }

        public IAlgorithm Algorithm { get; }
        public int MaxN { get; }
        public int Step { get; }
        public int Runs { get; }
        public DataType DataType { get; }

        public string ParamsSummary => $"N ≤ {MaxN:N0} · шаг {Step} · {Runs} зап.";
        public string Title => $"{Algorithm.Name} · {ParamsSummary}";

        public List<PlotPoint>? Points { get; set; }
        public List<PlotPoint>? Approximation { get; set; }
        public List<ExperimentResult>? RunResults { get; set; }
        public Algorithm? SavedEntity { get; set; }
        public int CachedPoints { get; set; }
        public bool Completed { get; set; }

        private string _statusText = "Ожидает";
        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        private Brush _statusBrush = PendingBrush;
        public Brush StatusBrush
        {
            get => _statusBrush;
            private set { _statusBrush = value; OnPropertyChanged(); }
        }

        public void MarkRunning() { StatusBrush = RunningBrush; StatusText = "Выполняется…"; }
        public void UpdateProgress(int completed, int total) { StatusText = $"Выполняется · {completed}/{total}"; }
        public void MarkDone() { StatusBrush = DoneBrush; StatusText = "Готово"; Completed = true; }
        public void MarkDoneSaved(int experimentId) { StatusBrush = DoneBrush; StatusText = $"Готово · в БД #{experimentId}"; Completed = true; }
        public void MarkCanceled() { StatusBrush = PendingBrush; StatusText = "Отменено"; }
        public void MarkFailed(string message) { StatusBrush = ErrorBrush; StatusText = $"Ошибка: {message}"; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private int _lastRunCount = 3;
}
