using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FabInspector.Core.Part
{
    internal class CrossItemPartQuery : BasePartQuery
    {
        public CrossItemPartQuery(string fileSystemPath) : this(fileSystemPath, null!)
        {
        }

        public CrossItemPartQuery(string fileSystemPath, IFabricFileSystem? fileSystem) : base(fileSystemPath, fileSystem)
        {
            SetParts(new Part("root", fileSystemPath, null!, PartFileSystemTypeEnum.Folder, _fileSystem));
        }

        public override object? Invoke(string query, Part context)
        {
            object? result = null;
            if (string.IsNullOrEmpty(query)) return context;

            result = SearchParts(query, context);
            
            return result;
        }
    }
}
