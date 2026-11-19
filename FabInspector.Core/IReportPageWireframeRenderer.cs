using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FabInspector.Core.Output;

namespace FabInspector.Core
{
    //TODO: move to FabInspector.ClientLibrary
    public interface IReportPageWireframeRenderer
    {
        /// <summary>
        /// Draws the report page wireframe.
        /// </summary>
        void DrawReportPages(IEnumerable<TestResult> fieldMapResults, IEnumerable<TestResult> testResults, string outputDir, string testedFilePath);

        string ConvertBitmapToBase64(string bitmapPath);
    }
}
