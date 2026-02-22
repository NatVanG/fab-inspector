using PBIRInspectorLibrary;
using System.Text.Json;

namespace PBIRInspectorTests
{
    [TestFixture]
    public class InMemoryFileSystemTests
    {
        [Test]
        public void InMemoryFileSystem_CanCreateAndReadFiles()
        {
            // Arrange
            var fileSystem = new InMemoryFileSystem();
            var path = "test/file.json";
            var content = "{\"name\": \"test\"}";

            // Act
            fileSystem.AddFile(path, content);
            var result = fileSystem.ReadAllText(path);

            // Assert
            Assert.That(result, Is.EqualTo(content));
            Assert.That(fileSystem.FileExists(path), Is.True);
        }

        [Test]
        public void InMemoryFileSystem_CanListFilesAndDirectories()
        {
            // Arrange
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile("root/file1.json", "{}");
            fileSystem.AddFile("root/file2.json", "{}");
            fileSystem.AddDirectory("root/subfolder");
            fileSystem.AddFile("root/subfolder/file3.json", "{}");

            // Act
            var files = fileSystem.GetFiles("root");
            var directories = fileSystem.GetDirectories("root");

            // Assert
            Assert.That(files.Count(), Is.EqualTo(2));
            Assert.That(directories.Count(), Is.EqualTo(1));
        }

        [Test]
        public void InMemoryFileSystem_SupportsSearchPatterns()
        {
            // Arrange
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile("root/file.json", "{}");
            fileSystem.AddFile("root/file.txt", "text");
            fileSystem.AddFile("root/sub/file2.json", "{}");

            // Act
            var jsonFiles = fileSystem.GetFiles("root", "*.json", System.IO.SearchOption.AllDirectories);

            // Assert
            Assert.That(jsonFiles.Count(), Is.EqualTo(2));
        }

        [Test]
        public void Part_CanUseInMemoryFileSystem()
        {
            // Arrange
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile("test/report.json", "{\"name\": \"TestReport\"}");
            
            // Act
            var part = new PBIRInspectorLibrary.Part.Part("report.json", "test/report.json", null!, 
                PBIRInspectorLibrary.Part.PartFileSystemTypeEnum.File, fileSystem);
            var content = fileSystem.ReadAllText(part.FileSystemPath);

            // Assert
            Assert.That(content, Is.Not.Null);
            Assert.That(content, Does.Contain("TestReport"));
        }
    }
}
