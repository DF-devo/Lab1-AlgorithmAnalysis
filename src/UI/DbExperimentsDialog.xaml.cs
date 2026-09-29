using System.Windows;
using System.Windows.Input;
using Core.Models;

namespace UI;

public partial class DbExperimentsDialog : Window
{
    public Experiment? Selected => (ExperimentsList.SelectedItem as Row)?.Experiment;

    public DbExperimentsDialog(IReadOnlyList<Experiment> experiments)
    {
        InitializeComponent();
        ExperimentsList.ItemsSource = experiments.Select(e => new Row(e)).ToList();
        ExperimentsList.SelectedIndex = 0;
        CountText.Text = $"Всего серий: {experiments.Count}";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not null)
            DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ExperimentsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Selected is not null)
            DialogResult = true;
    }

    public sealed class Row
    {
        public Row(Experiment experiment)
        {
            Experiment = experiment;
            Header = $"Эксперимент #{experiment.Id} · {experiment.Date.ToLocalTime():dd.MM.yyyy HH:mm}";
            var dataType = experiment.DataType switch
            {
                Core.Models.DataType.Sorted => " · отсортированные",
                Core.Models.DataType.Reversed => " · в обратном порядке",
                Core.Models.DataType.Random => " · случайные",
                _ => string.Empty
            };
            Details = $"N ≤ {experiment.N_max:N0} · шаг {experiment.Step} · {experiment.RunsCount} запусков{dataType}";
        }

        public Experiment Experiment { get; }
        public string Header { get; }
        public string Details { get; }
    }
}
