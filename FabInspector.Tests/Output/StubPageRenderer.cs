using FabInspector.Core;
using FabInspector.Core.Output;

namespace FabInspector.Tests.Output
{
    /// <summary>
    /// Hand-rolled stub for IReportPageWireframeRenderer (no mocking library in project).
    /// </summary>
    internal class StubPageRenderer : IReportPageWireframeRenderer
    {
        public List<(IEnumerable<TestResult> FieldMapResults, IEnumerable<TestResult> TestResults, string OutputDir, string TestedFilePath)> DrawCalls { get; } = new();

        public string Base64Result { get; set; } = "AAAA";

        public void DrawReportPages(IEnumerable<TestResult> fieldMapResults, IEnumerable<TestResult> testResults, string outputDir, string testedFilePath)
        {
            DrawCalls.Add((fieldMapResults, testResults, outputDir, testedFilePath));
        }

        public string ConvertBitmapToBase64(string bitmapPath)
        {
            return Base64Result;
        }
    }
}
