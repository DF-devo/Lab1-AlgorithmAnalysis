using Core.Interfaces;

namespace Core.Services;

/// <summary>
/// Реестр всех доступных для анализа алгоритмов.
/// </summary>
public class AlgorithmRegistry
{
    private readonly Dictionary<string, IAlgorithm> _algorithms = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IAlgorithm algorithm)
    {
        if (algorithm == null) throw new ArgumentNullException(nameof(algorithm));
        if (string.IsNullOrWhiteSpace(algorithm.Name)) throw new ArgumentException("Algorithm name cannot be empty");
        
        _algorithms[algorithm.Name] = algorithm;
    }

    public IAlgorithm? GetAlgorithm(string name)
    {
        return _algorithms.TryGetValue(name, out var algo) ? algo : null;
    }

    public List<IAlgorithm> GetAllAlgorithms()
    {
        return _algorithms.Values.ToList();
    }
}