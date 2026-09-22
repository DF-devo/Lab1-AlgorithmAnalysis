namespace Tests.UnitTests;

using Core.Models;
using Core.Services;
using System;
using System.Collections.Generic;
using Xunit;

public class MathServiceTests
{
    private readonly MathService _mathService;

    public MathServiceTests()
    {
        _mathService = new MathService();
    }

    #region CalculateMSE Tests

    [Fact]
    public void CalculateMSE_IdenticalArrays_ReturnsZero()
    {
        // Arrange
        var empirical = new List<double> { 1.0, 2.0, 3.0, 4.0, 5.0 };
        var theoretical = new List<double> { 1.0, 2.0, 3.0, 4.0, 5.0 };

        // Act
        double mse = _mathService.CalculateMSE(empirical, theoretical);

        // Assert
        Assert.Equal(0.0, mse, precision: 10);
    }

    [Fact]
    public void CalculateMSE_KnownDifference_ReturnsCorrectMSE()
    {
        // Arrange: разница всегда 1.0, квадрат разницы = 1.0. Среднее = 1.0
        var empirical = new List<double> { 1.0, 2.0, 3.0 };
        var theoretical = new List<double> { 2.0, 3.0, 4.0 };

        // Act
        double mse = _mathService.CalculateMSE(empirical, theoretical);

        // Assert
        Assert.Equal(1.0, mse, precision: 10);
    }

    [Fact]
    public void CalculateMSE_NullLists_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _mathService.CalculateMSE(null!, new List<double> { 1.0 }));
        Assert.Throws<ArgumentNullException>(() => _mathService.CalculateMSE(new List<double> { 1.0 }, null!));
    }

    [Fact]
    public void CalculateMSE_DifferentLengths_ThrowsArgumentException()
    {
        // Arrange
        var empirical = new List<double> { 1.0, 2.0 };
        var theoretical = new List<double> { 1.0 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _mathService.CalculateMSE(empirical, theoretical));
    }

    [Fact]
    public void CalculateMSE_EmptyLists_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _mathService.CalculateMSE(new List<double>(), new List<double>()));
    }

    #endregion

    #region Approximate Tests

    [Fact]
    public void Approximate_LinearData_ReturnsConstantOne()
    {
        // Arrange: Идеально линейные данные (y = 1 * x)
        var nValues = new List<int> { 10, 20, 30, 40, 50 };
        var timeValues = new List<double> { 10.0, 20.0, 30.0, 40.0, 50.0 };

        // Act
        var (constant, approximated) = _mathService.Approximate(nValues, timeValues, ComplexityType.Linear);

        // Assert
        Assert.Equal(1.0, constant, precision: 5);
        Assert.Equal(nValues.Count, approximated.Count);
        Assert.Equal(10.0, approximated[0], precision: 5);
        Assert.Equal(50.0, approximated[4], precision: 5);
    }

    [Fact]
    public void Approximate_QuadraticData_ReturnsCorrectConstant()
    {
        // Arrange: Идеально квадратичные данные (y = 2 * x^2)
        var nValues = new List<int> { 10, 20, 30 };
        var timeValues = new List<double> { 200.0, 800.0, 1800.0 }; // 2 * 10^2, 2 * 20^2, 2 * 30^2

        // Act
        var (constant, approximated) = _mathService.Approximate(nValues, timeValues, ComplexityType.Quadratic);

        // Assert
        Assert.Equal(2.0, constant, precision: 5);
        Assert.Equal(200.0, approximated[0], precision: 5);
        Assert.Equal(1800.0, approximated[2], precision: 5);
    }

    [Fact]
    public void Approximate_InvalidNValues_ThrowsArgumentException()
    {
        // Arrange: N <= 0 вызовет ошибку в GetBasisFunctionValue
        var nValues = new List<int> { 0, 10, 20 };
        var timeValues = new List<double> { 1.0, 2.0, 3.0 };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _mathService.Approximate(nValues, timeValues, ComplexityType.Linear));
    }

    #endregion

    #region GenerateApproximationCurve Tests

    [Fact]
    public void GenerateApproximationCurve_Linear_ReturnsCorrectValues()
    {
        // Arrange
        var nValues = new List<int> { 10, 20, 30 };
        double constant = 2.5;

        // Act
        var curve = _mathService.GenerateApproximationCurve(nValues, constant, ComplexityType.Linear);

        // Assert: y = 2.5 * n
        Assert.Equal(3, curve.Count);
        Assert.Equal(25.0, curve[0], precision: 5);
        Assert.Equal(50.0, curve[1], precision: 5);
        Assert.Equal(75.0, curve[2], precision: 5);
    }

    [Fact]
    public void GenerateApproximationCurve_EmptyNValues_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _mathService.GenerateApproximationCurve(new List<int>(), 1.0, ComplexityType.Linear));
    }

    #endregion

    #region DetectBestFitComplexity Tests

    [Fact]
    public void DetectBestFitComplexity_PerfectLinearData_DetectsLinear()
    {
        // Arrange
        var nValues = new List<int> { 10, 20, 30, 40, 50 };
        var timeValues = new List<double> { 10.0, 20.0, 30.0, 40.0, 50.0 };

        // Act
        var bestFit = _mathService.DetectBestFitComplexity(nValues, timeValues);

        // Assert
        Assert.Equal(ComplexityType.Linear, bestFit);
    }

    [Fact]
    public void DetectBestFitComplexity_PerfectQuadraticData_DetectsQuadratic()
    {
        // Arrange
        var nValues = new List<int> { 10, 20, 30, 40, 50 };
        var timeValues = new List<double> { 100.0, 400.0, 900.0, 1600.0, 2500.0 };

        // Act
        var bestFit = _mathService.DetectBestFitComplexity(nValues, timeValues);

        // Assert
        Assert.Equal(ComplexityType.Quadratic, bestFit);
    }

    [Fact]
    public void DetectBestFitComplexity_PerfectLogarithmicData_DetectsLogarithmic()
    {
        // Arrange
        var nValues = new List<int> { 10, 100, 1000, 10000 };
        // y = 1 * ln(n)
        var timeValues = new List<double>
        {
            Math.Log(10),
            Math.Log(100),
            Math.Log(1000),
            Math.Log(10000)
        };

        // Act
        var bestFit = _mathService.DetectBestFitComplexity(nValues, timeValues);

        // Assert
        Assert.Equal(ComplexityType.Logarithmic, bestFit);
    }

    [Fact]
    public void DetectBestFitComplexity_InvalidData_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _mathService.DetectBestFitComplexity(null!, new List<double> { 1.0 }));
        Assert.Throws<ArgumentException>(() => _mathService.DetectBestFitComplexity(new List<int>(), new List<double>()));
    }

    #endregion
}