using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;

namespace FabInspector.Core.Part
{
    internal static class PartQueryFactory
    {
        internal static IPartQuery CreatePartQuery(string type, string path, IFabricFileSystem? fileSystem = null)
        {
            if (type.Contains('|', StringComparison.InvariantCultureIgnoreCase))
            {
                return new CrossItemPartQuery(path, fileSystem);
            }
            else
            {
                switch (type.ToLowerInvariant())
                {
                    case "*":
                        return new CrossItemPartQuery(path, fileSystem);
                    case "report":
                        return new PBIRPartQuery(path, fileSystem);
                    case "semanticmodel":
                        return new TMDLPartQuery(path, fileSystem);
                    case "report_deprecated":
                        return new PBIRPartQuery_deprecated(path, fileSystem);
                    default:
                        return new GenericPartQuery(path, fileSystem);
                }
            }
        }
    }
}
