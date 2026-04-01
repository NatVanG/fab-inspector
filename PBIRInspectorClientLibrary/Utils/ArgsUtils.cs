
namespace PBIRInspectorClientLibrary.Utils
{
    public class ArgsUtils
    {
        public static Args ParseArgs(string[] args)
        {
            const string PBIX = "-pbix", PBIP = "-pbip", PBIPREPORT = "-pbipreport", FABRICITEM = "-fabricitem", FABRICWORKSPACE = "-fabricworkspace", RULES = "-rules", OUTPUT = "-output", FORMATS = "-formats", VERBOSE = "-verbose", PARALLEL = "-parallel", OVERWRITEOUTPUT = "-overwriteoutput", AUTHMETHOD = "-authmethod", TENANTID = "-tenantid", CLIENTID = "-clientid", CLIENTSECRET = "-clientsecret"; 
            const string TRUE = "true";
            const string FALSE = "false";
            string[] validOptions = { PBIX, PBIP, PBIPREPORT, FABRICITEM, FABRICWORKSPACE, RULES, OUTPUT, FORMATS, VERBOSE, PARALLEL, OVERWRITEOUTPUT, AUTHMETHOD, TENANTID, CLIENTID, CLIENTSECRET };

            int index = 0;
            int maxindex = args.Length - 2;
            var dic = new Dictionary<string, string>();
            while (index <= maxindex)
            {
                if (args[index].StartsWith("-") && validOptions.Contains(args[index], StringComparer.OrdinalIgnoreCase))
                {
                    var argName = args[index].ToLower();
                    var argValue = args[index + 1];
                    dic.Add(argName.ToLower(), argValue);
                    index += 2;
                }
                else
                {
                    throw new ArgumentException(string.Format("Invalid command option: \"{0}\".", args[index]));
                }
            }

            if (dic.ContainsKey(PBIX)) { throw new ArgumentNullException("-pbix option is not currently supported use -pbip instead.");  }
            if (!dic.ContainsKey(PBIPREPORT) && !dic.ContainsKey(PBIP) && !dic.ContainsKey(FABRICITEM) && !dic.ContainsKey(FABRICWORKSPACE)) { throw new ArgumentNullException("-pbipreport, -pbip, -fabricitem, or -fabricworkspace must be defined."); }

            if (!dic.ContainsKey(RULES)) { throw new ArgumentNullException("-rules must be defined"); }

            var pbiFilePath = dic.ContainsKey(PBIPREPORT) ? dic[PBIPREPORT] : (dic.ContainsKey(PBIP) ? dic[PBIP] : (dic.ContainsKey(FABRICITEM) ? dic[FABRICITEM] : string.Empty));
            var rulesPath = dic[RULES];
            var outputPath = dic.ContainsKey(OUTPUT) ? dic[OUTPUT] : string.Empty;
            var verboseString = dic.ContainsKey(VERBOSE) ? dic[VERBOSE] : FALSE;
            var parallelString = dic.ContainsKey(PARALLEL) ? dic[PARALLEL] : FALSE;
            var overwriteOutput = dic.ContainsKey(OVERWRITEOUTPUT) ? dic[OVERWRITEOUTPUT] : FALSE;
            var formatsString = dic.ContainsKey(FORMATS) ? dic[FORMATS] : string.Empty;
            
            // Fabric workspace parameter
            var fabricWorkspaceId = dic.ContainsKey(FABRICWORKSPACE) ? dic[FABRICWORKSPACE] : null;
            
            // Authentication parameters - defaults to "local" (no authentication)
            var authMethod = dic.ContainsKey(AUTHMETHOD) ? dic[AUTHMETHOD].ToLower() : "local";
            var tenantId = dic.ContainsKey(TENANTID) ? dic[TENANTID] : Environment.GetEnvironmentVariable("FABRIC_TENANT_ID");
            var clientId = dic.ContainsKey(CLIENTID) ? dic[CLIENTID] : Environment.GetEnvironmentVariable("FABRIC_CLIENT_ID");
            var clientSecret = dic.ContainsKey(CLIENTSECRET) ? dic[CLIENTSECRET] : Environment.GetEnvironmentVariable("FABRIC_CLIENT_SECRET");

            // Validate auth method
            string[] validAuthMethods = { "local", "devicecode", "interactive", "clientsecret" };
            if (!validAuthMethods.Contains(authMethod))
            {
                throw new ArgumentException($"Invalid auth method: '{authMethod}'. Valid options: local, devicecode, interactive, clientsecret");
            }

            // Validate required parameters based on auth method
            if (authMethod != "local")
            {
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    throw new ArgumentException("Client ID is required for remote authentication. Provide via -clientid or FABRIC_CLIENT_ID environment variable.");
                }

                if (authMethod == "clientsecret")
                {
                    if (string.IsNullOrWhiteSpace(tenantId))
                    {
                        throw new ArgumentException("Tenant ID is required for client secret authentication. Provide via -tenantid or FABRIC_TENANT_ID environment variable.");
                    }
                    if (string.IsNullOrWhiteSpace(clientSecret))
                    {
                        throw new ArgumentException("Client secret is required for client secret authentication. Provide via -clientsecret or FABRIC_CLIENT_SECRET environment variable.");
                    }
                }
            }

            if (OneLakeRulesFileDownloader.IsOneLakeDfsUrl(rulesPath) && authMethod == "local")
            {
                throw new ArgumentException("OneLake rules URL requires authentication. Use -authmethod devicecode, interactive, or clientsecret.");
            }

            // Validate incompatible format and auth method combinations
            if ((authMethod == "devicecode" || authMethod == "interactive") && !string.IsNullOrWhiteSpace(formatsString))
            {
                var upperFormats = formatsString.ToUpper();
                if (upperFormats.Contains("ADO") || upperFormats.Contains("GITHUB"))
                {
                    throw new ArgumentException($"{authMethod} authentication is not compatible with ADO or GitHub output formats. These formats are designed for CI/CD pipelines which require non-interactive authentication. Use local (default) or clientsecret authentication method instead.");
                }
            }

            // Validate parallel execution is not enabled with remote auth methods due to potential token caching issues
            if ((authMethod != "local" || string.IsNullOrEmpty(authMethod?.Trim())) && bool.TryParse(parallelString, out bool isParallel) && isParallel)
            {
                throw new ArgumentException("Parallel execution is not supported when using remote authentication methods (devicecode, interactive, clientsecret) due to potential token caching issues. Please set -parallel to false (default) when using remote authentication.");
            }

            // Validate Fabric workspace access requirements
            if (!string.IsNullOrWhiteSpace(fabricWorkspaceId))
            {
                if (authMethod == "local")
                {
                    throw new ArgumentException("Fabric workspace access requires authentication. Use -authmethod devicecode, interactive, or clientsecret.");
                }

                // Validate workspace ID is a GUID
                if (!Guid.TryParse(fabricWorkspaceId, out _))
                {
                    throw new ArgumentException($"Invalid Fabric workspace ID: '{fabricWorkspaceId}'. Must be a valid GUID.");
                }

                // If fabricitem is provided (for item-scoped mode), validate it's a GUID
                if (!string.IsNullOrWhiteSpace(pbiFilePath) && !Guid.TryParse(pbiFilePath, out _))
                {
                    throw new ArgumentException($"When using -fabricworkspace, the -fabricitem value must be a valid GUID (item ID) or omitted for workspace-scoped access. Received: '{pbiFilePath}'");
                }
            }

            return new Args 
            { 
                FabricItem = pbiFilePath, 
                RulesFilePath = rulesPath, 
                OutputPath = outputPath, 
                FormatsString = formatsString, 
                VerboseString = verboseString, 
                ParallelString = parallelString, 
                OverwriteOutputString = overwriteOutput, 
                AuthMethod = authMethod,
                TenantId = tenantId,
                ClientId = clientId,
                ClientSecret = clientSecret,
                FabricWorkspaceId = fabricWorkspaceId
            };
        }
    }
}