using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private static readonly Brush ExperimentBrush = new SolidColorBrush(Color.FromRgb(105, 216, 194));
    private static readonly Brush ApproximationBrush = new SolidColorBrush(Color.FromRgb(255, 143, 120));
    private static readonly Brush AreaBrush = new SolidColorBrush(Color.FromArgb(30, 105, 216, 194));

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
    }

    private IAlgorithm? SelectedAlgorithm => AlgorithmCombo.SelectedItem as IAlgorithm;

    private void AlgorithmCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var algorithm = SelectedAlgorithm;
        if (algorithm is null || PlotCanvas is null)
            return;

        _usesSteps = algorithm.SupportsStepCounting;
        AlgorithmDescription.Text = algorithm.Description;
        var limit = GetMaximumAllowedN(algorithm);
        MaxNBox.Text = algorithm switch
        {
            MatrixMultiplication or StrassenMultiplication => "256",
            BubbleSort => "2000",
            LevenshteinAlgorithm => "1000",
            PowerRecursive => "1000",
            _ => "2000"
        };
        DataTypeCombo.IsEnabled = algorithm.SupportsDataType;
        StepBox.Text = algorithm is MatrixMultiplication or StrassenMultiplication ? "16" : "100";
        ChartSubtitle.Text = algorithm.SupportsStepCounting
            ? $"{algorithm.Name} · рост количества операций, ожидаемая оценка O({ComplexityShortName(algorithm.TheoreticalComplexity)})"
            : $"{algorithm.Name} · время выполнения при увеличении размера входных данных";

        _points.Clear();
        _approximation.Clear();
        _currentRunResults.Clear();
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
        _points.Clear();
        _approximation.Clear();
        EmptyState.Visibility = Visibility.Visible;
        DrawEmptyChartFrame();
        _cancellation = new CancellationTokenSource();
        RunButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        RunProgress.Value = 0;
        StatusText.Text = "Подготовка эксперимента…";
        ChartSubtitle.Text = $"{algorithm.Name} · 0 из {MakeSizes(maxN, step).Count} размеров";

        var progress = new Progress<(int Completed, int Total, int CurrentN)>(p =>
        {
            RunProgress.Value = (double)p.Completed / p.Total * 100;
            StatusText.Text = $"Измерение N = {p.CurrentN:N0} · {p.Completed} из {p.Total}";
            ChartSubtitle.Text = $"{algorithm.Name} · {p.Completed} из {p.Total} размеров";
        });

        try
        {
            var result = await Task.Run(
                () => RunMeasurements(algorithm, MakeSizes(maxN, step), runs, dataType, _cancellation.Token, progress),
                _cancellation.Token);

            _points = result.Points;
            _currentRunResults = result.Results;
            _usesSteps = algorithm.SupportsStepCounting;
            _lastRunCount = runs;
            _currentAlgorithmRef = algorithm;
            _currentEntity = null;
            _currentMaxN = maxN;
            _currentStep = step;
            _currentRuns = runs;
            _currentDataType = dataType;
            _approximation = FitExpectedCurve(_points, algorithm.TheoreticalComplexity);
            EmptyState.Visibility = Visibility.Collapsed;
            SaveDbButton.IsEnabled = true;
            UpdateSummary(algorithm);
            RedrawChart();
            RunProgress.Value = 100;
            StatusText.Text = $"Готово · {_points.Count} точек, по {runs} запуск(а/ов) на каждую";
            ChartSubtitle.Text = $"{algorithm.Name} · {(_usesSteps ? "число операций" : "время выполнения")}";
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

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellation?.Cancel();
        _queueCancellation?.Cancel();
    }

    private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_points.Count > 0)
            RedrawChart();
        else
            DrawEmptyChartFrame();
    }

    private static (List<PlotPoint> Points, List<ExperimentResult> Results) RunMeasurements(
        IAlgorithm algorithm,
        IReadOnlyList<int> sizes,
        int runs,
        DataType dataType,
        CancellationToken cancellationToken,
        IProgress<(int Completed, int Total, int CurrentN)> progress)
    {
        var points = new List<PlotPoint>(sizes.Count);
        var runResults = new List<ExperimentResult>(sizes.Count * runs);
        var random = new Random(7411);
        var matrix = algorithm is MatrixMultiplication or StrassenMultiplication;

        for (var index = 0; index < sizes.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var n = sizes[index];
            double total = 0;

            for (var run = 0; run < runs; run++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var input = CreateInput(algorithm, n, dataType, random);
                var measurement = algorithm.Execute(input);
                total += algorithm.SupportsStepCounting ? measurement.Steps : measurement.TimeMs;
                runResults.Add(new ExperimentResult
                {
                    N = n,
                    M = matrix ? n : null,
                    RunNumber = run + 1,
                    TimeMs = measurement.TimeMs,
                    Steps = measurement.Steps
                });
            }

            points.Add(new PlotPoint(n, total / runs));
            progress.Report((index + 1, sizes.Count, n));
        }

        return (points, runResults);
    }

    private bool TryReadRunParameters(
        out IAlgorithm? algorithm, out int maxN, out int step, out int runs, out DataType dataType, out string? error)
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
        if (MakeSizes(maxN, step).Count > 120)
        {
            error = "Слишком много точек. Увеличьте шаг так, чтобы на графике было не больше 120 размеров.";
            return false;
        }

        algorithm = algo;
        dataType = GetSelectedDataType();
        return true;
    }

    private static AlgorithmInput CreateInput(IAlgorithm algorithm, int n, DataType dataType, Random random)
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
            M = algorithm is MatrixMultiplication or StrassenMultiplication ? n : null,
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

    private static List<int> MakeSizes(int maxN, int step)
    {
        var sizes = new List<int>();
        for (var n = step; n <= maxN; n += step)
            sizes.Add(n);
        if (sizes.Count == 0 || sizes[^1] != maxN)
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

    private void UpdateSummary(IAlgorithm algorithm)
    {
        var mean = _points.Average(point => point.Value);
        MeanMetric.Text = _usesSteps ? $"{mean:N0} оп." : $"{mean:0.####} мс";
        ComplexityMetric.Text = $"O({ComplexityShortName(algorithm.TheoreticalComplexity)})";
        ComplexityNote.Text = "теоретическая сложность алгоритма";
        PointsMetric.Text = _points.Count.ToString(CultureInfo.CurrentCulture);
        PointsNote.Text = $"по {_lastRunCount} запуск(а/ов) на точку";
    }

    private int _lastRunCount = 3;

    private void RedrawChart()
    {
        if (_points.Count == 0 || PlotCanvas.ActualWidth < 150 || PlotCanvas.ActualHeight < 130)
            return;

        PlotCanvas.Children.Clear();
        var width = PlotCanvas.ActualWidth;
        var height = PlotCanvas.ActualHeight;
        const double left = 76;
        const double right = 24;
        const double top = 29;
        const double bottom = 49;
        var plotWidth = Math.Max(1, width - left - right);
        var plotHeight = Math.Max(1, height - top - bottom);
        var minX = _points.Min(point => point.N);
        var maxX = _points.Max(point => point.N);
        if (maxX == minX)
            maxX = minX + 1;
        var maxY = Math.Max(_points.Max(point => point.Value), _approximation.Count == 0 ? 0 : _approximation.Max(point => point.Value));
        maxY = maxY <= 0 ? 1 : maxY * 1.12;

        AddText(_usesSteps ? "Количество операций" : "Время выполнения · мс", 4, 5, 180, TextBrush);
        for (var i = 0; i <= 5; i++)
        {
            var y = top + plotHeight * i / 5;
            var value = maxY * (5 - i) / 5;
            AddLine(left, y, width - right, y, GridBrush, 1);
            AddText(_usesSteps ? CompactNumber(value) : value.ToString("0.####", CultureInfo.CurrentCulture), 0, y - 8, 68, TextBrush, TextAlignment.Right);
        }

        for (var i = 0; i <= 5; i++)
        {
            var x = left + plotWidth * i / 5;
            var value = minX + (maxX - minX) * i / 5d;
            AddLine(x, top, x, top + plotHeight, GridBrush, 1);
            AddText(CompactNumber(value), x - 28, top + plotHeight + 8, 56, TextBrush, TextAlignment.Center);
        }

        AddLine(left, top, left, top + plotHeight, AxisBrush, 1.2);
        AddLine(left, top + plotHeight, width - right, top + plotHeight, AxisBrush, 1.2);
        AddText("Размер входных данных · N", width - right - 165, height - 22, 165, TextBrush, TextAlignment.Right);

        Point Map(PlotPoint point) => new(
            left + (point.N - minX) / (double)(maxX - minX) * plotWidth,
            top + plotHeight - point.Value / maxY * plotHeight);

        if (_approximation.Count > 1)
            AddPolyline(_approximation.Select(Map), ApproximationBrush, 2.2, dashed: true);
        if (_points.Count > 1)
        {
            var areaPoints = _points.Select(Map).ToList();
            areaPoints.Add(new Point(left + plotWidth, top + plotHeight));
            areaPoints.Add(new Point(left, top + plotHeight));
            var area = new Polygon
            {
                Points = new PointCollection(areaPoints),
                Fill = AreaBrush,
                StrokeThickness = 0
            };
            Panel.SetZIndex(area, 1);
            PlotCanvas.Children.Add(area);

            AddPolyline(_points.Select(Map), ExperimentBrush, 2.4);
        }
        foreach (var point in _points)
        {
            var position = Map(point);
            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = ExperimentBrush,
                Stroke = new SolidColorBrush(Color.FromRgb(17, 24, 42)),
                StrokeThickness = 1.2,
                ToolTip = $"N = {point.N:N0}{Environment.NewLine}{(_usesSteps ? "Операции" : "Время")}: {(_usesSteps ? point.Value.ToString("N0", CultureInfo.CurrentCulture) : point.Value.ToString("0.####", CultureInfo.CurrentCulture) + " мс")}"
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
        PlotCanvas.Children.Clear();
        var width = PlotCanvas.ActualWidth;
        var height = PlotCanvas.ActualHeight;
        const double left = 76;
        const double right = 24;
        const double top = 29;
        const double bottom = 49;
        for (var i = 0; i <= 5; i++)
        {
            var y = top + (height - top - bottom) * i / 5;
            AddLine(left, y, width - right, y, GridBrush, 1);
        }
        AddLine(left, top, left, height - bottom, AxisBrush, 1.2);
        AddLine(left, height - bottom, width - right, height - bottom, AxisBrush, 1.2);
        AddText(_usesSteps ? "Количество операций" : "Время выполнения · мс", 4, 5, 180, TextBrush);
        AddText("Размер входных данных · N", width - right - 165, height - 22, 165, TextBrush, TextAlignment.Right);
    }

    private void AddPolyline(IEnumerable<Point> points, Brush brush, double thickness, bool dashed = false)
    {
        var polyline = new Polyline
        {
            Points = new PointCollection(points),
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            SnapsToDevicePixels = true
        };
        if (dashed)
            polyline.StrokeDashArray = new DoubleCollection { 2.3, 1.8 };
        Panel.SetZIndex(polyline, 2);
        PlotCanvas.Children.Add(polyline);
    }

    private void AddLine(double x1, double y1, double x2, double y2, Brush brush, double thickness)
    {
        var line = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = thickness };
        PlotCanvas.Children.Add(line);
    }

    private void AddText(string text, double x, double y, double width, Brush brush, TextAlignment alignment = TextAlignment.Left)
    {
        var label = new TextBlock
        {
            Text = text,
            Width = width,
            Foreground = brush,
            FontSize = 10,
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
        if (_points.Count == 0 || _currentRunResults.Count == 0)
        {
            StatusText.Text = "Нет результатов для сохранения — сначала постройте график.";
            return;
        }

        SaveDbButton.IsEnabled = false;
        try
        {
            await UiDatabase.EnsureInitializedAsync();
            var entity = _currentEntity ?? await UiDatabase.ResolveAlgorithmAsync(_currentAlgorithmRef!);
            var experiment = new Experiment
            {
                AlgorithmId = entity.Id,
                Algorithm = entity,
                Date = DateTime.UtcNow,
                N_max = _currentMaxN,
                Step = _currentStep,
                RunsCount = _currentRuns,
                DataType = _currentDataType
            };

            await UiDatabase.CreateDatabaseService().SaveExperimentAsync(experiment, _currentRunResults);
            StatusText.Text = $"Сохранено в БД · эксперимент #{experiment.Id} · {_currentRunResults.Count} замеров";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Не удалось сохранить в базу: " + ex.Message;
        }
        finally
        {
            SaveDbButton.IsEnabled = _points.Count > 0;
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

            var usesSteps = results.Any(r => r.Steps > 0);
            var points = results
                .GroupBy(r => r.N)
                .OrderBy(g => g.Key)
                .Select(g => new PlotPoint(g.Key, usesSteps ? g.Average(r => (double)r.Steps) : g.Average(r => r.TimeMs)))
                .ToList();

            _usesSteps = usesSteps;
            _lastRunCount = selected.RunsCount;
            _points = points;
            _approximation = FitExpectedCurve(points, entity.TheoreticalComplexity);
            _currentRunResults = results;
            _currentAlgorithmRef = algorithm;
            _currentEntity = entity;
            _currentMaxN = selected.N_max;
            _currentStep = selected.Step;
            _currentRuns = selected.RunsCount;
            _currentDataType = selected.DataType;

            EmptyState.Visibility = Visibility.Collapsed;
            SaveDbButton.IsEnabled = true;
            UpdateSummary(algorithm);
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

        var db = UiDatabase.CreateDatabaseService();
        var autoSave = QueueSaveCheckBox.IsChecked == true;
        var token = _queueCancellation.Token;

        try
        {
            foreach (var item in _queueItems.Where(i => !i.Completed).ToList())
            {
                item.MarkRunning();
                RunProgress.Value = 0;
                QueueStatusText.Text = $"Выполнение: {item.Title}";
                StatusText.Text = $"Очередь: {item.Algorithm.Name} · N ≤ {item.MaxN:N0}";

                try
                {
                    var progress = new Progress<(int Completed, int Total, int CurrentN)>(p =>
                    {
                        item.UpdateProgress(p.Completed, p.Total);
                        RunProgress.Value = (double)p.Completed / p.Total * 100;
                    });

                    var (points, runResults) = await Task.Run(
                        () => RunMeasurements(item.Algorithm, MakeSizes(item.MaxN, item.Step), item.Runs, item.DataType, token, progress),
                        token);

                    item.Points = points;
                    item.RunResults = runResults;
                    item.Approximation = FitExpectedCurve(points, item.Algorithm.TheoreticalComplexity);
                    item.Completed = true;

                    if (autoSave)
                    {
                        var entity = await UiDatabase.ResolveAlgorithmAsync(item.Algorithm);
                        var experiment = new Experiment
                        {
                            AlgorithmId = entity.Id,
                            Algorithm = entity,
                            Date = DateTime.UtcNow,
                            N_max = item.MaxN,
                            Step = item.Step,
                            RunsCount = item.Runs,
                            DataType = item.DataType
                        };
                        await db.SaveExperimentAsync(experiment, runResults);
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

    private DataType GetSelectedDataType() => DataTypeCombo.SelectedIndex switch
    {
        1 => DataType.Sorted,
        2 => DataType.Reversed,
        _ => DataType.Random
    };

    private static int GetMaximumAllowedN(IAlgorithm algorithm) => algorithm switch
    {
        BubbleSort => 3000,
        MatrixMultiplication or StrassenMultiplication => 256,
        LevenshteinAlgorithm => 2500,
        PowerRecursive => 1000,
        _ => 100000
    };

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

    public readonly record struct PlotPoint(int N, double Value);

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
}
