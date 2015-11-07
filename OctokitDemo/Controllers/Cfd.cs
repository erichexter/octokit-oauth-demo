using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Octokit;

namespace OctokitDemo.Controllers
{
    public class CumulativeFlowDiagramReport
    {
        public async Task<CummlativeFlowDiagram> Create(GitHubClient client, string user, string repository)
        {
            DateTime? startDate = null;


            var labels = new List<string> {"backlog", "waffle:ready", "waffle:in progress", "waffle:needs review"};
            try
            {
                var contents =
                    await client.Repository.Content.GetAllContents(user, repository, "cfd.json").ConfigureAwait(false);

                if (contents.Count > 0)
                {
                    var jsonstring = contents.First().Content;
                    dynamic data = JObject.Parse(jsonstring);

                    labels = data.items.ToObject<List<string>>();
                    startDate = data.startDate;
                }
            }
            catch (Exception)
            {
            }


            var issues = new List<Issue>();


            var response =
                await
                    client.Issue.GetAllForRepository(user, repository,
                        new RepositoryIssueRequest {State = ItemState.All}).ConfigureAwait(false);
            var events = await client.Issue.Events.GetAllForRepository(user, repository).ConfigureAwait(false);

            foreach (var issue in response)
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
                            events.Where(
                                a =>
                                    a.Issue.Number == issue.Number &&
                                    (a.Event == EventInfoState.Labeled || a.Event == EventInfoState.Unlabeled)).ToList()
                        //client.Issue.Events.GetForIssue(user, repository, issue.Number).Result.Where(a => a.Event == EventInfoState.Labeled || a.Event == EventInfoState.Unlabeled).ToList()
                    };
                    issues.Add(i);
                }
                catch
                {
                }
            }


            var report = new List<CummlativeFlowDiagramItem>();
            if (!startDate.HasValue)
                startDate = issues.Min(a => a.CreatedDate).Date;
            var grouped =
                issues.GroupBy(a => a.CreatedDate.UtcDateTime.Date)
                    .Select(a => new {a.Key, Count = a.Count(), Issues = a.ToList()})
                    .OrderBy(a => a.Key);

            for (var i = 0; i < (DateTime.Now - startDate.Value).Days; i++)
            {
                var today = startDate.Value.AddDays(i).Date;

                var activeIssues = grouped.Where(a => a.Key <= today).SelectMany(a => a.Issues);
                var total = activeIssues.Count();

                var closed = activeIssues.Count(a => a.ClosedDate.GetValueOrDefault(today.AddDays(2)).Date <= today);
                var states = activeIssues.Where(a => a.ClosedDate.GetValueOrDefault(today.AddDays(2)).Date > today)
                    .Select(
                        a =>
                            new
                            {
                                Issue = a,
                                Label =
                                    a.Events.OrderByDescending(b => b.CreatedAt)
                                        .FirstOrDefault(b => labels.Contains(b.Label.Name.ToString())) != null
                                        ? a.Events.OrderByDescending(b => b.CreatedAt)
                                            .FirstOrDefault(b => labels.Contains(b.Label.Name.ToString())).Label.Name
                                        : "backlog"
                            }).GroupBy(a => a.Label).Select(a => new {Label = a.Key, Count = a.Count()});

                var item = new CummlativeFlowDiagramItem
                {
                    Period = today,
                    Total = total
                };

                item.Phases.Add(new Phase
                {
                    Name = "Backlog",
                    Count = states.Where(a => a.Label == "backlog").Sum(a => a.Count)
                });
                foreach (var phase in labels)
                {
                    item.Phases.Add(new Phase
                    {
                        Name = phase,
                        Count = states.Where(a => a.Label == phase).Sum(a => a.Count)
                    });
                }
                item.Phases.Add(new Phase {Name = "Closed", Count = closed});
                report.Add(item);
            }
            report =
                report.Where(a => a.Period.DayOfWeek != DayOfWeek.Saturday && a.Period.DayOfWeek != DayOfWeek.Sunday)
                    .ToList();

            var cumlativeFlowDiagram = new CummlativeFlowDiagram {States = labels.ToList(), Items = report.ToList()};
            cumlativeFlowDiagram.States.Insert(0, "Backlog");
            cumlativeFlowDiagram.States.Add("Closed");
            return cumlativeFlowDiagram;
        }

        public static GitHubClient GitHubClient()
        {
            var client = new GitHubClient(new ProductHeaderValue("export-issues"));
            var username = "qsautomation";
            var password = ConfigurationManager.AppSettings["password"];
            var basicAuth = new Credentials(username, password);
            client.Credentials = basicAuth;
            return client;
        }
    }
}