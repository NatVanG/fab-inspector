using FabInspector.Core.Output;
using SkiaSharp;
using System.Text.Json.Nodes;

namespace FabInspector.Tests
{
    [TestFixture]
    public class ReportPageWireframeRendererAspectRatioTests
    {
        private string _tempDir = null!;
        private const string ReportPath = "/home/runner/work/fab-inspector/fab-inspector/FabInspector.Tests/Files/pbip/Base-rules-passes.Report";
        private const string PageName = "ReportSectionadc267c0d12e40458799";

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FabInspectorTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void ImageLibrary_DrawReportPages_UsesPageAspectRatioWithFixedHeight()
        {
            var renderer = new FabInspector.ImageLibrary.ReportPageWireframeRenderer();

            renderer.DrawReportPages(CreateFieldMapResults(), CreateTestResults(), _tempDir, ReportPath);

            var outputPath = Directory.GetFiles(_tempDir, "*.png").Single();
            using var bitmap = SKBitmap.Decode(outputPath);
            Assert.That(bitmap.Width, Is.EqualTo(970));
            Assert.That(bitmap.Height, Is.EqualTo(730));
        }

        [Test]
        public void WinImageLibrary_DrawReportPages_UsesPageAspectRatioWithFixedHeight()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Ignore("WinImageLibrary rendering requires GDI+ on Windows.");
            }

            var renderer = new FabInspector.WinImageLibrary.ReportPageWireframeRenderer();

            renderer.DrawReportPages(CreateFieldMapResults(), CreateTestResults(), _tempDir, ReportPath);

            var outputPath = Directory.GetFiles(_tempDir, "*.png").Single();
            using var bitmap = SKBitmap.Decode(outputPath);
            Assert.That(bitmap.Width, Is.EqualTo(970));
            Assert.That(bitmap.Height, Is.EqualTo(730));
        }

        private static IEnumerable<TestResult> CreateFieldMapResults()
        {
            return
            [
                new TestResult
                {
                    RuleName = "Field map",
                    Message = "ok",
                    Pass = true,
                    ParentName = PageName,
                    ParentDisplayName = "Page 1",
                    Actual = JsonNode.Parse("""
                    [
                      {
                        "name": "Visual1",
                        "visualType": "card",
                        "x": 80,
                        "y": 60,
                        "width": 160,
                        "height": 120,
                        "visible": true
                      }
                    ]
                    """)
                }
            ];
        }

        private static IEnumerable<TestResult> CreateTestResults()
        {
            return
            [
                new TestResult
                {
                    RuleName = "Report rule",
                    Message = "ok",
                    Pass = true,
                    ParentName = PageName,
                    ParentDisplayName = "Page 1",
                    Actual = JsonNode.Parse("[]")
                }
            ];
        }
    }
}
