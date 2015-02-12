using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Security;
using Microsoft.Ajax.Utilities;
using Octokit;
using OctokitDemo.Models;

namespace OctokitDemo.Controllers
{
    public class HomeController : Controller
    {
        // TODO: Replace the following values with the values from your application registration. Register an
        // application at https://github.com/settings/applications/new to get these values.
        const string clientId = "64677367ecf4b8ebf20b";
        private const string clientSecret = "30fff64c9a7688a70ca89538d7016e05802e518a";
        readonly GitHubClient client =
            new GitHubClient(new ProductHeaderValue("Hexter-test-cfd"));

        // This URL uses the GitHub API to get a list of the current user's
        // repositories which include public and private repositories.
        public async Task<ActionResult> Index(string organization)
        {
            var accessToken = Session["OAuthToken"] as string;
            if (accessToken != null)
            {
                // This allows the client to make requests to the GitHub API on the user's behalf
                // without ever having the user's OAuth credentials.
                client.Credentials = new Credentials(accessToken);
            }

            try
            {
                // The following requests retrieves all of the user's repositories and
                // requires that the user be logged in to work.

                IReadOnlyList<Repository> repositories;
                if(organization.IsNullOrWhiteSpace())
                    repositories = await client.Repository.GetAllForCurrent();
                else
                {
                    repositories = await client.Repository.GetAllForOrg(organization);
                }

                
                var model = new IndexViewModel(repositories);

                return View(model);
            }
            catch (AuthorizationException)
            {
                // Either the accessToken is null or it's invalid. This redirects
                // to the GitHub OAuth login page. That page will redirect back to the
                // Authorize action.
                return Redirect(GetOauthLoginUrl());
            }
        }

        public async Task<ActionResult> Report(string organization,string repository)        
        {

            var accessToken = Session["OAuthToken"] as string;
            if (accessToken != null)
            {
                // This allows the client to make requests to the GitHub API on the user's behalf
                // without ever having the user's OAuth credentials.
                client.Credentials = new Credentials(accessToken);
            }
            
            
            List<CummlativeFlowDiagram> model;
            if (Session[organization + ":" + repository] != null)
            {
                model = (List<CummlativeFlowDiagram>) Session[organization + ":" + repository];
            }
            else
            {
                var report = new CFD();
                model = report.Create(client, organization, repository);
                Session[organization + ":" + repository] = model;
            }

            return View(model);
        }
        // This is the Callback URL that the GitHub OAuth Login page will redirect back to.
        public async Task<ActionResult> Authorize(string code, string state)
        {
            if (!String.IsNullOrEmpty(code))
            {
                var expectedState = Session["CSRF:State"] as string;
                if (state != expectedState) throw new InvalidOperationException("SECURITY FAIL!");
                Session["CSRF:State"] = null;

                var token = await client.Oauth.CreateAccessToken(
                    new OauthTokenRequest(clientId, clientSecret, code));
                Session["OAuthToken"] = token.AccessToken;
            }

            return RedirectToAction("Index");
        }

        private string GetOauthLoginUrl()
        {
            string csrf = Membership.GeneratePassword(24, 1);
            Session["CSRF:State"] = csrf;

            // 1. Redirect users to request GitHub access
            var request = new OauthLoginRequest(clientId)            
            {
                Scopes = { "user", "notifications", "repo" },
                State = csrf
            };
            var oauthLoginUrl = client.Oauth.GetGitHubLoginUrl(request);
            return oauthLoginUrl.ToString();
        }

        public async Task<ActionResult> Emojis()
        {
            var emojis = await client.Miscellaneous.GetEmojis();

            return View(emojis);
        }
    }

    public class CFD
    {
        public List<CummlativeFlowDiagram> Create(GitHubClient client, string user, string repository)
        {
            var issues = new List<Issue>();
            

            IReadOnlyList<Octokit.Issue> response = client.Issue.GetForRepository(user, repository, new RepositoryIssueRequest() { State = ItemState.All }).Result;

            foreach (Octokit.Issue issue in response)
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
                    Events = client.Issue.Events.GetForIssue(user, repository, issue.Number).Result.Where(a => a.Event == EventInfoState.Labeled || a.Event == EventInfoState.Unlabeled).ToList()
                };
                issues.Add(i);
            }


            var report = new List<CummlativeFlowDiagram>();
            var min = issues.Min(a => a.CreatedDate);
            var grouped = issues.GroupBy(a => a.CreatedDate.UtcDateTime.Date).Select(a => new { a.Key, Count = a.Count(), Issues = a.ToList() }).OrderBy(a => a.Key);
            var statusLabes = new[] { "req:business", "req:ui", "ready", "trackduck", "in progress", "req:tech", "testing" };
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



                //Console.WriteLine(today.ToShortDateString() + " " + total + " " + closed + " " + states.Select(a => a.Label + " " + a.Count).Aggregate((a, b) =>  a  + " " +b ));
                report.Add(new CummlativeFlowDiagram()
                {
                    Period = today,
                    Backlog = states.Where(a => a.Label == "backlog").Sum(a => a.Count),
                    RequirementsBusiness = states.Where(a => a.Label == "req:business").Sum(a => a.Count),
                    RequirementsUi = states.Where(a => a.Label == "req:ui").Sum(a => a.Count),
                    InProgress = states.Where(a => a.Label == "in progress").Sum(a => a.Count),
                    Ready = states.Where(a => a.Label == "ready").Sum(a => a.Count),
                    Testing = states.Where(a => a.Label == "testing").Sum(a => a.Count),
                    Closed = closed,
                    Total = total
                });

            }

            return report;
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

    public class Issue
    {
        public string Id { get; set; }
        public bool Open { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        //Effort to complete issue
        public string Effort { get; set; }
        //Business risk reduced my implementing feature
        public string Risk { get; set; }
        //Business cost reduced by implementing feature
        public string Cost { get; set; }
        public string Url { get; set; }
        public string Description { get; set; }
        public bool Print { get; set; }
        public string Body { get; set; }
        public DateTimeOffset? ClosedDate { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public IList<EventInfo> Events { get; set; }
    }

    public class CummlativeFlowDiagram
    {
        public DateTime Period { get; set; }
        public int Backlog { get; set; }
        public int RequirementsBusiness { get; set; }
        public int RequirementsUi { get; set; }
        public int InProgress { get; set; }
        public int Ready { get; set; }
        public int Testing { get; set; }
        public int Closed { get; set; }
        public int Total { get; set; }
    }
}
