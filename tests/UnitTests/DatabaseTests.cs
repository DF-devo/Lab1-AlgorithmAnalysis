namespace Tests.IntegrationTests;

using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

public class DatabaseTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly DatabaseService _dbService;

    public DatabaseTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        var factory = new TestDbContextFactory(options);
        _dbService = new DatabaseService(factory);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task SaveExperiment_CanBeRetrievedFromCache()
    {
        var algorithm = new Algorithm { Id = 1, Name = "TestAlgo" };

        var exp = new Experiment
        {
            AlgorithmId = algorithm.Id,
            Algorithm = algorithm,
            DataType = DataType.Random, // <-- ДОБАВЛЕНО: явно указываем тип данных
            RunsCount = 2,
            Date = DateTime.UtcNow
        };

        var results = new List<ExperimentResult>
        {
            new() { AlgorithmId = algorithm.Id, N = 100, M = null, DataType = DataType.Random, TimeMs = 1.5, Steps = 0, RunNumber = 1 },
            new() { AlgorithmId = algorithm.Id, N = 100, M = null, DataType = DataType.Random, TimeMs = 1.6, Steps = 0, RunNumber = 2 }
        };

        await _dbService.SaveExperimentAsync(exp, results);

        var cached = await _dbService.GetCachedResultsAsync(1, 100, DataType.Random, null);

        Assert.NotEmpty(cached);
        Assert.Equal(2, cached.Count);
        Assert.All(cached, r => Assert.Equal(100, r.N));
    }

    [Fact]
    public async Task HasCachedResults_ReturnsFalse_WhenEmpty()
    {
        bool hasCached = await _dbService.HasCachedResultsAsync(999, 100, DataType.Random, null, 5);
        Assert.False(hasCached);
    }

    [Fact]
    public async Task CachePreventsDuplicateExperiments()
    {
        var algorithm = new Algorithm { Id = 2, Name = "BubbleSort" };
        var exp = new Experiment
        {
            AlgorithmId = algorithm.Id,
            Algorithm = algorithm,
            DataType = DataType.Sorted, // <-- ДОБАВЛЕНО: явно указываем тип данных
            RunsCount = 3,
            Date = DateTime.UtcNow
        };

        var results = Enumerable.Range(1, 3).Select(i => new ExperimentResult
        {
            AlgorithmId = algorithm.Id,
            N = 100,
            M = null,
            DataType = DataType.Sorted,
            TimeMs = 5.0,
            Steps = 0,
            RunNumber = i
        }).ToList();

        bool hasCachedBefore = await _dbService.HasCachedResultsAsync(2, 100, DataType.Sorted, null, 3);
        Assert.False(hasCachedBefore);

        await _dbService.SaveExperimentAsync(exp, results);

        bool hasCachedAfter = await _dbService.HasCachedResultsAsync(2, 100, DataType.Sorted, null, 3);
        Assert.True(hasCachedAfter);
    }
}

public class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext() => new AppDbContext(_options);

    public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new AppDbContext(_options));
}