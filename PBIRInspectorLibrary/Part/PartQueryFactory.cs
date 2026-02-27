using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;

namespace PBIRInspectorLibrary.Part
{
    internal static class PartQueryFactory
    {
        internal static IPartQuery CreatePartQuery(string type, string path, IFabricFileSystem? fileSystem = null)
        {
            switch (type.ToLowerInvariant())
            {
                case "*":
                    return new CrossItemPartQuery(path, fileSystem);
                case "report":
                    return new PBIRPartQuery(path, fileSystem);
                case "report_deprecated":
                    return new PBIRPartQuery_deprecated(path, fileSystem);
                default:
                    return new GenericPartQuery(path, fileSystem);
            }
        }
    }
}
