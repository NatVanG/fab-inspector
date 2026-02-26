using PBIRInspectorClientLibrary.Utils;
using PBIRInspectorLibrary;
using PBIRInspectorLibrary.Exceptions;
using PBIRInspectorLibrary.Output;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace PBIRInspectorClientLibrary
{
    public class Main
    {
        public static event EventHandler<MessageIssuedEventArgs>? WinMessageIssued;
        private static string? _token = null;
        private static Args? _args = null;
        private static int _errorCount = 0;
        private static int _warningCount = 0;

        public static int ErrorCount
        {
            get
            {
                return _errorCount;
            }
        }

        public static void IncrementErrorCount()
        {
            Interlocked.Increment(ref _errorCount);
        }

        public static int WarningCount
        {
            get
            {
                return _warningCount;
            }
        }

        public static void IncrementWarningCount()
        {
            Interlocked.Increment(ref _warningCount);
        }

        public static void Run(string pbiFilePath, string rulesFilePath, string outputPath, bool verbose, bool parallel, bool jsonOutput, bool htmlOutput, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            var formatsString = string.Concat(jsonOutput ? "JSON" : string.Empty, ",", htmlOutput ? "HTML" : string.Empty);
            var verboseString = verbose.ToString();
            var parallelString = parallel.ToString();

            string resolvedPbiFilePath = string.Empty;

            var args = new Args { PBIFilePath = pbiFilePath, RulesFilePath = rulesFilePath, OutputPath = outputPath, FormatsString = formatsString, VerboseString = verboseString, ParallelString = parallelString };

            Run(args, pageRenderer, registries);
        }

        public static InspectionRules DeserialiseRulesFromPath(string rulesPath)
        {
            try
            {
                //TODO: consider validating rules file against schema here to provide more specific error message if rules file is invalid. 

                var inspectionRules = JsonUtils.DeserialiseFromPath<InspectionRules>(rulesPath);

                if (inspectionRules == null || inspectionRules.Rules == null || inspectionRules.Rules.Count == 0)
                {
                    throw new PBIRInspectorException(string.Format("No rule definitions were found within rules file at \"{0}\".", rulesPath));
                }
                else
                {
                    return inspectionRules;
                }
            }
            catch (System.IO.FileNotFoundException e)
            {
                throw new PBIRInspectorException(string.Format("Rules file with path \"{0}\" not found.", rulesPath), e);
            }
            catch (System.Text.Json.JsonException e)
            {
                throw new PBIRInspectorException(string.Format("Could not deserialise rules file with path \"{0}\". Check that the file is valid json and following the correct schema for PBI Inspector rules.", rulesPath), e);
            }
        }

        public static async Task Run(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            // Authenticate based on auth method
            if (args.AuthMethod != "local")
            {
                try
                {
                    OnMessageIssued(MessageTypeEnum.Information, $"Starting {args.AuthMethod} authentication...");

                    switch (args.AuthMethod.ToLower())
                    {
                        case "devicecode":
                            _token = await FabricAuthenticationHelper.AuthenticateWithDeviceCodeAsync(
                                args.ClientId,
                                args.TenantId,
                                message => OnMessageIssued(MessageTypeEnum.Information, message)
                            );
                            break;

                        case "interactive":
                            _token = await FabricAuthenticationHelper.AuthenticateInteractiveAsync(
                                args.ClientId,
                                args.TenantId
                            );
                            break;

                        case "clientsecret":
                            _token = await FabricAuthenticationHelper.AuthenticateWithClientSecretAsync(
                                args.ClientId,
                                args.ClientSecret,
                                args.TenantId
                            );
                            break;

                        default:
                            throw new ArgumentException($"Unsupported authentication method: {args.AuthMethod}");
                    }

                    OnMessageIssued(MessageTypeEnum.Information, "Authentication successful.");
                }
                catch (Exception ex)
                {
                    OnMessageIssued(MessageTypeEnum.Error, $"Authentication failed: {ex.Message}");
                    throw;
                }
            }

            if (!args.Parallel)
            {
                RunSingleThreaded(args, pageRenderer, registries);
            }
            else
            {
                RunParallel(args, pageRenderer, registries);
            }
        }

        public static void RunSingleThreaded(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;
            IEnumerable<TestResult> testResults = null;
            
            OnMessageIssued(MessageTypeEnum.Information, string.Concat("Test run started at (UTC): ", DateTime.Now.ToUniversalTime()));

            var rules = DeserialiseRulesFromPath(Main._args.RulesFilePath);
            testResults = RunSingleThreaded(rules, registries);

            if (testResults != null && testResults.Any())
            {
                OutputResults(testResults, pageRenderer, registries);
            }
            else
            {
                OnMessageIssued(MessageTypeEnum.Information, "No test results found.");
            }
            OnMessageIssued(MessageTypeEnum.Complete, string.Concat("Test run completed at (UTC): ", DateTime.Now.ToUniversalTime()));
        }

        public static void RunParallel(Args args, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            _args = args;
            var rules = DeserialiseRulesFromPath(Main._args.RulesFilePath);
            var ruleBuckets = ChunkInspectionRules(rules);
            var globalResults = new ConcurrentBag<TestResult>();

            OnMessageIssued(MessageTypeEnum.Information, string.Concat("Parallel test run started at (UTC): ", DateTime.Now.ToUniversalTime()));

            Parallel.ForEach(ruleBuckets, _ =>
            {
                var localResults = RunSingleThreaded(_, registries);

                foreach (var result in localResults ?? Enumerable.Empty<TestResult>())
                {
                    globalResults.Add(result);
                }
            });

            OutputResults(globalResults.ToList().OrderBy(_ => _.RuleId), pageRenderer, registries);
            OnMessageIssued(MessageTypeEnum.Complete, string.Concat("Test run completed at (UTC): ", DateTime.Now.ToUniversalTime()));
        }

        private static List<InspectionRules> ChunkInspectionRules(InspectionRules rules)
        {
            var processorCount = Environment.ProcessorCount;
            var allRules = rules.Rules;
            int totalRules = allRules.Count;
            int chunkSize = (int)Math.Ceiling((double)totalRules / processorCount);

            var ruleBuckets = allRules
                .Select((rule, index) => new { rule, index })
                .GroupBy(x => x.index / chunkSize)
                .Select(g => new InspectionRules { Rules = g.Select(x => x.rule).ToList() })
                .ToList();

            return ruleBuckets;
        }

        private static IEnumerable<TestResult>? RunSingleThreaded(InspectionRules rules, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            Inspector? insp = null;

            try
            {
                // Determine which file system to use based on Fabric workspace configuration
                IFileSystem fileSystem;
                if (!string.IsNullOrWhiteSpace(Main._args.FabricWorkspaceId))
                {
                    if (string.IsNullOrWhiteSpace(Main._token))
                    {
                        throw new InvalidOperationException("Authentication token is required for Fabric workspace access.");
                    }
                    
                    // Item-scoped vs workspace-scoped mode
                    fileSystem = string.IsNullOrWhiteSpace(Main._args.PBIFilePath)
                        ? new FabricFileSystem(Main._args.FabricWorkspaceId, Main._token)
                        : new FabricFileSystem(Main._args.FabricWorkspaceId, Main._args.PBIFilePath, Main._token);
                }
                else
                {
                    // Use PhysicalFileSystem with the specified path
                    fileSystem = new PhysicalFileSystem(Main._args.PBIFilePath ?? string.Empty);
                }
                
                insp = new Inspector(rules, registries, fileSystem);

                insp.MessageIssued += Insp_MessageIssued;
                var testResults = insp.Inspect().Where(_ => (!Main._args.Verbose && !_.Pass) || (Main._args.Verbose));
                return testResults;
            }
            catch (PBIRInspectorException e)
            {
                OnMessageIssued(MessageTypeEnum.Error, e.Message);
            }
            catch (ArgumentException e)
            {
                OnMessageIssued(MessageTypeEnum.Error, e.Message);
            }
            catch (Exception e)
            {
                OnMessageIssued(MessageTypeEnum.Error, e.Message);
            }
            finally
            {
                
                if (insp != null)
                {
                    insp.MessageIssued -= Insp_MessageIssued;
                }
            }

            // Ensure all code paths return a value
            return Enumerable.Empty<TestResult>();
        }

        private static void OutputResults(IEnumerable<TestResult> testResults, IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
        {
            string jsonTestRun = string.Empty;
            Inspector? fieldMapInsp = null;
            IEnumerable<TestResult> fieldMapResults = null;

            if (Main._args.CONSOLEOutput || Main._args.ADOOutput || Main._args.GITHUBOutput)
            {
                foreach (var result in testResults)
                {
                    //TODO: use Test log type json property instead
                    var msgType = result.Pass ? MessageTypeEnum.Information : result.LogType;
                    OnMessageIssued(result.ItemPath, msgType, result.Message);
                }

                // Summarise error and warning counts
                if (testResults != null && testResults.Any())
                {
                    OnMessageIssued(MessageTypeEnum.Information, string.Format("Test run summary: {0} errors, {1} warnings.", 
                        testResults.Count(_ => _.LogType == MessageTypeEnum.Error), 
                        testResults.Count(_ => _.LogType == MessageTypeEnum.Warning)));
                }
                else
                {
                    OnMessageIssued(MessageTypeEnum.Information, "Test run summary: No test results found.");
                }
            }

            //Ensure output dir exists
            if (!(Main._args.ADOOutput || Main._args.GITHUBOutput) && (Main._args.JSONOutput || Main._args.HTMLOutput || Main._args.PNGOutput))
            {
                if (!Directory.Exists(Main._args.OutputDirPath))
                {
                    Directory.CreateDirectory(Main._args.OutputDirPath);
                }
            }

            if (!(Main._args.ADOOutput || Main._args.GITHUBOutput) && (Main._args.JSONOutput || Main._args.HTMLOutput))
            {
                var outputFilePath = string.Empty;
                var pbiFileNameWOextension = Path.GetFileNameWithoutExtension(Main._args.PBIFilePath);

                if (!string.IsNullOrEmpty(Main._args.OutputDirPath))
                {
                    outputFilePath = Path.Combine(Main._args.OutputDirPath, string.Concat("TestRun_", pbiFileNameWOextension, ".json"));
                }
                else
                {
                    throw new ArgumentException("Directory with path \"{0}\" does not exist", Main._args.OutputDirPath);
                }

                var testRun = new TestRun() { CompletionTime = DateTime.Now, TestedFilePath = Main._args.PBIFilePath, RulesFilePath = Main._args.RulesFilePath, Verbose = Main._args.Verbose, Results = testResults };
                jsonTestRun = JsonSerializer.Serialize(testRun);
                if (Main._args.JSONOutput)
                {
                    OnMessageIssued(MessageTypeEnum.Information, string.Format("Writing JSON output to file at \"{0}\".", outputFilePath));
                    File.WriteAllText(outputFilePath, jsonTestRun, System.Text.Encoding.UTF8);
                }
            }

            if (!(Main._args.ADOOutput || Main._args.GITHUBOutput) && (Main._args.PNGOutput || Main._args.HTMLOutput))
            {
                // Create file system for field map inspection
                IFileSystem fieldMapFileSystem = new PhysicalFileSystem(Main._args.PBIFilePath ?? string.Empty);
                var fieldMapPathRules = DeserialiseRulesFromPath(Constants.ReportPageFieldMapFilePath);
                fieldMapInsp = new Inspector(fieldMapPathRules, registries, fieldMapFileSystem);

                fieldMapResults = fieldMapInsp.Inspect();

                var outputPNGDirPath = Path.Combine(Main._args.OutputDirPath, Constants.PNGOutputDir);

                if (Directory.Exists(outputPNGDirPath))
                {
                    if (Main._args.OverwriteOutput)
                    {
                        Directory.Delete(outputPNGDirPath, true);
                    }
                    else
                    {
                        //If the directory already exists and overwrite is not set, ask user if they want to delete existing content.
                        var eventArgs = RaiseWinMessage(MessageTypeEnum.Dialog, string.Format("Directory already exists at \"{0}\". Do you want to overwrite existing content?", outputPNGDirPath));
                        if (eventArgs.DialogOKResponse)
                        {
                            Directory.Delete(outputPNGDirPath, true);
                        }
                        else
                        {
                            OnMessageIssued(MessageTypeEnum.Information, "Skipping PNG output as directory already exists and overwrite not set.");
                            return;
                        }
                    }
                }
                Directory.CreateDirectory(outputPNGDirPath);
                OnMessageIssued(MessageTypeEnum.Information, string.Format("Writing report page wireframe images to files at \"{0}\".", outputPNGDirPath));
                pageRenderer.DrawReportPages(fieldMapResults, testResults, outputPNGDirPath);
            }

            if (!(Main._args.ADOOutput || Main._args.GITHUBOutput) && Main._args.HTMLOutput)
            {
                string pbiinspectorlogobase64 = string.Concat(Constants.Base64ImgPrefix, pageRenderer.ConvertBitmapToBase64(Constants.PBIInspectorPNG));
                //string nowireframebase64 = string.Concat(Base64ImgPrefix, ImageUtils.ConvertBitmapToBase64(@"Files\png\nowireframe.png"));
                string template = File.ReadAllText(Constants.TestRunHTMLTemplate);
                string html = template.Replace(Constants.LogoPlaceholder, pbiinspectorlogobase64, StringComparison.OrdinalIgnoreCase);
                html = html.Replace(Constants.VersionPlaceholder, AppUtils.About(), StringComparison.OrdinalIgnoreCase);
                html = html.Replace(Constants.JsonPlaceholder, jsonTestRun, StringComparison.OrdinalIgnoreCase);

                var outputHTMLFilePath = Path.Combine(Main._args.OutputDirPath, Constants.TestRunHTMLFileName);

                OnMessageIssued(MessageTypeEnum.Information, string.Format("Writing HTML output to file at \"{0}\".", outputHTMLFilePath));
                File.WriteAllText(outputHTMLFilePath, html);

                //Results have been written to a temporary directory so show output to user automatically.
                if (Main._args.DeleteOutputDirOnExit && !Main._args.CONSOLEOutput)
                {
                    AppUtils.OpenUrl(outputHTMLFilePath);
                }
            }
        }

        public static void CleanUpTestRunTempFolder()
        {
            if (_args != null && _args.DeleteOutputDirOnExit && Directory.Exists(_args.OutputDirPath))
            {
                Directory.Delete(_args.OutputDirPath, true);
            }
        }

        public static void CleanUpRootTempFolder()
        {
            if (!Directory.Exists(AppUtils.GetTempRootFolderPath()))
            {
                return;
            }

            var tempRootDir = AppUtils.GetTempRootFolderPath();  
            Directory.Delete(tempRootDir, true);
        }

        private static void Insp_MessageIssued(object? sender, MessageIssuedEventArgs e)
        {
            MessageIssued(e);
        }

        private static MessageIssuedEventArgs RaiseWinMessage(MessageTypeEnum messageType, string message)
        {
            var args = new MessageIssuedEventArgs(message, messageType);
            WinMessageIssued?.Invoke(null, args);
            return args;
        }

        private static void OnMessageIssued(MessageTypeEnum messageType, string message)
        {
            var e = new MessageIssuedEventArgs(message, messageType);
            MessageIssued(e);
        }

        private static void OnMessageIssued(string itemPath, MessageTypeEnum messageType, string message)
        {
            var e = new MessageIssuedEventArgs(itemPath, message, messageType);
            MessageIssued(e);
        }

        private static void MessageIssued(MessageIssuedEventArgs e)
        {
            if (_args != null && (_args.ADOOutput || _args.GITHUBOutput))
            {
                if (e.MessageType == MessageTypeEnum.Error) IncrementErrorCount();
                if (e.MessageType == MessageTypeEnum.Warning) IncrementWarningCount();
            }

            EventHandler<MessageIssuedEventArgs>? handler = WinMessageIssued;
            if (handler != null)
            {
                handler(null, e);
            }
        }
    }
}