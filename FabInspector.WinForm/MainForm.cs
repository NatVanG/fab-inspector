using Microsoft.Extensions.DependencyInjection;
using FabInspector.ClientLibrary;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Operators;
using Ric.Operators;

namespace FabInspector.WinForm
{
    public partial class MainForm : Form
    {
        IReportPageWireframeRenderer? _pageRenderer = null;
        IEnumerable<JsonLogicOperatorRegistry>? _registries = null;
        private bool _syncingRulesInputs;

        public MainForm()
        {
            InitializeComponent();
            this.Text = AppUtils.About();
            this.FormClosing += MainForm_FormClosing;
            var serviceProvider = InitServiceProvider();
            _pageRenderer = serviceProvider.GetRequiredService<IReportPageWireframeRenderer>();
            _registries = serviceProvider.GetRequiredService<IEnumerable<JsonLogicOperatorRegistry>>();
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
                new LetOperator(),
                new RectangleOverlapOperator()
            }));

            registries.Add(new JsonLogicOperatorRegistry(
            new FabInspectorSerializerContext(),
            new IJsonLogicOperator[] {
                new DaxQueryOperator(),
                new SqlQueryOperator(),
                new ApiGetOperator(),
                new DfsGetOperator(),
                new ScannerApiOperator()}));

            services.AddTransient<IEnumerable<JsonLogicOperatorRegistry>>(provider => registries);

            services.AddTransient<IReportPageWireframeRenderer, FabInspector.WinImageLibrary.ReportPageWireframeRenderer>();

            // 3. Build the service provider from the service collection.
            var serviceProvider = services.BuildServiceProvider();

            return serviceProvider;

            //TODO: cleanup on application end
            //using (IHost host = new HostBuilder().Build())
            //{
            //    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

            //    lifetime.ApplicationStarted.Register(() =>
            //    {
            //        Console.WriteLine("Started");
            //    });
            //    lifetime.ApplicationStopping.Register(() =>
            //    {
            //        Console.WriteLine("Stopping firing");
            //        Console.WriteLine("Stopping end");
            //    });
            //    lifetime.ApplicationStopped.Register(() =>
            //    {
            //        Console.WriteLine("Stopped firing");
            //        Console.WriteLine("Stopped end");
            //    });

            //    host.Start();

            //    // Listens for Ctrl+C.
            //    host.WaitForShutdown();
            //}
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            txtConsoleOutput.Clear();
            Main.WinMessageIssued += Main_MessageIssued;
            ConfigureInputTooltips();
            UseSamplePBIFileStateCheck();
            UseBaseRulesCheck();
            UseTempFilesStateCheck();
            UpdateRulesInputOptions();
            txtFabricItem.Focus();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            Clear();
        }

        private void Main_MessageIssued(object? sender, FabInspector.Core.MessageIssuedEventArgs e)
        {
            if (e.MessageType == FabInspector.Core.MessageTypeEnum.Dialog)
            {
                // Must show dialog on the UI thread for proper modality.
                // The event may fire from a thread pool thread after Task.Run offloading.
                if (this.InvokeRequired)
                {
                    this.Invoke(() =>
                    {
                        var dr = MessageBox.Show(this, e.Message, "Delete directory?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (dr == DialogResult.Yes)
                        {
                            e.DialogOKResponse = true;
                        }
                    });
                }
                else
                {
                    var dr = MessageBox.Show(this, e.Message, "Delete directory?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dr == DialogResult.Yes)
                    {
                        e.DialogOKResponse = true;
                    }
                }
            }
            else
            {
                AppendToTextBox(string.Concat(e.MessageType.ToString(), ": ", e.Message, "\r\n"));
            }
        }


        private void AppendToTextBox(string text)
        {
            if (txtConsoleOutput.InvokeRequired)
            {
                txtConsoleOutput.BeginInvoke(new Action<string>(AppendToTextBox), text);
            }
            else
            {
                txtConsoleOutput.AppendText(text);
            }
        }


        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void btnBrowsePBIDesktopFile_Click(object sender, EventArgs e)
        {
            this.openPBIDesktopFileDialog.ShowDialog(this);
        }

        private void btnBrowseRulesFile_Click(object sender, EventArgs e)
        {
            this.openRulesFileDialog.ShowDialog(this);
        }

        private void btnBrowseRulesCatalogFile_Click(object sender, EventArgs e)
        {
            this.openRulesCatalogFileDialog.ShowDialog(this);
        }

        private void btnBrowseOutputDir_Click(object sender, EventArgs e)
        {
            if (this.outputFolderBrowserDialog.ShowDialog(this) == DialogResult.OK)
            {
                this.txtOutputDirPath.Text = this.outputFolderBrowserDialog.SelectedPath;
            }
        }

        private void UseSamplePBIFileStateCheck()
        {
            var enabled = !this.chckUseSamplePBIFile.Checked;
            var samplePBIPPath = AppUtils.ResolveFromExecutableDirectory(Constants.SamplePBIPReportFolderPath);
            if (!enabled) { this.txtFabricItem.Text = samplePBIPPath; } else { this.txtFabricItem.Clear(); }
            ;
            this.txtFabricItem.Enabled = enabled;
            this.btnBrowsePBIDesktopFile.Enabled = enabled;
            //this.chckVerbose.Checked = !enabled;
        }

        private void chckUseSamplePBIFile_CheckedChanged(object sender, EventArgs e)
        {
            UseSamplePBIFileStateCheck();
        }


        private void UseBaseRulesCheck()
        {
            var enabled = !this.chkUseBaseRules.Checked;
            var baseRulesPath = AppUtils.ResolveFromExecutableDirectory(Constants.SampleRulesFilePath);
            if (!enabled) 
            { 
                SetRulesFilePath(baseRulesPath); 
            } 
            else 
            { 
                this.txtRulesFilePath.Clear(); 
            }
            this.txtRulesFilePath.Enabled = enabled;
            UpdateRulesInputOptions();
        }

        private void chkUseBaseRules_CheckedChanged(object sender, EventArgs e)
        {
            UseBaseRulesCheck();
        }

        private void UseTempFilesStateCheck()
        {
            var enabled = !this.chckUseTempFiles.Checked;
            this.txtOutputDirPath.Clear();
            this.txtOutputDirPath.Enabled = enabled;
            this.btnBrowseOutputDir.Enabled = enabled;
        }

        private void chckUseTempFiles_CheckedChanged(object sender, EventArgs e)
        {
            UseTempFilesStateCheck();
        }

        private async void btnRun_Click(object sender, EventArgs e)
        {
            Clear();

            btnRun.Enabled = false;

            var fabricWorskpaceId = this.txtFabricWorkspaceId.Text;
            var fabricItem = this.txtFabricItem.Text;
            var rulesFilePath = this.txtRulesFilePath.Text;
            var rulesCatalogPath = this.txtRulesCatalogPath.Text;
            var outputPath = this.txtOutputDirPath.Text;
            var verbose = this.chckVerbose.Checked;
            var parallel = this.chckParallel.Checked;
            var jsonOutput = this.chckJsonOutput.Checked;
            var htmlOutput = this.chckHTMLOutput.Checked;

            var hasRulesFilePath = !string.IsNullOrWhiteSpace(rulesFilePath);
            var hasRulesCatalogPath = !string.IsNullOrWhiteSpace(rulesCatalogPath);
            if (hasRulesFilePath == hasRulesCatalogPath)
            {
                MessageBox.Show(this, "Provide either a Rules file path or a Rules catalog path, but not both.", "Invalid rule source", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                btnRun.Enabled = true;
                return;
            }

            try
            {
                await Main.AttendedRun(fabricWorskpaceId, fabricItem, rulesFilePath, rulesCatalogPath, outputPath, verbose, parallel, jsonOutput, htmlOutput, _pageRenderer!, _registries!);
            }
            finally
            {
                btnRun.Enabled = true;
            }
        }

        internal void Clear()
        {
            txtConsoleOutput.Clear();
            Main.CleanUpTestRunTempFolder();
        }

        private void openPBIDesktopFileDialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.txtFabricItem.Text = this.openPBIDesktopFileDialog.FileName;
        }

        private void openRulesFileDialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SetRulesFilePath(this.openRulesFileDialog.FileName);
        }

        private void openRulesCatalogFileDialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SetRulesCatalogPath(this.openRulesCatalogFileDialog.FileName);
        }

        private void outputFolderBrowserDialog_HelpRequest(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void txtOutputDirPath_TextChanged(object sender, EventArgs e)
        {
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void lnkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                AppUtils.OpenUrl(Constants.ReadmePageUrl);
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to open link that was clicked.");
            }
        }

        private void lnkLicense_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                AppUtils.OpenUrl(Constants.LicensePageUrl);
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to open link that was clicked.");
            }
        }

        private void lnkAbout_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            MessageBox.Show(AppUtils.About());
        }

        private void lnkLatestRelease_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                AppUtils.OpenUrl(Constants.LatestReleasePageUrl);
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to open link that was clicked.");
            }
        }

        private void lnkReportIssue_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                AppUtils.OpenUrl(Constants.IssuesPageUrl);
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to open link that was clicked.");
            }
        }

        private void txtPBIDesktopFile_TextChanged(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void txtRulesFilePath_TextChanged(object sender, EventArgs e)
        {
            if (_syncingRulesInputs)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(txtRulesFilePath.Text) && !string.IsNullOrWhiteSpace(txtRulesCatalogPath.Text))
            {
                _syncingRulesInputs = true;
                txtRulesCatalogPath.Clear();
                _syncingRulesInputs = false;
            }

            UpdateRulesInputOptions();
        }

        private void txtRulesCatalogPath_TextChanged(object sender, EventArgs e)
        {
            if (_syncingRulesInputs)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(txtRulesCatalogPath.Text) && !string.IsNullOrWhiteSpace(txtRulesFilePath.Text))
            {
                _syncingRulesInputs = true;
                txtRulesFilePath.Clear();
                _syncingRulesInputs = false;
            }

            UpdateRulesInputOptions();
        }

        private void SetRulesFilePath(string path)
        {
            _syncingRulesInputs = true;
            txtRulesFilePath.Text = path;
            if (!string.IsNullOrWhiteSpace(path))
            {
                txtRulesCatalogPath.Clear();
            }
            _syncingRulesInputs = false;
            UpdateRulesInputOptions();
        }

        private void SetRulesCatalogPath(string path)
        {
            _syncingRulesInputs = true;
            txtRulesCatalogPath.Text = path;
            if (!string.IsNullOrWhiteSpace(path))
            {
                txtRulesFilePath.Clear();
            }
            _syncingRulesInputs = false;
            UpdateRulesInputOptions();
        }

        private void UpdateRulesInputOptions()
        {
            var hasRulesCatalogPath = !string.IsNullOrWhiteSpace(txtRulesCatalogPath.Text);

            if (hasRulesCatalogPath && chkUseBaseRules.Checked)
            {
                chkUseBaseRules.Checked = false;
            }

            chkUseBaseRules.Enabled = true;
            btnBrowseRulesFile.Enabled = txtRulesFilePath.Enabled;
            btnBrowseRulesCatalogFile.Enabled = true;
        }

        private void ConfigureInputTooltips()
        {
            var fabricWorkspaceTip = "Optional Fabric workspace GUID. Required for workspace or item GUID scans.";
            var fabricItemTip = "Local Fabric item path or item GUID (with workspace ID). Supports PBIP/.Report folder paths.";
            var rulesFileTip = "Path or OneLake DFS URL to a single rules JSON file.";
            var rulesCatalogTip = "Path or OneLake DFS URL to a rules catalog JSON that references multiple rulesets.";
            var outputDirectoryTip = "Optional local output folder or OneLake DFS destination. Leave blank to use temp files.";
            var outputFormatsTip = "Select one or more result formats to generate, such as JSON and/or HTML.";
            var outputOptionsTip = "Optional run behavior settings that control execution details such as parallelism and verbose logging.";

            inputToolTip.SetToolTip(lblInfoFabricWorkspace, fabricWorkspaceTip);
            inputToolTip.SetToolTip(lblInfoFabricItem, fabricItemTip);
            inputToolTip.SetToolTip(lblInfoRulesFile, rulesFileTip);
            inputToolTip.SetToolTip(lblInfoRulesCatalog, rulesCatalogTip);
            inputToolTip.SetToolTip(lblInfoOutputDirectory, outputDirectoryTip);
            inputToolTip.SetToolTip(lblInfoOutputFormats, outputFormatsTip);
            inputToolTip.SetToolTip(lblInfoOutputOptions, outputOptionsTip);

            inputToolTip.SetToolTip(txtFabricWorkspaceId, fabricWorkspaceTip);
            inputToolTip.SetToolTip(txtFabricItem, fabricItemTip);
            inputToolTip.SetToolTip(txtRulesFilePath, rulesFileTip);
            inputToolTip.SetToolTip(txtRulesCatalogPath, rulesCatalogTip);
            inputToolTip.SetToolTip(txtOutputDirPath, outputDirectoryTip);
            inputToolTip.SetToolTip(chckJsonOutput, outputFormatsTip);
            inputToolTip.SetToolTip(chckHTMLOutput, outputFormatsTip);
            inputToolTip.SetToolTip(chckParallel, outputOptionsTip);
            inputToolTip.SetToolTip(chckVerbose, outputOptionsTip);
            inputToolTip.SetToolTip(btnCopyOutput, "Copy output text to clipboard.");

            btnCopyOutput.Text = char.ConvertFromUtf32(0x1F4CB);
        }

        private void btnCopyOutput_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtConsoleOutput.Text))
            {
                return;
            }

            Clipboard.SetText(txtConsoleOutput.Text);
        }

        private void chkBlank_CheckedChanged(object sender, EventArgs e)
        {
            if (chkBlank.Checked)
            {
                txtFabricWorkspaceId.Clear();
                txtFabricWorkspaceId.Enabled = false;
            }
            else
            {
                txtFabricWorkspaceId.Enabled = true;
                txtFabricWorkspaceId.Focus();
            }
        }
    }
}