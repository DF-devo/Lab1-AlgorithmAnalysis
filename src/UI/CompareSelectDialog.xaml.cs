using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Core.Interfaces;
using Core.Models;

namespace UI;

public partial class CompareSelectDialog : Window
{
    private readonly List<Row> _rows;

    public List<IAlgorithm> Selected => _rows
        .Where(r => r.Selected)
        .Select(r => r.Algorithm)
        .ToList();

    public CompareSelectDialog(IReadOnlyList<IAlgorithm> algorithms, IEnumerable<string> preselected, string mainAlgorithmName)
    {
        InitializeComponent();
        var palette = MainWindow.ComparisonPalette();
        var index = 0;
        _rows = algorithms.Select(a => new Row(a, palette[index++ % palette.Length])
        {
            Selected = preselected.Contains(a.Name, StringComparer.OrdinalIgnoreCase)
        }).ToList();
        AlgorithmsList.ItemsSource = _rows;
        foreach (var row in _rows)
            row.PropertyChanged += (_, _) => UpdateCount();
        HintText.Text = $"Основной алгоритм «{mainAlgorithmName}» рисуется всегда; отмеченные добавятся к нему.";
        UpdateCount();
    }

    private void UpdateCount() =>
        CountText.Text = $"Отмечено: {_rows.Count(r => r.Selected)} (нужно минимум 2)";

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rows.Count(r => r.Selected) < 2)
        {
            CountText.Text = "Нужно отметить минимум 2 алгоритма.";
            return;
        }
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    public sealed class Row : INotifyPropertyChanged
    {
        public Row(IAlgorithm algorithm, Brush brush)
        {
            Algorithm = algorithm;
            Brush = brush;
            Details = $"O({MainWindow.ComplexityShortNamePublic(algorithm.TheoreticalComplexity)}) · " +
                      (algorithm.SupportsStepCounting ? "измерение: шаги" : "измерение: время") +
                      (algorithm.SupportsMatrixDimensions ? " · матрицы (3D)" : string.Empty);
        }

        public IAlgorithm Algorithm { get; }
        public Brush Brush { get; }
        public string Details { get; }

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set { _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
