
namespace SNUS_Kol_1
{
    public class ProcessingSystemTests
    {
        public static async Task Test_SuccessfulJob()
        {
            var system = new ProcessingSystem(2, 10);

            var job = new Job
            {
                Type = JobType.IO,
                Payload = "delay:500",
                Priority = 1
            };

            var handle = system.Submit(job);

            int result = await handle.Result;

            Console.WriteLine($"Test_SuccessfulJob PASSED -> Result: {result}");
        }

        public static async Task Test_TimeoutAbort()
        {
            var system = new ProcessingSystem(2, 10);

            var job = new Job
            {
                Type = JobType.IO,
                Payload = "delay:5000", // > 2s → timeout
                Priority = 1
            };

            var handle = system.Submit(job);

            try
            {
                await handle.Result;
            }
            catch
            {
                Console.WriteLine("Test_TimeoutAbort PASSED -> Job aborted");
            }
        }

        public static void Test_PriorityQueue()
        {
            var system = new ProcessingSystem(0, 10);

            var job1 = new Job { Type = JobType.IO, Payload = "delay:500", Priority = 3 };
            var job2 = new Job { Type = JobType.IO, Payload = "delay:500", Priority = 1 };

            system.Submit(job1);
            system.Submit(job2);

            var top = system.GetTopJobs(2).ToList();

            if (top[0].Priority == 1)
                Console.WriteLine("Test_PriorityQueue PASSED");
            else
                Console.WriteLine("Test_PriorityQueue FAILED");
        }

        public static async Task Test_Idempotency()
        {
            var system = new ProcessingSystem(2, 10);

            var id = Guid.NewGuid();

            var job1 = new Job
            {
                Id = id,
                Type = JobType.IO,
                Payload = "delay:500",
                Priority = 1
            };

            var job2 = new Job
            {
                Id = id, // same ID
                Type = JobType.IO,
                Payload = "delay:500",
                Priority = 1
            };

            system.Submit(job1);

            try
            {
                system.Submit(job2);
                Console.WriteLine("Test_Idempotency FAILED");
            }
            catch
            {
                Console.WriteLine("Test_Idempotency PASSED");
            }
        }
    }
}
