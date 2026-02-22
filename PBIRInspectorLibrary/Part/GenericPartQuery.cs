using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PBIRInspectorLibrary.Part
{
    internal class GenericPartQuery : BasePartQuery
    {
        public GenericPartQuery(string fileSystemPath) : this(fileSystemPath, null)
        {
        }

        public GenericPartQuery(string fileSystemPath, IFileSystem? fileSystem) : base(fileSystemPath, fileSystem)
        {
            SetParts(new Part("root", fileSystemPath, null!, PartFileSystemTypeEnum.Folder, _fileSystem));
        }
    }
}
