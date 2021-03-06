﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        //const string clientId = "64677367ecf4b8ebf20b";
        //private const string clientSecret = "30fff64c9a7688a70ca89538d7016e05802e518a";
        readonly GitHubClient client =new GitHubClient(new ProductHeaderValue("Hexter-test-cfd"));

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

                List<Repository> repositories=new List<Repository>();

                repositories.AddRange( await client.Repository.GetAllForCurrent().ConfigureAwait(false) );
                               
                
                var model = new IndexViewModel(repositories.Where(a=>a.OpenIssuesCount>0).OrderByDescending(a=>a.OpenIssuesCount));

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

        public async Task<ActionResult> WorkInProcessReport(string organization, string repository)
        {

            var accessToken = Session["OAuthToken"] as string;
            if (accessToken != null)
            {
                // This allows the client to make requests to the GitHub API on the user's behalf
                // without ever having the user's OAuth credentials.
                client.Credentials = new Credentials(accessToken);
            }
            else
                return Redirect(GetOauthLoginUrl());

            CummlativeFlowDiagram model;
            if (Session[organization + ":" + repository] != null)
            {
                model = (CummlativeFlowDiagram)Session[organization + ":" + repository];
            }
            else
            {
                var report = new CumulativeFlowDiagramReport();
                model = await report.Create(client, organization, repository);
                model.Title = organization + "/" + repository;
                Session[organization + ":" + repository] = model;
            }

            model.States.Clear();
            model.States.Add("WIP");
            model.Items=model.Items.Select(a =>
                new CummlativeFlowDiagramItem()
                {
                Period    = a.Period,
                Total = 0,
                Phases = new List<Phase>(new Phase[]{new Phase()
                {
                    Name="WIP",
                    Count = a.Phases.Where(b=>b.Name!="Closed" && b.Name!="Backlog").Sum(b=>b.Count)
                }, })}).ToList();
                
            return View("Report",model);
        }

        public async Task<ActionResult> Report(string organization, string repository)        
        {

            var accessToken = Session["OAuthToken"] as string;
            if (accessToken != null)
            {
                // This allows the client to make requests to the GitHub API on the user's behalf
                // without ever having the user's OAuth credentials.
                client.Credentials = new Credentials(accessToken);            
            }
            else
                return Redirect(GetOauthLoginUrl());
            
            CummlativeFlowDiagram  model;
            if (Session[organization + ":" + repository] != null)
            {
                model = (CummlativeFlowDiagram) Session[organization + ":" + repository];
            }
            else
            {
                var report = new CumulativeFlowDiagramReport();
                model = await report.Create(client, organization, repository).ConfigureAwait(false);
                model.Title = organization + "/" + repository;
                Session[organization + ":" + repository] = model;
            }

            return View(model);
        }
        // This is the Callback URL that the GitHub OAuth Login page will redirect back to.
        public async Task<ActionResult> Authorize(string code, string state)
        {
            var settings = System.Configuration.ConfigurationManager.AppSettings;
            if (!String.IsNullOrEmpty(code))
            {
                var expectedState = Session["CSRF:State"] as string;
                if (state != expectedState) throw new InvalidOperationException("SECURITY FAIL!");
                Session["CSRF:State"] = null;

                var token = await client.Oauth.CreateAccessToken(
                    new OauthTokenRequest( settings["github.clientId"], settings["github.clientSecret"], code));
                Session["OAuthToken"] = token.AccessToken;
            }

            return RedirectToAction("Index");
        }

        private string GetOauthLoginUrl()
        {
            var settings = System.Configuration.ConfigurationManager.AppSettings;
            string csrf = Membership.GeneratePassword(24, 1);
            Session["CSRF:State"] = csrf;

            // 1. Redirect users to request GitHub access
            var request = new OauthLoginRequest(settings["github.clientId"])  
            {
                Scopes = { "user", "notifications", "repo" },
                State = csrf
            };
            var oauthLoginUrl = client.Oauth.GetGitHubLoginUrl(request);
            return oauthLoginUrl.ToString();
        }

    }
}
