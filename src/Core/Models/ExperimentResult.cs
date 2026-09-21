using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models;

public class ExperimentResult
{
    public int Id { get; set; }
    public int ExperimentId { get; set; }

    [ForeignKey(nameof(ExperimentId))]
    public Experiment Experiment { get; set; } = null!;

    // Явные свойства для быстрого составного индекса кэша
    public int AlgorithmId { get; set; }
    public DataType? DataType { get; set; }

    public int N { get; set; }
    public int? M { get; set; }
    public int RunNumber { get; set; }
    public double TimeMs { get; set; }
    public long Steps { get; set; }
}