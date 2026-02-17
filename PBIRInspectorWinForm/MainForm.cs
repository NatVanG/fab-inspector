using Microsoft.Extensions.DependencyInjection;
using PBIRInspectorClientLibrary;
using PBIRInspectorClientLibrary.Utils;
using PBIRInspectorLibrary;
using FabInspector.Operators;
using Ric.Operators;

namespace PBIRInspectorWinForm
{
    public partial class MainForm : Form
    {
        IReportPageWireframeRenderer _pageRenderer = null;
        IEnumerable<JsonLogicOperatorRegistry> _registries = null;

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
                new FromYamlFileOperator()
            }));

            registries.Add(new JsonLogicOperatorRegistry(
            new FabInspectorSerializerContext(),
            new IJsonLogicOperator[] {
                new RectangleOverlapOperator()}));

            services.AddTransient<IEnumerable<JsonLogicOperatorRegistry>>(provider => registries);

            services.AddTransient<IReportPageWireframeRenderer, PBIRInspectorWinImageLibrary.ReportPageWireframeRenderer>();

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
            UseSamplePBIFileStateCheck();
            UseBaseRulesCheck();
            UseTempFilesStateCheck();
            txtPBIDesktopFile.Focus();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            Clear();
        }

        private void Main_MessageIssued(object? sender, PBIRInspectorLibrary.MessageIssuedEventArgs e)
        {
            if (e.MessageType == PBIRInspectorLibrary.MessageTypeEnum.Dialog)
            {
                var dr = MessageBox.Show(e.Message, "Delete directory?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.Yes)
                {
                    e.DialogOKResponse = true;
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
            if (!enabled) { this.txtPBIDesktopFile.Text = Constants.SamplePBIPReportFolderPath; } else { this.txtPBIDesktopFile.Clear(); };
            this.txtPBIDesktopFile.Enabled = enabled;
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
            if (!enabled) { this.txtRulesFilePath.Text = Constants.SampleRulesFilePath; } else { this.txtRulesFilePath.Clear(); }
            this.txtRulesFilePath.Enabled = enabled;
            this.btnBrowseRulesFile.Enabled = enabled;
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

        private void btnRun_Click(object sender, EventArgs e)
        {
            Clear();

            btnRun.Enabled = false;

            var pbiFilePath = this.txtPBIDesktopFile.Text;
            var rulesFilePath = this.txtRulesFilePath.Text;
            var outputPath = this.txtOutputDirPath.Text;
            var verbose = this.chckVerbose.Checked;
            var parallel = false; //todo: implement parallel processing option
            var jsonOutput = this.chckJsonOutput.Checked;
            var htmlOutput = this.chckHTMLOutput.Checked;

            Main.Run(pbiFilePath, rulesFilePath, outputPath, verbose, parallel, jsonOutput, htmlOutput, _pageRenderer, _registries);

            btnRun.Enabled = true;
        }

        internal void Clear()
        {
            txtConsoleOutput.Clear();
            Main.CleanUpTestRunTempFolder();
        }

        private void openPBIDesktopFileDialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.txtPBIDesktopFile.Text = this.openPBIDesktopFileDialog.FileName;
        }

        private void openRulesFileDialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.txtRulesFilePath.Text = this.openRulesFileDialog.FileName;
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
            catch (Exception ex)
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
            catch (Exception ex)
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
            catch (Exception ex)
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
            catch (Exception ex)
            {
                MessageBox.Show("Unable to open link that was clicked.");
            }
        }
    }
}