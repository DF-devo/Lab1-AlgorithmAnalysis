using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Data;
using Core.Interfaces;
using Core.Models;
using Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// 1. Настройка DI
var services = new ServiceCollection();

var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "algorithm_analysis.db");
services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

services.AddScoped<IDatabaseService, DatabaseService>();
services.AddScoped<ICacheService, CacheService>();
services.AddScoped<IMathService, MathService>();
services.AddScoped<IDataGenerator, DataGenerator>();
services.AddScoped<ExperimentRunner>();
services.AddSingleton<AlgorithmRegistry>();
services.AddScoped<SeedService>();

var provider = services.BuildServiceProvider();

// 2. Инициализация БД
Console.WriteLine("Инициализация БД...");
using (var scope = provider.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<SeedService>();
    await seeder.InitializeAsync();
}

// 3. Получение сервисов
var registry = provider.GetRequiredService<AlgorithmRegistry>();
var runner = provider.GetRequiredService<ExperimentRunner>();
var mathService = provider.GetRequiredService<IMathService>();
var dbService = provider.GetRequiredService<IDatabaseService>();

// 4. Регистрация тестового алгоритма (Constant Function)
// В реальном приложении это делается через DI или вручную при старте
registry.Register(new ConstantFunctionAlgorithm());

var algorithm = registry.GetAlgorithm("Constant Function");
if (algorithm == null)
{
    Console.WriteLine("Алгоритм не найден!");
    return;
}

// Получаем реальный ID алгоритма из БД для корректного кэширования
using (var scope = provider.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
    var dbAlgo = await ctx.Algorithms.FirstOrDefaultAsync(a => a.Name == algorithm.Name);
    if (dbAlgo != null)
    {
        // Хак для теста: передаем ID через замыкание или модифицируем раннер. 
        // Для простоты теста просто выведем результаты.
    }
}

Console.WriteLine($"\nЗапуск эксперимента для: {algorithm.Name}");
Console.WriteLine("N | Время (мс) | Шаги");
Console.WriteLine("-----------------------");

// 5. Запуск серии экспериментов
var progress = new Progress<double>(p => Console.Write($"\rПрогресс: {p:F1}%"));
var results = await runner.RunExperimentSeriesAsync(
    algorithm: algorithm,
    nMax: 1000,
    step: 200,
    runsCount: 3,
    dataType: null,
    forceRecalculate: false,
    progress: progress
);

Console.WriteLine("\rПрогресс: 100.0%");

// 6. Агрегация и анализ результатов
var grouped = results.GroupBy(r => r.N).OrderBy(g => g.Key).ToList();

var nValues = grouped.Select(g => g.Key).ToList();
var avgTimeValues = grouped.Select(g => g.Average(r => r.TimeMs)).ToList();

foreach (var g in grouped)
{
    Console.WriteLine($"{g.Key,3} | {g.Average(r => r.TimeMs),10:F4} | {g.First().Steps,4}");
}

// 7. Математический анализ
var bestFit = mathService.DetectBestFitComplexity(nValues, avgTimeValues);
Console.WriteLine($"\n[ANALYSIS] Лучшее совпадение сложности: {bestFit}");

var (constant, approximated) = mathService.Approximate(nValues, avgTimeValues, bestFit);
var mse = mathService.CalculateMSE(avgTimeValues, approximated);

Console.WriteLine($"[ANALYSIS] Константа C = {constant:F6}");
Console.WriteLine($"[ANALYSIS] MSE = {mse:F6}");

Console.WriteLine("\nТест успешно завершен! Нажмите любую клавишу...");
Console.ReadKey();

// --- Вспомогательный класс для теста (в реальном проекте будет в папке Algorithms) ---
public class ConstantFunctionAlgorithm : IAlgorithm
{
    public string Name => "Constant Function";
    public string Description => "Возвращает константу";
    public ComplexityType TheoreticalComplexity => ComplexityType.Constant;
    public ExperimentType ExperimentType => ExperimentType.TimeMeasurement;
    public bool SupportsDataType => false;
    public bool SupportsMatrixDimensions => false;
    public bool SupportsStepCounting => false;

    public AlgorithmExecutionResult Execute(IAlgorithmInput input)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // Эмуляция работы
        int dummy = 42; 
        System.Threading.Thread.Sleep(1); // Небольшая задержка для измеряемого времени

        sw.Stop();
        return new AlgorithmExecutionResult { TimeMs = sw.Elapsed.TotalMilliseconds, Steps = 1, ResultData = dummy };
    }
}