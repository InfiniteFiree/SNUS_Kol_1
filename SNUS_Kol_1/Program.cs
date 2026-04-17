
namespace SNUS_Kol_1
{
    class Program
    {
        private static readonly SemaphoreSlim _logLock = new(1, 1);

        static void Main()
        {
            var config = XmlConfigLoader.Load("SystemConfig.xml");

            var system = new ProcessingSystem(
                config.WorkerCount,
                config.MaxQueueSize
            );

            system.JobCompleted += async (job, result) =>
            {
                await _logLock.WaitAsync();
                try
                {
                    string log = $"[{DateTime.Now}] SUCCESS {job.Id}, {result}";
                    await File.AppendAllTextAsync("log.txt", log + "\n");
                }
                finally
                {
                    _logLock.Release();
                }
            };

            system.JobFailed += async (job, ex) =>
            {
                await _logLock.WaitAsync();
                try
                {
                    string log = $"[{DateTime.Now}] FAILURE {job.Id}, {ex.GetType()}";
                    await File.AppendAllTextAsync("log.txt", log + "\n");
                }
                finally
                {
                    _logLock.Release();
                }
            };

            // Load initial jobs
            foreach (var job in config.Jobs)
                system.Submit(job);

            // Producers
            for (int i = 0; i < config.WorkerCount; i++)
            {
                Task.Run(async () =>
                {
                    var rand = new Random();

                    while (true)
                    {
                        await Task.Delay(rand.Next(500, 2000));

                        var isPrime = rand.Next(2) == 0;

                        var job = new Job
                        {
                            Type = isPrime ? JobType.Prime : JobType.IO,
                            Priority = rand.Next(1, 5),
                            Payload = isPrime
                                ? $"numbers:{rand.Next(5000, 20000)},threads:{rand.Next(1, 5)}"
                                : $"delay:{rand.Next(500, 3000)}"
                        };

                        try
                        {
                            system.Submit(job);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                });
            }

            Console.ReadLine();
        }

        static async Task RunTests()
        {
            await ProcessingSystemTests.Test_SuccessfulJob();
            await ProcessingSystemTests.Test_TimeoutAbort();
            ProcessingSystemTests.Test_PriorityQueue();
            await ProcessingSystemTests.Test_Idempotency();
        }
    }
}