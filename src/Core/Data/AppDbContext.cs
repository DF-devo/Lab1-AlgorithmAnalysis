using System.Diagnostics;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Core.Data;

/// <summary>
/// Контекст базы данных для приложения эмпирического анализа алгоритмов.
/// Управляет таблицами Algorithms, Experiments и ExperimentResults.
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<Algorithm> Algorithms { get; set; } = null!;
    public DbSet<Experiment> Experiments { get; set; } = null!;
    public DbSet<ExperimentResult> ExperimentResults { get; set; } = null!;

    // Параметр-less конструктор для инструментов миграции EF Core (dotnet ef)
    public AppDbContext() { }

    // Конструктор для внедрения зависимостей (DI)
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Настраиваем подключение, только если оно еще не было настроено через DI
        if (!optionsBuilder.IsConfigured)
        {
            // Файл БД будет создан в папке запуска приложения (например, bin/Debug/net8.0)
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "algorithm_analysis.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

#if DEBUG
            // Включаем логирование SQL-запросов в окно вывода (Output) Visual Studio
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.LogTo(message => Debug.WriteLine(message));
#endif
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Настройка связи один-ко-многим: Experiment -> ExperimentResults
        // При удалении эксперимента все его результаты удаляются автоматически (Cascade)
        modelBuilder.Entity<ExperimentResult>()
            .HasOne(er => er.Experiment)
            .WithMany() // Если в Experiment нет навигационного свойства ICollection<ExperimentResult>, оставляем пустым
            .HasForeignKey(er => er.ExperimentId)
            .OnDelete(DeleteBehavior.Cascade);

        // 2. Составной индекс для молниеносного поиска по кэшу.
        // Критически важно для производительности при проверке HasCachedResultsAsync и GetCachedResultsAsync.
        // Использует денормализованные поля AlgorithmId и DataType из модели ExperimentResult.
        modelBuilder.Entity<ExperimentResult>()
            .HasIndex(er => new { er.AlgorithmId, er.N, er.DataType, er.M })
            .HasDatabaseName("IX_ExperimentResult_CacheLookup");

        // 3. Индекс для быстрого получения списка экспериментов конкретного алгоритма
        modelBuilder.Entity<Experiment>()
            .HasIndex(e => e.AlgorithmId)
            .HasDatabaseName("IX_Experiment_AlgorithmId");
    }
}