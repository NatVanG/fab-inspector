using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FabInspector.ClientLibrary;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Operators;
using ModelContextProtocol.Server;
using Ric.Operators;

internal partial class Program
{
    private static Args _parsedArgs = null!;

    private static async Task Main(string[] args)
    {
        // Check for help flag before processing anything else
        if (args.Length > 0 && (args[0].Equals("-help", StringComparison.OrdinalIgnoreCase) || 
            args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) ||
            args[0].Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            ArgsUtils.DisplayHelp();
            return;
        }

        // MCP server mode
        if (args.Length > 0 && args[0].Equals("serve", StringComparison.OrdinalIgnoreCase))
        {
            await StartMcpServer();
            return;
        }

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
            FabInspector.ClientLibrary.Main.WinMessageIssued += Main_MessageIssued;
            FabInspector.ClientLibrary.Main.CleanUpRootTempFolder();
            await FabInspector.ClientLibrary.Main.Run(_parsedArgs, pageRenderer, operatorRegistries);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            FabInspector.ClientLibrary.Main.WinMessageIssued -= Main_MessageIssued;
            Exit();
        }
    }

    private static ServiceProvider InitServiceProvider()
    {
        var services = new ServiceCollection();
        ConfigureSharedServices(services);
        return services.BuildServiceProvider();
    }

    internal static void ConfigureSharedServices(IServiceCollection services)
    {
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
                new FromYamlFileOperator(),
                new KeysOperator(),
                new ValuesOperator(),
                new DistinctOperator(),
                new TypeOfOperator(),
                new HasPropOperator(),
                new StringSplitOperator(),
                new StringJoinOperator(),
                new RegexExtractOperator(),
                new CoalesceOperator(),
                new SliceOperator(),
                new NowOperator(),
                new DateDiffOperator(),
                new LetOperator()
        }));

        registries.Add(new JsonLogicOperatorRegistry(
        new FabInspectorSerializerContext(),
        new IJsonLogicOperator[] {
                new RectangleOverlapOperator(),
                new DaxQueryOperator(),
                new ApiGetOperator(),
                new DfsGetOperator(),
                new ScannerApiOperator()
        }));

        services.AddSingleton<IEnumerable<JsonLogicOperatorRegistry>>(registries);
        services.AddSingleton<IReportPageWireframeRenderer, FabInspector.ImageLibrary.ReportPageWireframeRenderer>();
    }

    private static async Task StartMcpServer()
    {
        // Redirect Console.Out to stderr so inspection log messages
        // don't corrupt the MCP stdio transport (which uses stdout).
        Console.SetOut(Console.Error);

        var builder = Host.CreateApplicationBuilder();
        ConfigureSharedServices(builder.Services);

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "fab-inspector",
                    Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "3.1.0"
                };
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
    }

    private static void Main_MessageIssued(object? sender, FabInspector.Core.MessageIssuedEventArgs e)
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
            if ((!_parsedArgs.ADOOutput && !_parsedArgs.GITHUBOutput) || ((_parsedArgs.ADOOutput || _parsedArgs.GITHUBOutput) && ShouldEmitStructuredMessage(e.MessageType)))
            {
                SafeWriteLine(FormatConsoleMessage(e.ItemPath ?? string.Empty, e.MessageType, e.Message));
            }

            //ADO output only
            if (_parsedArgs.ADOOutput && e.MessageType == MessageTypeEnum.Complete)
            {
                string completionStatus = FabInspector.ClientLibrary.Main.ErrorCount > 0 ? "Failed" : ((FabInspector.ClientLibrary.Main.WarningCount > 0) ? "SucceededWithIssues" : "Succeeded");

                SafeWriteLine(string.Format(Constants.ADOCompleteTemplate, completionStatus));
            }

            //GitHub output only
            if (_parsedArgs.GITHUBOutput && e.MessageType == MessageTypeEnum.Complete)
            {
                int exitCode = FabInspector.ClientLibrary.Main.ErrorCount > 0 ? 1 : 0;
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
        if (_parsedArgs.ADOOutput)
        {
            return FormatAzureDevOpsMessage(itemPath, messageType, message);
        }

        if (_parsedArgs.GITHUBOutput)
        {
            return FormatGitHubMessage(itemPath, messageType, message);
        }

        string messageTypeFormat = string.Format(Constants.ConsoleMsgTemplate, messageType, itemPath);
        return string.Concat(messageTypeFormat, ": ", message);
    }

    private static bool ShouldEmitStructuredMessage(MessageTypeEnum messageType)
    {
        return messageType == MessageTypeEnum.Error
            || messageType == MessageTypeEnum.Warning
            || messageType == MessageTypeEnum.Information;
    }

    private static string FormatAzureDevOpsMessage(string itemPath, MessageTypeEnum messageType, string message)
    {
        if (messageType == MessageTypeEnum.Information)
        {
            return string.Concat(Constants.ADOInformationTemplate, FormatStructuredMessageBody(itemPath, message));
        }

        string msgType = messageType.ToString().ToLowerInvariant();
        string messageTypeFormat = string.Format(Constants.ADOLogIssueTemplate, msgType, itemPath);
        return string.Concat(messageTypeFormat, message);
    }

    private static string FormatGitHubMessage(string itemPath, MessageTypeEnum messageType, string message)
    {
        string msgType = messageType == MessageTypeEnum.Information
            ? "notice"
            : messageType.ToString().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return string.Concat("::", msgType, ":: ", message);
        }

        string messageTypeFormat = string.Format(Constants.GitHubMsgTemplate, msgType, itemPath);
        return string.Concat(messageTypeFormat, message);
    }

    private static string FormatStructuredMessageBody(string itemPath, string message)
    {
        return string.IsNullOrWhiteSpace(itemPath)
            ? message
            : string.Concat(itemPath, ": ", message);
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
        var exitCode = FabInspector.ClientLibrary.Main.ErrorCount > 0 ? 1 : 0;
        Environment.Exit(exitCode);
    }
}
