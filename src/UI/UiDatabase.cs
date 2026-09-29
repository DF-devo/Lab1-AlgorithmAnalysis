using System.IO;
using Core.Data;
using Core.Interfaces;
using Core.Models;
using Core.Services;
using Microsoft.EntityFrameworkCore;

namespace UI;

/// <summary>
/// Мост между WPF-интерфейсом и SQLite-базой (EF Core).
/// Файл базы создаётся рядом с exe; при первом обращении выполняется сидинг алгоритмов.
/// </summary>
public static class UiDatabase
{
    private static Task? _initialization;

    public static string DbPath { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "algorithm_analysis.db");

    public static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={DbPath}").Options);

    public static IDatabaseService CreateDatabaseService() => new DatabaseService(new ContextFactory());

    /// <summary>Создаёт базу и наполняет справочник алгоритмов (однократно; при сбое готова к повтору).</summary>
    public static Task EnsureInitializedAsync()
    {
        _initialization ??= new SeedService(new ContextFactory()).InitializeAsync();
        return _initialization;
    }

    /// <summary>Находит алгоритм в справочнике по имени или регистрирует его по метаданным IAlgorithm.</summary>
    public static async Task<Algorithm> ResolveAlgorithmAsync(IAlgorithm algorithm)
    {
        using var context = CreateContext();
        var entity = await context.Algorithms.FirstOrDefaultAsync(a => a.Name == algorithm.Name);
        if (entity is not null)
            return entity;

        entity = new Algorithm
        {
            Name = algorithm.Name,
            Description = algorithm.Description,
            TheoreticalComplexity = algorithm.TheoreticalComplexity,
            ExperimentType = algorithm.ExperimentType,
            SupportsDataType = algorithm.SupportsDataType,
            SupportsMatrixDimensions = algorithm.SupportsMatrixDimensions,
            SupportsStepCounting = algorithm.SupportsStepCounting
        };
        context.Algorithms.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    private sealed class ContextFactory : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => CreateContext();
        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CreateContext());
    }
}
