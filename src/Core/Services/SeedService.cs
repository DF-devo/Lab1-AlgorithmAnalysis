using System.Diagnostics;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Core.Services;

public class SeedService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public SeedService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task InitializeAsync()
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            // Создаем БД и таблицы, если их нет
            await context.Database.EnsureCreatedAsync();
            Debug.WriteLine("[SEED] Database ensured.");

            // Проверяем, есть ли уже данные
            if (await context.Algorithms.AnyAsync())
            {
                Debug.WriteLine("[SEED] Database already seeded. Skipping.");
                return;
            }

            var algorithms = new List<Algorithm>
            {
                // Базовые / Простые
                new() { Name = "Constant Function", Description = "Возвращает константу", TheoreticalComplexity = ComplexityType.Constant, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = false, SupportsMatrixDimensions = false, SupportsStepCounting = false },
                new() { Name = "Sum Elements", Description = "Сумма элементов массива", TheoreticalComplexity = ComplexityType.Linear, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = false, SupportsMatrixDimensions = false, SupportsStepCounting = true },
                new() { Name = "Product Elements", Description = "Произведение элементов массива", TheoreticalComplexity = ComplexityType.Linear, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = false, SupportsMatrixDimensions = false, SupportsStepCounting = true },
                
                // Полиномы
                new() { Name = "Polynomial Naive", Description = "Вычисление полинома наивным методом", TheoreticalComplexity = ComplexityType.Quadratic, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = false, SupportsMatrixDimensions = false, SupportsStepCounting = true },
                new() { Name = "Polynomial Horner", Description = "Вычисление полинома схемой Горнера", TheoreticalComplexity = ComplexityType.Linear, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = false, SupportsMatrixDimensions = false, SupportsStepCounting = true },
                
                // Сортировки (поддерживают DataType)
                new() { Name = "Bubble Sort", Description = "Сортировка пузырьком", TheoreticalComplexity = ComplexityType.Quadratic, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = true, SupportsMatrixDimensions = false, SupportsStepCounting = true },
                new() { Name = "Quick Sort", Description = "Быстрая сортировка", TheoreticalComplexity = ComplexityType.Linearithmic, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = true, SupportsMatrixDimensions = false, SupportsStepCounting = true },
                new() { Name = "Timsort", Description = "Гибридная сортировка (как в Array.Sort)", TheoreticalComplexity = ComplexityType.Linearithmic, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = true, SupportsMatrixDimensions = false, SupportsStepCounting = true },
                
                // Матрицы (поддерживают M)
                new() { Name = "Matrix Multiplication", Description = "Умножение матриц N x M", TheoreticalComplexity = ComplexityType.Cubic, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = false, SupportsMatrixDimensions = true, SupportsStepCounting = true },
                
                // Индивидуальные задания
                new() { Name = "KMP", Description = "Алгоритм Кнута-Морриса-Пратта (поиск подстроки)", TheoreticalComplexity = ComplexityType.Linear, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = true, SupportsMatrixDimensions = false, SupportsStepCounting = true },
                new() { Name = "FFT", Description = "Быстрое преобразование Фурье", TheoreticalComplexity = ComplexityType.Linearithmic, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = false, SupportsMatrixDimensions = false, SupportsStepCounting = true },
                new() { Name = "Levenshtein", Description = "Расстояние Левенштейна между строками", TheoreticalComplexity = ComplexityType.Quadratic, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = false, SupportsMatrixDimensions = false, SupportsStepCounting = true },
                
                // Возведение в степень
                new() { Name = "Power Iterative", Description = "Возведение в степень итеративно", TheoreticalComplexity = ComplexityType.Linear, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = false, SupportsMatrixDimensions = false, SupportsStepCounting = true },
                new() { Name = "Power Recursive", Description = "Возведение в степень рекурсивно", TheoreticalComplexity = ComplexityType.Linear, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = false, SupportsMatrixDimensions = false, SupportsStepCounting = true },
                new() { Name = "Power Binary", Description = "Бинарное возведение в степень", TheoreticalComplexity = ComplexityType.Logarithmic, ExperimentType = ExperimentType.TimeMeasurement, SupportsDataType = false, SupportsMatrixDimensions = false, SupportsStepCounting = true }
            };

            context.Algorithms.AddRange(algorithms);
            await context.SaveChangesAsync();
            Debug.WriteLine($"[SEED] Successfully seeded {algorithms.Count} algorithms.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SEED ERROR] Initialization failed: {ex.Message}");
            throw;
        }
    }
}