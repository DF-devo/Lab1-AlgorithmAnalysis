namespace Algorithms.Helpers;

using Core;

public class AlgorithmInput : IAlgorithmInput
{
    public int N { get; set; }
    public int? M { get; set; }
    public DataType? DataType { get; set; }
    public object Data { get; set; } = null!;
}