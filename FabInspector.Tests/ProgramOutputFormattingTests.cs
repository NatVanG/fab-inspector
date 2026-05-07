using System.Reflection;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;

namespace FabInspector.Tests;

[TestFixture]
public class ProgramOutputFormattingTests
{
    private Type _programType = null!;
    private MethodInfo _mainMessageIssued = null!;
    private FieldInfo _parsedArgsField = null!;
    private TextWriter _originalConsoleOut = null!;

    [SetUp]
    public void SetUp()
    {
        var cliAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(_ => _.GetName().Name == "FabInspector.CLI")
            ?? Assembly.Load("FabInspector.CLI");

        _programType = cliAssembly.GetType("Program")!;
        _mainMessageIssued = _programType.GetMethod("Main_MessageIssued", BindingFlags.NonPublic | BindingFlags.Static)!;
        _parsedArgsField = _programType.GetField("_parsedArgs", BindingFlags.NonPublic | BindingFlags.Static)!;
        _originalConsoleOut = Console.Out;
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetOut(_originalConsoleOut);
    }

    [Test]
    public void MainMessageIssued_ADOInformationMessage_UsesCommandLoggingSyntax()
    {
        _parsedArgsField.SetValue(null, new Args { FormatsString = "ADO" });
        var output = new StringWriter();
        Console.SetOut(output);

        _mainMessageIssued.Invoke(null, new object?[] { null, new MessageIssuedEventArgs("item1", "informational", MessageTypeEnum.Information) });

        Assert.That(output.ToString(), Does.Contain("##[command]item1: informational"));
    }

    [Test]
    public void MainMessageIssued_GitHubInformationMessage_UsesNoticeLoggingSyntax()
    {
        _parsedArgsField.SetValue(null, new Args { FormatsString = "GitHub" });
        var output = new StringWriter();
        Console.SetOut(output);

        _mainMessageIssued.Invoke(null, new object?[] { null, new MessageIssuedEventArgs("item1", "informational", MessageTypeEnum.Information) });

        Assert.That(output.ToString(), Does.Contain("::notice file=item1,line=0,col=0:: informational"));
    }
}
