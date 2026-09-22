namespace Core.Models;

/// <summary>
/// Тип генерируемых входных данных (актуально для алгоритмов сортировки и поиска).
/// </summary>
public enum DataType
{
    Random,
    Sorted,
    Reversed
}

/// <summary>
/// Теоретическая асимптотическая сложность алгоритма.
/// </summary>
public enum ComplexityType
{
    Constant,
    Logarithmic,
    Linear,
    Linearithmic,
    Quadratic,
    Cubic
}

/// <summary>
/// Тип проводимого эксперимента: замер времени выполнения или подсчет элементарных шагов.
/// </summary>
public enum ExperimentType
{
    TimeMeasurement,
    StepCounting
}