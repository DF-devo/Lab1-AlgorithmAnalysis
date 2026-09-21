// ... добавить реализацию нового метода:
public ComplexityType DetectBestFitComplexity(List<int> nValues, List<double> timeValues)
{
    if (nValues == null || timeValues == null || nValues.Count != timeValues.Count || nValues.Count == 0)
        throw new ArgumentException("Invalid input data for complexity detection");

    ComplexityType bestFit = ComplexityType.Constant;
    double minMse = double.MaxValue;

    foreach (ComplexityType type in Enum.GetValues(typeof(ComplexityType)))
    {
        try
        {
            var (_, approximated) = Approximate(nValues, timeValues, type);
            double mse = CalculateMSE(timeValues, approximated);
            
            if (mse < minMse)
            {
                minMse = mse;
                bestFit = type;
            }
        }
        catch
        {
            // Игнорируем типы, которые не могут быть аппроксимированы (например, log(0))
        }
    }

    return bestFit;
}