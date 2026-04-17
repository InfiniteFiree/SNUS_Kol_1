
namespace SNUS_Kol_1
{
    public enum JobType
    {
        Prime,
        IO
    }

    public class Job
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public JobType Type { get; set; }
        public string Payload { get; set; }
        public int Priority { get; set; }
    }

    public class JobHandle
    {
        public Guid Id { get; set; }
        public Task<int> Result { get; set; }
    }

    public class JobExecutionInfo
    {
        public Job Job { get; set; }
        public bool Success { get; set; }
        public int? Result { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
