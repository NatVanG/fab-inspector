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
            string pbiinspectorlogobase64 = string.Concat(Constants.Base64ImgPrefix, _pageRenderer.ConvertBitmapToBase64(Constants.PBIInspectorPNG));
            string template = File.ReadAllText(Constants.TestRunHTMLTemplate);
            string html = template.Replace(Constants.LogoPlaceholder, pbiinspectorlogobase64, StringComparison.OrdinalIgnoreCase);
            html = html.Replace(Constants.VersionPlaceholder, AppUtils.About(), StringComparison.OrdinalIgnoreCase);
            html = html.Replace(Constants.JsonPlaceholder, context.JsonTestRun, StringComparison.OrdinalIgnoreCase);

            var outputHTMLFilePath = Path.Combine(context.LocalOutputDirPath, Constants.TestRunHTMLFileName);

            context.OnMessage(MessageTypeEnum.Information, string.Format("Writing HTML output to file at \"{0}\".", outputHTMLFilePath));
            File.WriteAllText(outputHTMLFilePath, html);

            if (context.IsOneLakeOutput)
            {
                context.OutputArtifacts.Add((outputHTMLFilePath, Constants.TestRunHTMLFileName));
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
