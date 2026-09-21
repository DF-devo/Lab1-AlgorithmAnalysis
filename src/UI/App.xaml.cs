using Core.Data;
using Core.Interfaces;
using Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// 1. Регистрируем DbContextFactory (лучшая практика для WPF, чтобы избежать проблем с потоками)
services.AddDbContextFactory<AppDbContext>(options =>
{
    var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "algorithm_analysis.db");
    options.UseSqlite($"Data Source={dbPath}");
});

// 2. Регистрируем сервисы
services.AddScoped<IDatabaseService, DatabaseService>();
services.AddScoped<ICacheService, CacheService>();
services.AddScoped<IMathService, MathService>();
services.AddScoped<SeedService>();

var provider = services.BuildServiceProvider();

// 3. Инициализация БД при старте
var seedService = provider.GetRequiredService<SeedService>();
await seedService.InitializeAsync();