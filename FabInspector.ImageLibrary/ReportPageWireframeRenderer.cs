using FabInspector.ImageLibrary.Drawing;
using FabInspector.Core;
using FabInspector.Core.Output;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;


namespace FabInspector.ImageLibrary
{
    public class ReportPageWireframeRenderer : IReportPageWireframeRenderer
    {
        private const int DefaultPageHeight = 720;
        private const int DefaultPageWidth = 1280;

        public void DrawReportPages(IEnumerable<TestResult> fieldMapResults, IEnumerable<TestResult> testResults, string outputDir, string testedFilePath)
        {
            var pageSizes = LoadPageSizes(testedFilePath);

            foreach (TestResult testResult in testResults.Where(_ => !string.IsNullOrEmpty(_.ParentName)))
            {
                var testResultId = testResult.Id;

                foreach (TestResult fields in fieldMapResults.Where(_ => string.Equals(_.ParentName, testResult.ParentName)))
                {
                    var pageName = fields.ParentName;
                    var pageDisplayName = fields.ParentDisplayName;
                    var sourcePageSize = GetPageSize(pageName, pageSizes);
                    var scale = (decimal)DefaultPageHeight / sourcePageSize.Height;
                    var pageSize = new ReportPage.PageSize
                    {
                        Height = DefaultPageHeight,
                        Width = (int)Math.Round(sourcePageSize.Width * scale)
                    };
                    List<ReportPage.VisualContainer> visuals = new List<ReportPage.VisualContainer>();
                    foreach (var f in fields.Actual!.AsArray())
                    {
                        var name = f!["name"]?.ToString();
                        var visualType = f!["visualType"]?.ToString();
                        var x = ScaleValue(f!["x"]!.GetValue<decimal>(), scale);
                        var y = ScaleValue(f!["y"]!.GetValue<decimal>(), scale);
                        var height = ScaleValue(f!["height"]!.GetValue<decimal>(), scale);
                        var width = ScaleValue(f!["width"]!.GetValue<decimal>(), scale);
                        var visible = f!["visible"]!.GetValue<bool>();

                        //If a visual name is returned in the test actual array then highlight it as a test failure in the page wireframe
                        //A visual name can be returned either as a JsonValue or a named JsonObject (i.e. {"name": "VisualName"}) hence the "or else" operator below (i.e. "||")
                        var visualNameInTestActualArray = (testResult.Actual != null && testResult.Actual is JsonArray
                                                                && testResult.Actual.AsArray().Any(_ => _ != null
                                                                    && _ is JsonValue && _.AsValue().ToString().Equals(name)))
                                                          || (testResult.Actual != null && testResult.Actual is JsonArray
                                                        && testResult.Actual.AsArray().Any(_ => _ != null && _ is JsonObject && _ is not JsonValue
                                                            && _["name"] != null && _["name"] is JsonValue && _["name"]!.AsValue().ToString().Equals(name)));


                        bool visualPass = !visualNameInTestActualArray;
                        visuals.Add(new ReportPage.VisualContainer { Name = name ?? string.Empty, VisualType = visualType ?? string.Empty, X = x, Y = y, Height = height, Width = width, Pass = visualPass, Visible = visible });

                    }
                    using var rp = new ReportPage(pageName ?? string.Empty, pageDisplayName ?? string.Empty, pageSize, visuals);
                    rp.Draw();
                    var filename = string.Concat(testResultId, ".png");
                    var filepath = Path.Combine(outputDir, filename);
                    rp.Save(filepath);
                }
            }
        }

        public string ConvertBitmapToBase64(string bitmapPath)
        {
            if (string.IsNullOrEmpty(bitmapPath))
            {
                throw new ArgumentException("Bitmap path is null or empty.", nameof(bitmapPath));
            }
            var executableDirectory = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? AppContext.BaseDirectory;
            bitmapPath = Path.IsPathRooted(bitmapPath)
                ? bitmapPath
                : Path.Combine(executableDirectory, bitmapPath);
            if (!File.Exists(bitmapPath))
            {
                throw new ArgumentException($"Bitmap path does not exist: {bitmapPath}", nameof(bitmapPath));
            }
            using var bitmap = SKBitmap.Decode(bitmapPath);
            var skData = bitmap.Encode(SKEncodedImageFormat.Png, 100);

            using MemoryStream m = new();

            skData.SaveTo(m);

            byte[] imageBytes = m.ToArray();

            // Convert byte[] to Base64 String
            string base64String = Convert.ToBase64String(imageBytes);
            return base64String;
        }

        private static int ScaleValue(decimal value, decimal scale)
        {
            return (int)Math.Round(value * scale);
        }

        private static ReportPage.PageSize GetPageSize(string? pageName, IReadOnlyDictionary<string, ReportPage.PageSize> pageSizes)
        {
            if (!string.IsNullOrEmpty(pageName) && pageSizes.TryGetValue(pageName, out var pageSize))
            {
                return pageSize;
            }

            return new ReportPage.PageSize { Height = DefaultPageHeight, Width = DefaultPageWidth };
        }

        private static Dictionary<string, ReportPage.PageSize> LoadPageSizes(string testedFilePath)
        {
            var reportFolderPath = ResolveReportFolderPath(testedFilePath);
            if (string.IsNullOrEmpty(reportFolderPath) || !Directory.Exists(reportFolderPath))
            {
                return new Dictionary<string, ReportPage.PageSize>(StringComparer.Ordinal);
            }

            return Directory
                .EnumerateFiles(reportFolderPath, "page.json", SearchOption.AllDirectories)
                .Select(TryReadPageSize)
                .Where(_ => _.HasValue)
                .Select(_ => _.GetValueOrDefault())
                .ToDictionary(_ => _.Name, _ => _.PageSize, StringComparer.Ordinal);
        }

        private static (string Name, ReportPage.PageSize PageSize)? TryReadPageSize(string pagePath)
        {
            var pageNode = JsonNode.Parse(File.ReadAllText(pagePath));
            var name = pageNode?["name"]?.GetValue<string>();
            var width = pageNode?["width"]?.GetValue<int?>();
            var height = pageNode?["height"]?.GetValue<int?>();

            if (string.IsNullOrEmpty(name) || !width.HasValue || width.Value <= 0 || !height.HasValue || height.Value <= 0)
            {
                return null;
            }

            return (name, new ReportPage.PageSize { Width = width.Value, Height = height.Value });
        }

        private static string? ResolveReportFolderPath(string testedFilePath)
        {
            if (string.IsNullOrWhiteSpace(testedFilePath))
            {
                return null;
            }

            if (Directory.Exists(testedFilePath))
            {
                return testedFilePath;
            }

            if (!File.Exists(testedFilePath) || !testedFilePath.EndsWith(".pbip", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var pbipNode = JsonNode.Parse(File.ReadAllText(testedFilePath));
            var relativeReportPath = pbipNode?["artifacts"]?[0]?["report"]?["path"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(relativeReportPath))
            {
                return null;
            }

            var baseDirectory = Path.GetDirectoryName(testedFilePath);
            if (string.IsNullOrEmpty(baseDirectory))
            {
                return null;
            }

            return Path.GetFullPath(Path.Combine(baseDirectory, relativeReportPath));
        }
    }
}
