using System;
using System.Diagnostics;
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

// 2. Инициализация БД (создание таблиц + сидинг 15 алгоритмов)
Console.WriteLine("Инициализация БД...");
Console.WriteLine($"Файл базы: {dbPath}\n");
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
registry.Register(new ConstantFunctionAlgorithm());

var algorithm = registry.GetAlgorithm("Constant Function");
if (algorithm == null)
{
    Console.WriteLine("Алгоритм не найден!");
    return;
}

Console.WriteLine($"Запуск эксперимента для: {algorithm.Name}");
Console.WriteLine("N | Время (мс) | Шаги");
Console.WriteLine("-----------------------");

// 5. Запуск серии экспериментов (первый прогон — вычисление + сохранение в SQLite)
var progress = new Progress<double>(p => Console.Write($"\rПрогресс: {p:F1}%"));
var stopwatch = Stopwatch.StartNew();
var results = await runner.RunExperimentSeriesAsync(
    algorithm: algorithm,
    nMax: 1000,
    step: 200,
    runsCount: 3,
    dataType: null,
    forceRecalculate: false,
    progress: progress
);
stopwatch.Stop();
Console.WriteLine("\rПрогресс: 100.0%");
Console.WriteLine($"[1-й прогон] Вычислено и сохранено в БД за {stopwatch.ElapsedMilliseconds} мс");

// 6. Повторный запуск с теми же параметрами — все точки должны взяться из кэша SQLite
stopwatch.Restart();
var cachedResults = await runner.RunExperimentSeriesAsync(
    algorithm: algorithm,
    nMax: 1000,
    step: 200,
    runsCount: 3,
    dataType: null,
    forceRecalculate: false,
    progress: progress
);
stopwatch.Stop();
Console.WriteLine($"[2-й прогон] Прочитано из кэша БД за {stopwatch.ElapsedMilliseconds} мс ({cachedResults.Count} записей)\n");

// 7. Агрегация и анализ результатов
var grouped = results.GroupBy(r => r.N).OrderBy(g => g.Key).ToList();

var nValues = grouped.Select(g => g.Key).ToList();
var avgTimeValues = grouped.Select(g => g.Average(r => r.TimeMs)).ToList();

foreach (var g in grouped)
{
    Console.WriteLine($"{g.Key,3} | {g.Average(r => r.TimeMs),10:F4} | {g.First().Steps,4}");
}

// 8. Математический анализ
var bestFit = mathService.DetectBestFitComplexity(nValues, avgTimeValues);
Console.WriteLine($"\n[ANALYSIS] Лучшее совпадение сложности: {bestFit}");

var (constant, approximated) = mathService.Approximate(nValues, avgTimeValues, bestFit);
var mse = mathService.CalculateMSE(avgTimeValues, approximated);

Console.WriteLine($"[ANALYSIS] Константа C = {constant:F6}");
Console.WriteLine($"[ANALYSIS] MSE = {mse:F6}");

// 9. Итоговое содержимое базы данных
using (var scope = provider.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var ctx = await factory.CreateDbContextAsync();

    int algorithmsCount = await ctx.Algorithms.CountAsync();
    int experimentsCount = await ctx.Experiments.CountAsync();
    int resultsCount = await ctx.ExperimentResults.CountAsync();

    Console.WriteLine("\n[DB] Содержимое algorithm_analysis.db:");
    Console.WriteLine($"[DB]   Алгоритмы:  {algorithmsCount}");
    Console.WriteLine($"[DB]   Эксперименты: {experimentsCount}");
    Console.WriteLine($"[DB]   Результаты: {resultsCount}");

    var lastExperiment = await ctx.Experiments
        .OrderByDescending(e => e.Id)
        .FirstOrDefaultAsync();
    if (lastExperiment != null)
    {
        var expResults = await dbService.GetResultsForExperimentAsync(lastExperiment.Id);
        Console.WriteLine($"[DB]   Последний эксперимент ID={lastExperiment.Id} " +
                          $"(AlgorithmId={lastExperiment.AlgorithmId}, N_max={lastExperiment.N_max}): {expResults.Count} результатов");
    }
}

Console.WriteLine("\nТест успешно завершен!");
if (!Console.IsInputRedirected)
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
