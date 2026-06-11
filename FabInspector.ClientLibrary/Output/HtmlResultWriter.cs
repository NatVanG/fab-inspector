using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;

namespace FabInspector.ClientLibrary.Output
{
    internal class HtmlResultWriter : IResultOutputWriter
    {
        private readonly IReportPageWireframeRenderer _pageRenderer;

        public HtmlResultWriter(IReportPageWireframeRenderer pageRenderer)
        {
            _pageRenderer = pageRenderer;
        }

        public Task WriteAsync(OutputContext context)
        {
            var pbiInspectorPngPath = AppUtils.ResolveFromExecutableDirectory(Constants.PBIInspectorPNG);
            var templatePath = AppUtils.ResolveFromExecutableDirectory(Constants.TestRunHTMLTemplate);
            string pbiinspectorlogobase64 = string.Concat(Constants.Base64ImgPrefix, _pageRenderer.ConvertBitmapToBase64(pbiInspectorPngPath));
            string template = File.ReadAllText(templatePath);
            string html = template.Replace(Constants.LogoPlaceholder, pbiinspectorlogobase64, StringComparison.OrdinalIgnoreCase);
            html = html.Replace(Constants.VersionPlaceholder, AppUtils.About(), StringComparison.OrdinalIgnoreCase);
            html = html.Replace(Constants.JsonPlaceholder, context.JsonTestRun, StringComparison.OrdinalIgnoreCase);

            var outputHTMLFilePath = Path.Combine(context.LocalOutputDirPath, Constants.TestRunHTMLFileName);

            context.OnMessage(MessageTypeEnum.Information, string.Format("Writing HTML output to file at \"{0}\".", outputHTMLFilePath));
            File.WriteAllText(outputHTMLFilePath, html);

            if (context.IsOneLakeOutput)
            {
                var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var runFolder = string.Concat("TestRun_", context.TestRunId.ToString("N"), "_", context.Timestamp);
                var relativeHtmlPath = Path.Combine(dateFolder, runFolder, Constants.TestRunHTMLFileName);
                context.OutputArtifacts.Add((outputHTMLFilePath, relativeHtmlPath));
            }

            // Results have been written to a temporary directory so show output to user automatically.
            if (!context.IsOneLakeOutput && context.DeleteOutputDirOnExit && !context.CONSOLEOutput)
            {
                AppUtils.OpenUrl(outputHTMLFilePath);
            }

            return Task.CompletedTask;
        }
    }
}
