
namespace FabInspector.ClientLibrary.Utils
{
    public class ArgsUtils
    {
        /// <summary>
        /// Displays help information for all available CLI options.
        /// </summary>
        public static void DisplayHelp()
        {
            var helpText = @"
FabInspector CLI - Power BI / Fabric Inspector Command Line Tool

USAGE:
  fab-inspector -fabricitem <path> -rules <path> [options]
  fab-inspector -fabricitem <path> -rulescatalog <path> [options]
  fab-inspector serve

MCP SERVER MODE:
  serve                           Start as an MCP (Model Context Protocol) server over stdio.
                                  Exposes CLI functionality as MCP tools for AI assistants.
                                  
                                  VS Code / Claude Desktop configuration example:
                                  {
                                    ""mcpServers"": {
                                      ""fab-inspector"": {
                                        ""command"": ""fab-inspector"",
                                        ""args"": [""serve""]
                                      }
                                    }
                                  }

REQUIRED PARAMETERS:
  -fabricitem <path>|<guid>       Path to local folder containing one or more Fabric item definition or, if supplied with -fabricworkspace, Fabric item ID (guid).
                                  Legacy behaviour: path to local Power BI report's .pbip file path or .Report folder path also works but is deprecated.
  
  -rules <path>                   Path to rules file (JSON) or OneLake URL to rules file (latter requires authentication)
  -rulescatalog <path>            Path to rules catalog file (JSON) or OneLake URL to catalog file
                                  Exactly one of -rules or -rulescatalog must be specified
  
ALTERNATIVE INPUT OPTIONS (use one of these):
  -pbipreport <path>              Deprecated: Path to Power BI file
  -pbip <path>                    Path to Power BI Project file
  -fabricworkspace <guid>         Fabric workspace ID (must be a GUID) - requires authentication

OPTIONAL PARAMETERS:
  -output <path>                  Output local directory path or OneLake folder URL (default: local temporary folder)
  -formats <list>                 Output formats separated by comma/semicolon/pipe
                                  Valid: CONSOLE, HTML, JSON, PNG, ADO, GITHUB
                                  (default: CONSOLE)
  -verbose <true|false>           Display all results including passes (default: false)
  -parallel <true|false>          Enable parallel rule processing (default: false)
  -overwriteoutput <true|false>   Overwrite existing output (default: false)

AUTHENTICATION PARAMETERS (use -authmethod):
  -authmethod <method>            Authentication method (default: local)
                                  Valid: local, interactive, azurecli, clientsecret, certificate, 
                                         federatedtoken, managedidentity
  
  LOCAL (default - no authentication):
    No additional parameters needed
  
  INTERACTIVE:
    -clientid <id>                Azure AD application ID (optional)
  
  AZURECLI (developer flow - requires prior 'az login'):
    -tenantid <id>                Azure tenant ID (optional, pins to a specific tenant)
    Uses the Azure CLI token cache. No client ID or secrets needed.
    Run 'az login' before using this method.
  
  CLIENTSECRET:
    -tenantid <id>                Azure tenant ID or name
    -clientid <id>                Azure AD application ID
    -clientsecret <secret>        Azure AD client secret
    (Can also use environment variables: FABRIC_TENANT_ID, FABRIC_CLIENT_ID, FABRIC_CLIENT_SECRET)
  
  CERTIFICATE:
    -tenantid <id>                Azure tenant ID or name
    -clientid <id>                Azure AD application ID
    -certificatepath <path>       Path to certificate file (.pem, .p12)
    -certificatepassword <pwd>    Certificate password (optional)
  
  FEDERATEDTOKEN:
    -tenantid <id>                Azure tenant ID or name
    -clientid <id>                Azure AD application ID
    -federatedtoken <token>       Federated token for authentication
  
  MANAGEDIDENTITY:
    -clientid <id>                Client ID (optional, for user-assigned identity)

EXAMPLES:
  # Local analysis with console output
  fab-inspector -pbip report.pbip -rules rules.json -formats CONSOLE
  
  # Generate HTML output
  fab-inspector -fabricitem report.pbip -rules rules.json -output results -formats HTML

  # Run multiple rulesets from a catalog
  fab-inspector -fabricitem report.pbip -rulescatalog rules-catalog.json -formats JSON
  
  # Analyze Fabric workspace with client secret authentication, output to OneLake in JSON format
  fab-inspector -fabricworkspace a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6 -rules rules.json ^
    -authmethod clientsecret -tenantid tenant-id -clientid app-id -clientsecret app-secret ^
    -output https://myorg.dfs.core.windows.net/results/ -formats JSON

  # Analyze Fabric workspace with client secret authentication, output to Azure DevOps logging commands (as part of a CI/CD pipeline)
  fab-inspector -fabricworkspace a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6 -rules rules.json ^
    -authmethod clientsecret -tenantid tenant-id -clientid app-id -clientsecret app-secret ^
    -formats ADO
  
  # Analyze specific Fabric item in workspace
  fab-inspector -fabricworkspace workspace-guid -fabricitem item-guid -rules rules.json ^
    -authmethod interactive

  # Developer flow: analyze Fabric workspace using Azure CLI credentials (after 'az login')
  fab-inspector -fabricworkspace workspace-guid -rules rules.json ^
    -authmethod azurecli

  # Developer flow with tenant pinning
  fab-inspector -fabricworkspace workspace-guid -rules rules.json ^
    -authmethod azurecli -tenantid my-tenant-id

For more information, visit: https://github.com/NatVanG/fab-inspector
";
            Console.WriteLine(helpText);
        }

        public static Args ParseArgs(string[] args)
        {
            const string PBIX = "-pbix", PBIP = "-pbip", PBIPREPORT = "-pbipreport", FABRICITEM = "-fabricitem", FABRICWORKSPACE = "-fabricworkspace", RULES = "-rules", RULESCATALOG = "-rulescatalog", OUTPUT = "-output", FORMATS = "-formats", VERBOSE = "-verbose", PARALLEL = "-parallel", OVERWRITEOUTPUT = "-overwriteoutput", AUTHMETHOD = "-authmethod", TENANTID = "-tenantid", CLIENTID = "-clientid", CLIENTSECRET = "-clientsecret", CERTIFICATEPATH = "-certificatepath", CERTIFICATEPASSWORD = "-certificatepassword", FEDERATEDTOKEN = "-federatedtoken", HELP = "-help";
            const string FALSE = "false";
            string[] validOptions = { PBIX, PBIP, PBIPREPORT, FABRICITEM, FABRICWORKSPACE, RULES, RULESCATALOG, OUTPUT, FORMATS, VERBOSE, PARALLEL, OVERWRITEOUTPUT, AUTHMETHOD, TENANTID, CLIENTID, CLIENTSECRET, CERTIFICATEPATH, CERTIFICATEPASSWORD, FEDERATEDTOKEN, HELP };

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

            var hasRules = dic.ContainsKey(RULES);
            var hasRulesCatalog = dic.ContainsKey(RULESCATALOG);
            if (hasRules == hasRulesCatalog)
            {
              throw new ArgumentNullException("Exactly one of -rules or -rulescatalog must be defined.");
            }

            var pbiFilePath = dic.ContainsKey(PBIPREPORT) ? dic[PBIPREPORT] : (dic.ContainsKey(PBIP) ? dic[PBIP] : (dic.ContainsKey(FABRICITEM) ? dic[FABRICITEM] : string.Empty));
            var rulesPath = hasRules ? dic[RULES] : string.Empty;
            var rulesCatalogPath = hasRulesCatalog ? dic[RULESCATALOG] : string.Empty;
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
            var certificatePath = dic.ContainsKey(CERTIFICATEPATH) ? dic[CERTIFICATEPATH] : null;
            var certificatePassword = dic.ContainsKey(CERTIFICATEPASSWORD) ? dic[CERTIFICATEPASSWORD] : null;
            var federatedToken = dic.ContainsKey(FEDERATEDTOKEN) ? dic[FEDERATEDTOKEN] : null;

            // Validate auth method
            string[] validAuthMethods = { "local", "interactive", "azurecli", "clientsecret", "certificate", "federatedtoken", "managedidentity" };
            if (!validAuthMethods.Contains(authMethod))
            {
                throw new ArgumentException($"Invalid auth method: '{authMethod}'. Valid options: local, interactive, azurecli, clientsecret, certificate, federatedtoken, managedidentity");
            }

            // Validate required parameters based on auth method
            if (authMethod == "clientsecret" || authMethod == "certificate" || authMethod == "federatedtoken")
            {
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    throw new ArgumentException("Client ID is required for this authentication method. Provide via -clientid or FABRIC_CLIENT_ID environment variable.");
                }
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

            if (authMethod == "certificate")
            {
                if (string.IsNullOrWhiteSpace(tenantId))
                {
                    throw new ArgumentException("Tenant ID is required for certificate authentication. Provide via -tenantid or FABRIC_TENANT_ID environment variable.");
                }
                if (string.IsNullOrWhiteSpace(certificatePath))
                {
                    throw new ArgumentException("Certificate path is required for certificate authentication. Provide via -certificatepath.");
                }
            }

            if (authMethod == "federatedtoken")
            {
                if (string.IsNullOrWhiteSpace(tenantId))
                {
                    throw new ArgumentException("Tenant ID is required for federated token authentication. Provide via -tenantid or FABRIC_TENANT_ID environment variable.");
                }
                if (string.IsNullOrWhiteSpace(federatedToken))
                {
                    throw new ArgumentException("Federated token is required for federated token authentication. Provide via -federatedtoken.");
                }
            }

            if (OneLakeRulesFileDownloader.IsOneLakeDfsUrl(rulesPath) && authMethod == "local")
            {
                throw new ArgumentException("OneLake rules URL requires authentication. Use -authmethod interactive, azurecli, clientsecret, certificate, federatedtoken, or managedidentity.");
            }

            if (RulesCatalogAuthHelper.CatalogContainsEnabledOneLakeRuleSets(rulesCatalogPath) && authMethod == "local")
            {
              throw new ArgumentException("OneLake rules catalog URL requires authentication. Use -authmethod interactive, azurecli, clientsecret, certificate, federatedtoken, or managedidentity.");
            }

            if (OneLakeOutputUploader.IsOneLakeDfsUrl(outputPath) && authMethod == "local")
            {
                throw new ArgumentException("OneLake output URL requires authentication. Use -authmethod interactive, azurecli, clientsecret, certificate, federatedtoken, or managedidentity.");
            }

            // Validate Fabric workspace access requirements
            if (!string.IsNullOrWhiteSpace(fabricWorkspaceId))
            {
                if (authMethod == "local")
                {
                    throw new ArgumentException("Fabric workspace access requires authentication. Use -authmethod interactive, azurecli, clientsecret, certificate, federatedtoken, or managedidentity.");
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
                RulesCatalogPath = rulesCatalogPath,
                OutputPath = outputPath, 
                FormatsString = formatsString, 
                VerboseString = verboseString, 
                ParallelString = parallelString, 
                OverwriteOutputString = overwriteOutput, 
                AuthMethod = authMethod,
                TenantId = tenantId,
                ClientId = clientId,
                ClientSecret = clientSecret,
                CertificatePath = certificatePath,
                CertificatePassword = certificatePassword,
                FederatedToken = federatedToken,
                FabricWorkspaceId = fabricWorkspaceId
            };
        }
    }
}