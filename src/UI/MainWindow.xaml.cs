using System.Globalization;
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

namespace UI;

public partial class MainWindow : Window
{
    private static readonly Brush GridBrush = new SolidColorBrush(Color.FromRgb(36, 48, 70));
    private static readonly Brush AxisBrush = new SolidColorBrush(Color.FromRgb(105, 119, 145));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(137, 149, 173));
    private static readonly Brush ExperimentBrush = new SolidColorBrush(Color.FromRgb(105, 216, 194));
    private static readonly Brush ApproximationBrush = new SolidColorBrush(Color.FromRgb(255, 143, 120));

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

    public MainWindow()
    {
        InitializeComponent();
        AlgorithmCombo.ItemsSource = _algorithms;
        AlgorithmCombo.SelectedItem = _algorithms[1];
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
        RunProgress.Value = 0;
        MeanMetric.Text = "—";
        ComplexityMetric.Text = "—";
        ComplexityNote.Text = "ожидаемая сложность";
        PointsMetric.Text = "—";
        PointsNote.Text = "размеров входных данных";
        EmptyState.Visibility = Visibility.Visible;
        PlotCanvas.Children.Clear();
        StatusText.Text = $"Готов к запуску · предел N = {limit:N0}";
        DrawEmptyChartFrame();
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAlgorithm is not { } algorithm)
            return;

        if (!int.TryParse(MaxNBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxN) ||
            !int.TryParse(StepBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var step) ||
            !int.TryParse(RunsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var runs))
        {
            StatusText.Text = "Введите целые числа для N, шага и количества запусков.";
            return;
        }

        var maxAllowed = GetMaximumAllowedN(algorithm);
        if (maxN < 2 || maxN > maxAllowed || step < 1 || step > maxN || runs < 1 || runs > 20)
        {
            StatusText.Text = $"Допустимые значения: N от 2 до {maxAllowed:N0}, шаг от 1 до N, 1–20 запусков.";
            return;
        }

        var dataType = GetSelectedDataType();
        var sizes = MakeSizes(maxN, step);
        if (sizes.Count > 120)
        {
            StatusText.Text = "Слишком много точек. Увеличьте шаг так, чтобы на графике было не больше 120 размеров.";
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
        ChartSubtitle.Text = $"{algorithm.Name} · 0 из {sizes.Count} размеров";

        var progress = new Progress<(int Completed, int Total, int CurrentN)>(p =>
        {
            RunProgress.Value = (double)p.Completed / p.Total * 100;
            StatusText.Text = $"Измерение N = {p.CurrentN:N0} · {p.Completed} из {p.Total}";
            ChartSubtitle.Text = $"{algorithm.Name} · {p.Completed} из {p.Total} размеров";
        });

        try
        {
            var result = await Task.Run(
                () => RunMeasurements(algorithm, sizes, runs, dataType, _cancellation.Token, progress),
                _cancellation.Token);

            _points = result;
            _usesSteps = algorithm.SupportsStepCounting;
            _lastRunCount = runs;
            _approximation = FitExpectedCurve(_points, algorithm.TheoreticalComplexity);
            EmptyState.Visibility = Visibility.Collapsed;
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

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _cancellation?.Cancel();

    private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_points.Count > 0)
            RedrawChart();
        else
            DrawEmptyChartFrame();
    }

    private static List<PlotPoint> RunMeasurements(
        IAlgorithm algorithm,
        IReadOnlyList<int> sizes,
        int runs,
        DataType dataType,
        CancellationToken cancellationToken,
        IProgress<(int Completed, int Total, int CurrentN)> progress)
    {
        var output = new List<PlotPoint>(sizes.Count);
        var random = new Random(7411);

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
            }

            output.Add(new PlotPoint(n, total / runs));
            progress.Report((index + 1, sizes.Count, n));
        }

        return output;
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
            AddText(_usesSteps ? value.ToString("0", CultureInfo.CurrentCulture) : value.ToString("0.####", CultureInfo.CurrentCulture), 0, y - 8, 68, TextBrush, TextAlignment.Right);
        }

        for (var i = 0; i <= 5; i++)
        {
            var x = left + plotWidth * i / 5;
            var value = minX + (maxX - minX) * i / 5d;
            AddLine(x, top, x, top + plotHeight, GridBrush, 1);
            AddText(value.ToString("0", CultureInfo.CurrentCulture), x - 28, top + plotHeight + 8, 56, TextBrush, TextAlignment.Center);
        }

        AddLine(left, top, left, top + plotHeight, AxisBrush, 1.2);
        AddLine(left, top + plotHeight, width - right, top + plotHeight, AxisBrush, 1.2);
        AddText("Размер входных данных · N", width - right - 165, height - 22, 165, TextBrush, TextAlignment.Right);

        Point Map(PlotPoint point) => new(
            left + (point.N - minX) / (double)(maxX - minX) * plotWidth,
            top + plotHeight - point.Value / maxY * plotHeight);

        if (_approximation.Count > 1)
            AddPolyline(_approximation.Select(Map), ApproximationBrush, 2.2);
        if (_points.Count > 1)
            AddPolyline(_points.Select(Map), ExperimentBrush, 2.4);
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
                ToolTip = $"N = {point.N:N0}{Environment.NewLine}{(_usesSteps ? "Операции" : "Время")}: {point.Value:0.####}" + (_usesSteps ? string.Empty : " мс")
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

    private void AddPolyline(IEnumerable<Point> points, Brush brush, double thickness)
    {
        var polyline = new Polyline
        {
            Points = new PointCollection(points),
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            SnapsToDevicePixels = true
        };
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

    private readonly record struct PlotPoint(int N, double Value);
}
