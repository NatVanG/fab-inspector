using FabInspector.AvaloniaUI;
using FabInspector.Core;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FabInspector.Tests;

[TestFixture]
public class AvaloniaUiServicesTests
{
    [Test]
    public void CreateServiceProvider_UsesCrossPlatformImageRenderer()
    {
        using var services = AppServices.CreateServiceProvider();

        var renderer = services.GetRequiredService<IReportPageWireframeRenderer>();

        Assert.That(renderer, Is.TypeOf<FabInspector.ImageLibrary.ReportPageWireframeRenderer>());
    }

    [Test]
    public void CreateServiceProvider_RegistersOperatorRegistries()
    {
        using var services = AppServices.CreateServiceProvider();

        var registries = services.GetRequiredService<IEnumerable<JsonLogicOperatorRegistry>>().ToList();

        Assert.That(registries, Has.Count.EqualTo(2));
        Assert.That(registries.All(r => r != null), Is.True);
    }
}
