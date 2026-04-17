
namespace FabInspector.ClientLibrary.Utils
{
    public class Args
    {
        public string? FabricItem { get; set; }

        public string? RulesFilePath { get; set; }

        public string OutputPath
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    OutputDirPath = value;
                    DeleteOutputDirOnExit = false;
                }
                else
                {
                    OutputDirPath = Path.Combine(AppUtils.GetTempRootFolderPath(), Guid.NewGuid().ToString());
                    DeleteOutputDirOnExit = true;
                }
            }
        }

        public string? OutputDirPath { get; private set; }

        public bool DeleteOutputDirOnExit { get; private set; }

        public string FormatsString
        {
            set
            {
                var formatDelimiters = new char[] { ',', ';', '|' };
                var formats = value.Split(formatDelimiters, StringSplitOptions.RemoveEmptyEntries);
                const string PNG = "PNG";
                const string HTML = "HTML";
                const string JSON = "JSON";
                const string CONSOLE = "Console";
                const string ADO = "ADO";
                const string GITHUB = "GitHub";

                if (formats.Length > 0)
                {
                    PNGOutput = formats.Contains(PNG, StringComparer.OrdinalIgnoreCase);
                    HTMLOutput = formats.Contains(HTML, StringComparer.OrdinalIgnoreCase);
                    JSONOutput = formats.Contains(JSON, StringComparer.OrdinalIgnoreCase);
                    ADOOutput = formats.Contains(ADO, StringComparer.OrdinalIgnoreCase);
                    GITHUBOutput = formats.Contains(GITHUB, StringComparer.OrdinalIgnoreCase);
                }

                CONSOLEOutput = formats.Contains(CONSOLE, StringComparer.OrdinalIgnoreCase) || !(PNGOutput || HTMLOutput || JSONOutput || ADOOutput || GITHUBOutput);
            }
        }

        public bool PNGOutput { get; private set; }

        public bool HTMLOutput { get; private set; }

        public bool JSONOutput { get; private set; }

        public bool ADOOutput { get; private set; }

        public bool GITHUBOutput { get; private set; }

        public bool CONSOLEOutput { get; private set; }

        public string VerboseString
        {
            set
            {
                var verbose = false;
                _ = bool.TryParse(value, out verbose);
                Verbose = verbose;
            }
        }

        public bool Verbose { get; private set; } = false;

        public string ParallelString
        {
            set
            {
                var parallel = false;
                _ = bool.TryParse(value, out parallel);
                Parallel = parallel;
            }
        }

        public bool Parallel { get; private set; } = false;

        public string OverwriteOutputString
        {
            set
            {
                var overwrite = false;
                _ = bool.TryParse(value, out overwrite);
                OverwriteOutput = overwrite;
            }
        }

        public bool OverwriteOutput { get; private set; } = false;

        public string? AuthMethod { get; set; } = "local";
        
        public string? TenantId { get; set; }
        
        public string? ClientId { get; set; }
        
        public string? ClientSecret { get; set; }

        public string? CertificatePath { get; set; }

        public string? CertificatePassword { get; set; }

        public string? FederatedToken { get; set; }
        
        public string? FabricWorkspaceId { get; set; }
    }
}