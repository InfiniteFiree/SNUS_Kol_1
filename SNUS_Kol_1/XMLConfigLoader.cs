using System.Xml.Linq;

namespace SNUS_Kol_1
{
    public class SystemConfig
    {
        public int WorkerCount { get; set; }
        public int MaxQueueSize { get; set; }
        public List<Job> Jobs { get; set; } = new();
    }

    public static class XmlConfigLoader
    {
        public static SystemConfig Load(string path)
        {
            var doc = XDocument.Load(path);

            var config = new SystemConfig
            {
                WorkerCount = int.Parse(doc.Root.Element("WorkerCount").Value),
                MaxQueueSize = int.Parse(doc.Root.Element("MaxQueueSize").Value)
            };

            foreach (var jobElem in doc.Root.Element("Jobs").Elements("Job"))
            {
                var job = new Job
                {
                    Type = Enum.Parse<JobType>(jobElem.Attribute("Type").Value),
                    Payload = jobElem.Attribute("Payload").Value,
                    Priority = int.Parse(jobElem.Attribute("Priority").Value)
                };

                config.Jobs.Add(job);
            }

            return config;
        }
    }
}
