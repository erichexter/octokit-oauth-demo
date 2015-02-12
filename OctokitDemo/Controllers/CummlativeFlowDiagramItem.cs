using System;
using System.Collections.Generic;

namespace OctokitDemo.Controllers
{
    public class CummlativeFlowDiagramItem
    {
        public CummlativeFlowDiagramItem()
        {
            Phases=new List<Phase>();
        }

        public DateTime Period { get; set; }
        public int Total { get; set; }
        public List<Phase> Phases { get; set; }
    }
}