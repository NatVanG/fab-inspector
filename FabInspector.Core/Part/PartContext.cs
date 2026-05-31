using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabInspector.Core.Part
{
    public class PartContext
    {
           public required IPartQuery PartQuery { get; set; }
           public required Part Part { get; set; }
        public string? RuleName { get; set; }
        public string? ItemPath { get; set; }
        public IInspectionMessageReporter? MessageReporter { get; set; }

        /// <summary>
        /// Token provider used by operators that call authenticated APIs.
        /// Carried on the context so concurrent inspection runs (for example, multiple
        /// users in a multi-tenant host) cannot share or overwrite each other's tokens.
        /// </summary>
        public ITokenProvider? TokenProvider { get; set; }

        /// <summary>
        /// Fabric workspace ID (GUID) targeted by the current inspection run.
        /// </summary>
        public string? FabricWorkspaceId { get; set; }

        /// <summary>
        /// Fabric item file path (local) or ID (remote) targeted by the current inspection run.
        /// </summary>
        public string? FabricItem { get; set; }
    }
}