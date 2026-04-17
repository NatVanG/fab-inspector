using FabInspector.Core;

namespace FabInspector.Tests
{
    [TestFixture]
    public class UtilsTests
    {
        [Test]
        public void NormalisePathWinTest()
        {
            var result = Utils.NormalizePath(@"C:\Folder\File.txt");
            Assert.That(result, Is.EqualTo("C:Folder:File.txt"));
        }

        [Test]
        public void NormalisePathLinuxTest()
        {
            var result = Utils.NormalizePath("/home/user/file.txt");
            Assert.That(result, Is.EqualTo(":home:user:file.txt"));
        }

        [Test]
        public void NormaliseEmptyPathTest()
        {
            var result = Utils.NormalizePath(string.Empty);
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void NormaliseNullPathTest()
        {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            var result = Utils.NormalizePath(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            Assert.That(result, Is.EqualTo(string.Empty));
        }
    }
}