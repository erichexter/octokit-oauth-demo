using System;
using System.Collections.Generic;
using Octokit;

namespace OctokitDemo.Controllers
{
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
        public List<IssueEvent> Events { get; set; }
    }
}