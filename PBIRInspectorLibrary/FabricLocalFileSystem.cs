using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PBIRInspectorLibrary
{
    public class FabricLocalFileSystem : LocalFileSystem, IFabricFileSystem
    {

        /// <summary>
        /// Initializes a new instance of PhysicalFileSystem with an empty root path
        /// </summary>
        public FabricLocalFileSystem() : base(string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of PhysicalFileSystem with a specified root path
        /// </summary>
        /// <param name="rootPath">The root path for this file system instance</param>
        public FabricLocalFileSystem(string rootPath) : base(rootPath)
        {
            
        }

        public IEnumerable<FabricItem> GetFabricItems(string path, string searchPattern, SearchOption searchOption)
        {
            throw new NotImplementedException();
        }
    }
}
