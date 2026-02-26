using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PBIRInspectorLibrary.Part
{
    public class PartInfo
    { 
        private readonly IFileSystem _fileSystem;

        public string FileSystemName { get; set; }
        public string FileSystemPath { get; set; }
        public PartFileSystemTypeEnum PartFileSystemType { get; private set; } = PartFileSystemTypeEnum.None;
        public bool Exists { get; set; }
        public long? FileSize { get; private set; }
        public int? FileCount { get; private set; }

        public PartInfo(Part part, bool setAdvancedProps = true)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));

            _fileSystem = part.GetFileSystem();
            FileSystemName = part.FileSystemName;
            FileSystemPath = part.FileSystemPath;
            PartFileSystemType = part.PartFileSystemType;

            if (setAdvancedProps) this.setAdvancedProps();
        }

        public PartInfo(string fileSystemPath, bool setAdvancedProps = false, IFileSystem fileSystem = null)
        {
            _fileSystem = fileSystem ?? new PhysicalFileSystem();
            FileSystemName = _fileSystem.GetFileNameWithoutExtension(fileSystemPath);
            FileSystemPath = fileSystemPath;
            PartFileSystemType = _fileSystem.DirectoryExists(FileSystemPath) ? PartFileSystemTypeEnum.Folder : (_fileSystem.FileExists(fileSystemPath) ? PartFileSystemTypeEnum.File : PartFileSystemTypeEnum.None);
            Exists = PartFileSystemType == PartFileSystemTypeEnum.File || PartFileSystemType == PartFileSystemTypeEnum.Folder;

            if (setAdvancedProps) this.setAdvancedProps();
        }

        private void setAdvancedProps()
        {
            if (_fileSystem.FileExists(FileSystemPath))
            {
                this.FileCount = 1;
                this.FileSize = _fileSystem.GetFileSize(FileSystemPath);
            }

            if (_fileSystem.DirectoryExists(FileSystemPath))
            {
                var files = _fileSystem.GetFiles(FileSystemPath, "*", System.IO.SearchOption.AllDirectories);
                var filesList = files.ToList();
                this.FileCount = filesList.Count;
                this.FileSize = filesList.Sum(f => _fileSystem.GetFileSize(f));
            }
        }
    }
}
