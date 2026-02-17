using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PBIRInspectorClientLibrary;
using PBIRInspectorClientLibrary.Utils;
using PBIRInspectorLibrary;
using FabInspector.Operators;
using Ric.Operators;

internal partial class Program
{
    private static Args _parsedArgs = null;

    private static void Main(string[] args)
    {
#if DEBUG
        Console.WriteLine("Attach debugger to process? Press any key to continue.");
        Console.ReadLine();
#endif
        var serviceProvider = InitServiceProvider();
        var pageRenderer = serviceProvider.GetRequiredService<IReportPageWireframeRenderer>();
        var operatorRegistries = serviceProvider.GetRequiredService<IEnumerable<JsonLogicOperatorRegistry>>();


        try
        {
            _parsedArgs = ArgsUtils.ParseArgs(args);

            Welcome();
            PBIRInspectorClientLibrary.Main.WinMessageIssued += Main_MessageIssued;
            PBIRInspectorClientLibrary.Main.CleanUpRootTempFolder();
            PBIRInspectorClientLibrary.Main.Run(_parsedArgs, pageRenderer, operatorRegistries);
        }
        catch (ArgumentException e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            PBIRInspectorClientLibrary.Main.WinMessageIssued -= Main_MessageIssued;
            Exit();
        }
    }

    private static ServiceProvider InitServiceProvider()
    {
        // 1. Create the service collection.
        var services = new ServiceCollection();

        var registries = new List<JsonLogicOperatorRegistry>();

        registries.Add(new JsonLogicOperatorRegistry(
        new RicSerializerContext(),
        new IJsonLogicOperator[] {
                new CountOperator(),
                new DrillVariableOperator(),
                new FileSizeOperator(),
                new FileTextSearchCountOperator(),
                new IsNullOrEmptyOperator(),
                new PartInfoOperator(),
                new PartOperator(),
                new PathOperator(),
                new QueryOperator(),
                new SetDifferenceOperator(),
                new SetEqualOperator(),
                new SetIntersectionOperator(),
                new SetSymmetricDifferenceOperator(),
                new SetUnionOperator(),
                new StringContainsOperator(),
                new ToRecordOperator(),
                new ToStringOperator(),
                new FromYamlFileOperator()
        }));

        registries.Add(new JsonLogicOperatorRegistry(
        new FabInspectorSerializerContext(),
        new IJsonLogicOperator[] {
                new RectangleOverlapOperator()}));

        services.AddTransient<IEnumerable<JsonLogicOperatorRegistry>>(provider => registries);

        services.AddTransient<IReportPageWireframeRenderer, PBIRInspectorImageLibrary.ReportPageWireframeRenderer>();

        // 3. Build the service provider from the service collection.
        var serviceProvider = services.BuildServiceProvider();

        return serviceProvider;
    }

    private static void Main_MessageIssued(object? sender, PBIRInspectorLibrary.MessageIssuedEventArgs e)
    {
        if (e.MessageType == MessageTypeEnum.Dialog)
        {
            if (_parsedArgs.ADOOutput || _parsedArgs.GITHUBOutput)
            {
                //Running in non-interactive mode on Azure DevOps or GitHub.
                e.DialogOKResponse = true;
            }
            else
            {
                SafeWriteLine(string.Concat(e.Message, " Y/N"));
                var a = Console.ReadLine();
                e.DialogOKResponse = !string.IsNullOrEmpty(a) && a.Equals("Y", StringComparison.OrdinalIgnoreCase);
            }
        }
        else
        {
            //Console and ADO/GitHub outputs
            if ((!_parsedArgs.ADOOutput && !_parsedArgs.GITHUBOutput) || ((_parsedArgs.ADOOutput || _parsedArgs.GITHUBOutput) && (e.MessageType == MessageTypeEnum.Error || e.MessageType == MessageTypeEnum.Warning)))
            {
                SafeWriteLine(FormatConsoleMessage(e.ItemPath, e.MessageType, e.Message));
            }

            //ADO output only
            if (_parsedArgs.ADOOutput && e.MessageType == MessageTypeEnum.Complete)
            {
                string completionStatus = PBIRInspectorClientLibrary.Main.ErrorCount > 0 ? "Failed" : ((PBIRInspectorClientLibrary.Main.WarningCount > 0) ? "SucceededWithIssues" : "Succeeded");

                SafeWriteLine(string.Format(Constants.ADOCompleteTemplate, completionStatus));
            }

            //GitHub output only
            if (_parsedArgs.GITHUBOutput && e.MessageType == MessageTypeEnum.Complete)
            {
                int exitCode = PBIRInspectorClientLibrary.Main.ErrorCount > 0 ? 1 : 0;
                Environment.ExitCode = exitCode;
            }
        }
    }


    private static readonly object consoleLock = new object();

    private static void SafeWriteLine(string message)
    {
        lock (consoleLock)
        {
            Console.WriteLine(message);
        }
    }


    private static String FormatConsoleMessage(string itemPath, MessageTypeEnum messageType, string message)
    {
        string template = _parsedArgs.ADOOutput ? Constants.ADOLogIssueTemplate : _parsedArgs.GITHUBOutput ? Constants.GitHubMsgTemplate : Constants.ConsoleMsgTemplate;
        string msgType = _parsedArgs.ADOOutput || _parsedArgs.GITHUBOutput ? messageType.ToString().ToLower() : messageType.ToString();
        string msgSeparator = _parsedArgs.ADOOutput || _parsedArgs.GITHUBOutput ? "" : ": ";
        string messageTypeFormat = string.Format(template, msgType, itemPath);

        return string.Concat(messageTypeFormat, msgSeparator, message);
    }

    private static void Welcome()
    {
#if !DEBUG
     if (!_parsedArgs.CONSOLEOutput || _parsedArgs.ADOOutput || _parsedArgs.GITHUBOutput) return;
#endif

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(AppUtils.About());
        Console.ResetColor();
    }

    private static void Exit()
    {
        var exitCode = PBIRInspectorClientLibrary.Main.ErrorCount > 0 ? 1 : 0;
        Environment.Exit(exitCode);
    }
}