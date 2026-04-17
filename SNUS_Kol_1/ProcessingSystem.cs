using System.Diagnostics;

namespace SNUS_Kol_1
{
    public class ProcessingSystem
    {
        private readonly int _maxQueueSize;
        private readonly SortedDictionary<int, Queue<Job>> _queue = new();
        private readonly HashSet<Guid> _submitted = new();
        private readonly HashSet<Guid> _processed = new();
        private readonly Dictionary<Guid, Job> _allJobs = new();

        private readonly SemaphoreSlim _signal = new(0);
        private readonly object _lock = new();

        private readonly Dictionary<Guid, TaskCompletionSource<int>> _taskMap = new();
        private readonly List<JobExecutionInfo> _history = new();

        public event Func<Job, int, Task> JobCompleted;
        public event Func<Job, Exception, Task> JobFailed;

        public ProcessingSystem(int workers, int maxQueueSize)
        {
            _maxQueueSize = maxQueueSize;

            for (int i = 0; i < workers; i++)
            {
                Task.Run(WorkerLoop);
                Console.WriteLine($"Worker {i} started");
            }

            Task.Run(ReportLoop);
        }

        public JobHandle Submit(Job job)
        {
            var tcs = new TaskCompletionSource<int>();

            lock (_lock)
            {
                if (_submitted.Contains(job.Id))
                    throw new Exception("Duplicate job");

                _submitted.Add(job.Id);

                int size = _queue.Values.Sum(q => q.Count);

                if (size >= _maxQueueSize)
                {
                    _submitted.Remove(job.Id); // rollback

                    if (JobFailed != null)
                    {
                        _ = JobFailed(job, new Exception("Queue full")); // fire async, but immediately scheduled
                    }

                    tcs.SetException(new Exception("Queue full"));

                    return new JobHandle
                    {
                        Id = job.Id,
                        Result = tcs.Task
                    };
                }

                if (!_queue.ContainsKey(job.Priority))
                    _queue[job.Priority] = new Queue<Job>();

                _queue[job.Priority].Enqueue(job);
                _allJobs[job.Id] = job;
                _taskMap[job.Id] = tcs;
            }

            _signal.Release();

            return new JobHandle
            {
                Id = job.Id,
                Result = tcs.Task
            };
        }

        private async Task WorkerLoop()
        {
            while (true)
            {
                await _signal.WaitAsync();

                Job job = null;

                lock (_lock)
                {
                    foreach (var kv in _queue)
                    {
                        if (kv.Value.Count > 0)
                        {
                            job = kv.Value.Dequeue();
                            break;
                        }
                    }
                }

                if (job != null)
                    await ExecuteWithRetry(job);
            }
        }

        private async Task ExecuteWithRetry(Job job)
        {
            lock (_lock)
            {
                if (_processed.Contains(job.Id))
                    return; // already done
            }

            int attempts = 0;

            while (attempts < 3)
            {
                var sw = Stopwatch.StartNew();

                try
                {
                    Console.WriteLine($"Processing job {job.Id}, attempt {attempts + 1}");

                    var task = ExecuteJob(job);

                    if (await Task.WhenAny(task, Task.Delay(2000)) != task)
                        throw new TimeoutException();

                    int result = await task;

                    sw.Stop();

                    lock (_lock)
                    {
                        if (_processed.Contains(job.Id))
                            return;

                        _processed.Add(job.Id);

                        _history.Add(new JobExecutionInfo
                        {
                            Job = job,
                            Success = true,
                            Result = result,
                            Duration = sw.Elapsed
                        });
                    }

                    Console.WriteLine($"SUCCESS {job.Id}");

                    _taskMap[job.Id].SetResult(result);

                    if (JobCompleted != null)
                        await JobCompleted(job, result);

                    return; // STOP after success
                }
                catch (Exception ex)
                {
                    attempts++;

                    if (attempts < 3)
                    {
                        Console.WriteLine($"Retrying job {job.Id}...");
                        continue;
                    }

                    // FINAL FAILURE -> ABORT
                    sw.Stop();

                    lock (_lock)
                    {
                        _history.Add(new JobExecutionInfo
                        {
                            Job = job,
                            Success = false,
                            Duration = sw.Elapsed
                        });
                    }

                    Console.WriteLine($"ABORT {job.Id}");

                    _taskMap[job.Id].SetException(ex);

                    if (JobFailed != null)
                        await JobFailed(job, ex);
                }
            }
        }

        private Task<int> ExecuteJob(Job job)
        {
            return Task.Run(() =>
            {
                return job.Type switch
                {
                    JobType.Prime => ProcessPrime(job.Payload),
                    JobType.IO => ProcessIO(job.Payload),
                    _ => 0
                };
            });
        }

        private int ProcessPrime(string payload)
        {
            // numbers:10_000,threads:3
            var parts = payload.Split(',');
            int max = int.Parse(parts[0].Split(':')[1].Replace("_", ""));
            int threads = Math.Clamp(int.Parse(parts[1].Split(':')[1]), 1, 8);

            int count = 0;

            Parallel.For(2, max + 1,
                new ParallelOptions { MaxDegreeOfParallelism = threads },
                i =>
                {
                    if (IsPrime(i))
                        Interlocked.Increment(ref count);
                });

            return count;
        }

        private int ProcessIO(string payload)
        {
            int delay = int.Parse(payload.Split(':')[1].Replace("_", ""));
            Thread.Sleep(delay);
            return new Random().Next(0, 101);
        }

        private bool IsPrime(int n)
        {
            if (n < 2) return false;
            for (int i = 2; i <= Math.Sqrt(n); i++)
                if (n % i == 0) return false;
            return true;
        }

        public IEnumerable<Job> GetTopJobs(int n)
        {
            lock (_lock)
            {
                return _queue
                    .OrderBy(k => k.Key)
                    .SelectMany(k => k.Value)
                    .Take(n)
                    .Select(j => new Job   // defensive copy
                    {
                        Id = j.Id,
                        Type = j.Type,
                        Payload = j.Payload,
                        Priority = j.Priority
                    })
                    .ToList();
            }
        }

        public Job GetJob(Guid id)
        {
            lock (_lock)
                return _allJobs.GetValueOrDefault(id);
        }

        private async Task ReportLoop()
        {
            int index = 0;

            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(1));

                List<JobExecutionInfo> snapshot;

                lock (_lock)
                    snapshot = _history.ToList();

                ReportManager.Generate(snapshot, index);

                index = (index + 1) % 10;
            }
        }
    }
}
