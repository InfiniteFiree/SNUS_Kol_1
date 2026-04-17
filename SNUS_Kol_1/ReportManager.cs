using System.Xml.Linq;

namespace SNUS_Kol_1
{
    public static class ReportManager
    {
        public static void Generate(List<JobExecutionInfo> history, int index)
        {
            var doc = new XDocument(
                new XElement("Report",
                    new XElement("Stats",
                        history.GroupBy(h => h.Job.Type)
                            .Select(g => new XElement("Type",
                                new XAttribute("Name", g.Key),
                                new XElement("Count", g.Count()),
                                new XElement("AvgTime",
                                    g.Average(x => x.Duration.TotalMilliseconds)),
                                new XElement("Failures",
                                    g.Count(x => !x.Success))
                            ))
                    )
                )
            );

            doc.Save($"report_{index}.xml");
        }
    }
}
