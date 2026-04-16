using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PBIRInspectorLibrary.Part
{
    public class PartContext
    {
           public required IPartQuery PartQuery { get; set; }
           public required Part Part { get; set; }
        public string? RuleName { get; set; }
        public string? ItemPath { get; set; }
        public IInspectionMessageReporter? MessageReporter { get; set; }
    }
}