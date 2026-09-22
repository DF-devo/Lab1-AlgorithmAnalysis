using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Core.Interfaces;
using Core.Models;
using MathNet.Numerics;

namespace Core.Services;

/// <summary>
/// Сервис для математического анализа результатов экспериментов (MSE, аппроксимация).
/// </summary>
public class MathService : IMathService
{
    public double CalculateMSE(List<double> empirical, List<double> theoretical)
    {
        if (empirical == null || theoretical == null)
            throw new ArgumentNullException("Списки не могут быть null");
        if (empirical.Count != theoretical.Count || empirical.Count == 0)
            throw new ArgumentException("Списки должны быть одинаковой ненулевой длины");

        // MSE = (1/k) * Σ(empirical - theoretical)^2
        double sumSquaredErrors = empirical.Zip(theoretical, (e, t) => Math.Pow(e - t, 2)).Sum();
        return sumSquaredErrors / empirical.Count;
    }

    public (double constant, List<double> approximated) Approximate(
        List<int> nValues, List<double> timeValues, ComplexityType complexity)
    {
        if (nValues == null || timeValues == null || nValues.Count != timeValues.Count || nValues.Count == 0)
            throw new ArgumentException("Некорректные входные данные для аппроксимации");

        // Формируем массив X: значения базовой функции f(n) для каждого n
        double[] x = nValues.Select(n => GetBasisFunctionValue(n, complexity)).ToArray();
        double[] y = timeValues.ToArray();

        // Fit.LineThroughOrigin находит уравнение y = C * x (без свободного члена).
        // Возвращает непосредственно значение коэффициента C (slope).
        double c = Fit.LineThroughOrigin(x, y);

        // Генерируем аппроксимированные значения: C * f(n)
        List<double> approximated = x.Select(val => val * c).ToList();

        Debug.WriteLine($"[MATH] Аппроксимация {complexity}: найдена константа C = {c:F6}");
        return (c, approximated);
    }

    public List<double> GenerateApproximationCurve(List<int> nValues, double constant, ComplexityType complexity)
    {
        if (nValues == null || nValues.Count == 0)
            throw new ArgumentException("Список nValues не может быть пустым или null");

        return nValues.Select(n => constant * GetBasisFunctionValue(n, complexity)).ToList();
    }

    public ComplexityType DetectBestFitComplexity(List<int> nValues, List<double> timeValues)
    {
        if (nValues == null || timeValues == null || nValues.Count != timeValues.Count || nValues.Count == 0)
            throw new ArgumentException("Некорректные входные данные для определения сложности");

        ComplexityType bestFit = ComplexityType.Constant;
        double minMse = double.MaxValue;

        // Перебираем все возможные типы сложности
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
                // Игнорируем типы, которые не могут быть рассчитаны (например, log(0), хотя валидация n>0 это предотвращает)
                Debug.WriteLine($"[MATH] Не удалось аппроксимировать для типа {type}");
            }
        }

        Debug.WriteLine($"[MATH] Лучшее совпадение сложности: {bestFit} (MSE = {minMse:F6})");
        return bestFit;
    }

    /// <summary>
    /// Возвращает значение базовой функции f(n) для заданного типа сложности.
    /// </summary>
    private double GetBasisFunctionValue(int n, ComplexityType complexity)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "Размер данных N должен быть больше 0");

        double dn = n;
        return complexity switch
        {
            ComplexityType.Constant => 1.0,
            ComplexityType.Logarithmic => Math.Log(dn),
            ComplexityType.Linear => dn,
            ComplexityType.Linearithmic => dn * Math.Log(dn),
            ComplexityType.Quadratic => dn * dn,
            ComplexityType.Cubic => dn * dn * dn,
            _ => throw new ArgumentOutOfRangeException(nameof(complexity), "Неизвестный тип сложности")
        };
    }
}