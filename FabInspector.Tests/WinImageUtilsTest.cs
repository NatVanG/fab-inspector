using Microsoft.Extensions.DependencyInjection;
using FabInspector.WinImageLibrary;
using FabInspector.Core;
using FabInspector.Core.Output;
using System;

namespace FabInspector.Tests
{
    [TestFixture]
    internal class WinImageUtilsTest
    {
        private ServiceProvider _serviceProvider;
        private IReportPageWireframeRenderer _pageRenderer;

        [OneTimeSetUp]
        public void GlobalSetup()
        {
            var services = new ServiceCollection();
            services.AddTransient<IReportPageWireframeRenderer, ReportPageWireframeRenderer>();
            _serviceProvider = services.BuildServiceProvider();

            _pageRenderer = _serviceProvider.GetRequiredService<IReportPageWireframeRenderer>();
        }

        [OneTimeTearDown]
        public void GlobalTeardown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public void ConvertBitmapToBase64Test()
        {
            var bitmapPath = string.Empty;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => _pageRenderer.ConvertBitmapToBase64(bitmapPath));
        }
        [Test]
        public void DrawReportPagesTest()
        {
            var fieldMapResults = new List<TestResult>();
            var testResults = new List<TestResult>();
            var outputDir = "";
            _pageRenderer.DrawReportPages(fieldMapResults, testResults, outputDir);
            Assert.That(string.IsNullOrEmpty(outputDir));
        }
    }
}
