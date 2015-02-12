using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Helpers;
using Newtonsoft.Json.Linq;
using Octokit;

namespace OctokitDemo.Controllers
{
    public class Cfd
    {
        public async Task<CummlativeFlowDiagram> Create(GitHubClient client, string user, string repository)
        {



            var statusLabes = new List<string> { "backlog", "waffle:ready", "waffle:in progress", "waffle:needs review" };

            try
            {
                var contents = await client.Repository.Content.GetContents(user, repository, "cfd.json");

                if (contents.Count > 0)
                {
                    var jsonstring = contents.First().Content;
                    dynamic data = JObject.Parse(jsonstring);

                    statusLabes = data.items.ToObject<List<string>>();
                }
            }
            catch (Exception)
            {
                
                
            }
            

            var issues = new List<Issue>();
            

            IReadOnlyList<Octokit.Issue> response = client.Issue.GetForRepository(user, repository, new RepositoryIssueRequest() { State = ItemState.All }).Result;

            foreach (Octokit.Issue issue in response)            
            {
                try
                {
                    var i = new Issue
                    {
                        Title = issue.Title,
                        Description = issue.Body,
                        Id = issue.Number.ToString(),
                        Open = !issue.ClosedAt.HasValue,
                        Url = issue.HtmlUrl.ToString(),
                        Body = issue.Body,
                        CreatedDate = issue.CreatedAt.ToUniversalTime(),
                        ClosedDate = issue.ClosedAt,
                        Events =
                            client.Issue.Events.GetForIssue(user, repository, issue.Number)
                                .Result.Where(
                                    a => a.Event == EventInfoState.Labeled || a.Event == EventInfoState.Unlabeled)
                                .ToList()
                    };
                    issues.Add(i);
                }
                catch
                {
                }
            }


            var report = new List<CummlativeFlowDiagramItem>();
            var min = issues.Min(a => a.CreatedDate);
            var grouped = issues.GroupBy(a => a.CreatedDate.UtcDateTime.Date).Select(a => new { a.Key, Count = a.Count(), Issues = a.ToList() }).OrderBy(a => a.Key);
            
            for (int i = 0; i < (DateTime.Now - min).Days; i++)
            {
                var today = min.AddDays(i).Date;

                var activeIssues = grouped.Where(a => a.Key <= today).SelectMany(a => a.Issues);
                var total = activeIssues.Count();

                var closed = activeIssues.Count(a => a.ClosedDate.GetValueOrDefault(today.AddDays(2)).Date <= today);
                var states = activeIssues.Where(a => a.ClosedDate.GetValueOrDefault(today.AddDays(2)).Date > today).Select(
                    a =>
                        new
                        {
                            Issue = a,
                            Label =
                                a.Events.OrderByDescending(b => b.CreatedAt)
                                    .FirstOrDefault(b => statusLabes.Contains(b.Label.Name.ToString())) != null ? a.Events.OrderByDescending(b => b.CreatedAt)
                                        .FirstOrDefault(b => statusLabes.Contains(b.Label.Name.ToString())).Label.Name : "backlog"

                        }).GroupBy(a => a.Label).Select(a => new { Label = a.Key, Count = a.Count() });

                var item = new CummlativeFlowDiagramItem()
                {
                    Period = today,
                    Total = total
                };

                item.Phases.Add(new Phase() { Name = "Backlog", Count = states.Where(a => a.Label == "backlog").Sum(a => a.Count) });
                foreach (var phase in statusLabes)
                {
                    item.Phases.Add(new Phase() { Name = phase, Count = states.Where(a => a.Label == phase).Sum(a => a.Count) });
                }                
                item.Phases.Add(new Phase() { Name = "Closed", Count = closed });
                report.Add(item);

            }

            var cumlativeFlowDiagram = new CummlativeFlowDiagram {States=statusLabes.ToList(),Items = report.Where(a => a.Total > 10).ToList()};
            cumlativeFlowDiagram.States.Insert(0,"Backlog");
            cumlativeFlowDiagram.States.Add("Closed");
            return cumlativeFlowDiagram;
        }

        public static GitHubClient GitHubClient()
        {
            var client = new GitHubClient(new ProductHeaderValue("export-issues"));
            var username = "qsautomation";
            var password = System.Configuration.ConfigurationManager.AppSettings["password"];
            var basicAuth = new Credentials(username, password);
            client.Credentials = basicAuth;
            return client;
        }
    }
}