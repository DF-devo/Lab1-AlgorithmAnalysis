namespace Algorithms.Helpers;

using System.Collections.Concurrent;
using Core.Interfaces;

public class ExperimentQueue
{
    private readonly ConcurrentQueue<ExperimentTask> _queue = new();
    private readonly ConcurrentDictionary<Guid, ExperimentTask> _runningTasks = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxParallelism;

    public ExperimentQueue(int maxParallelism = 4)
    {
        _maxParallelism = maxParallelism;
        _semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
    }

    public int PendingCount => _queue.Count;
    public int RunningCount => _runningTasks.Count;
    public int MaxParallelism => _maxParallelism;

    public Guid Enqueue(IAlgorithm algorithm, IAlgorithmInput input, int runsCount = 10)
    {
        var taskId = Guid.NewGuid();
        var task = new ExperimentTask
        {
            Id = taskId,
            Algorithm = algorithm,
            Input = input,
            RunsCount = runsCount,
            Status = TaskStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _queue.Enqueue(task);
        return taskId;
    }

    public async Task<ExperimentTask> ExecuteNextAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        if (!_queue.TryDequeue(out var task))
        {
            _semaphore.Release();
            throw new InvalidOperationException("Очередь пуста");
        }

        task.Status = TaskStatus.Running;
        task.StartedAt = DateTime.UtcNow;
        _runningTasks[task.Id] = task;

        try
        {
            var results = new List<AlgorithmExecutionResult>();

            // Прогревочный запуск (не учитывается в результатах)
            _ = task.Algorithm.Execute(task.Input);

            // Основные прогоны
            for (int i = 0; i < task.RunsCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = task.Algorithm.Execute(task.Input);
                results.Add(result);
            }

            task.Results = results;
            task.Status = TaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            task.Status = TaskStatus.Cancelled;
            throw;
        }
        catch (Exception ex)
        {
            task.Status = TaskStatus.Failed;
            task.ErrorMessage = ex.Message;
        }
        finally
        {
            _runningTasks.TryRemove(task.Id, out _);
            _semaphore.Release();
        }

        return task;
    }

    public async Task<List<ExperimentTask>> ExecuteAllAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<ExperimentTask>>();

        while (_queue.Count > 0)
        {
            tasks.Add(ExecuteNextAsync(cancellationToken));
        }

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    public bool TryGetTask(Guid taskId, out ExperimentTask? task)
    {
        return _runningTasks.TryGetValue(taskId, out task);
    }
}

public class ExperimentTask
{
    public Guid Id { get; set; }
    public IAlgorithm Algorithm { get; set; } = null!;
    public IAlgorithmInput Input { get; set; } = null!;
    public int RunsCount { get; set; }
    public TaskStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<AlgorithmExecutionResult> Results { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}