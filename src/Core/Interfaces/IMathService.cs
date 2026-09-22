using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Интерфейс математического сервиса для анализа результатов (MSE, аппроксимация).
/// </summary>
public interface IMathService
{
    /// <summary>
    /// Вычисляет среднюю квадратичную ошибку (MSE) между эмпирическими и теоретическими значениями.
    /// </summary>
    ComplexityType DetectBestFitComplexity(List<int> nValues, List<double> timeValues);
    double CalculateMSE(List<double> empirical, List<double> theoretical);

    /// <summary>
    /// Выполняет аппроксимацию эмпирических данных согласно заданному типу сложности.
    /// </summary>
    /// <param name="nValues">Список размеров входных данных.</param>
    /// <param name="timeValues">Список замеренных значений (время или шаги).</param>
    /// <param name="complexity">Тип теоретической сложности для подбора функции.</param>
    /// <returns>Кортеж, содержащий найденную константу множителя и список аппроксимированных значений.</returns>
    (double constant, List<double> approximated) Approximate(
        List<int> nValues, 
        List<double> timeValues, 
        ComplexityType complexity);

    /// <summary>
    /// Генерирует кривую аппроксимации для заданных значений N, константы и типа сложности.
    /// </summary>
    List<double> GenerateApproximationCurve(List<int> nValues, double constant, ComplexityType complexity);
}