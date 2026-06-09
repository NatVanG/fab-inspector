namespace FabInspector.WinForm
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            groupBox1 = new GroupBox();
            lblInfoRulesCatalog = new Label();
            lblInfoRulesFile = new Label();
            lblInfoFabricItem = new Label();
            lblInfoFabricWorkspace = new Label();
            btnBrowseRulesCatalogFile = new Button();
            txtRulesCatalogPath = new TextBox();
            label6 = new Label();
            txtFabricWorkspaceId = new TextBox();
            label5 = new Label();
            btnBrowseRulesFile = new Button();
            btnBrowsePBIDesktopFile = new Button();
            label2 = new Label();
            label1 = new Label();
            chkUseBaseRules = new CheckBox();
            chckUseSamplePBIFile = new CheckBox();
            txtRulesFilePath = new TextBox();
            txtFabricItem = new TextBox();
            btnBrowseOutputDir = new Button();
            label3 = new Label();
            chckUseTempFiles = new CheckBox();
            txtOutputDirPath = new TextBox();
            groupBox2 = new GroupBox();
            lblInfoOutputOptions = new Label();
            lblInfoOutputFormats = new Label();
            lblInfoOutputDirectory = new Label();
            label7 = new Label();
            chckParallel = new CheckBox();
            chckVerbose = new CheckBox();
            label4 = new Label();
            chckHTMLOutput = new CheckBox();
            chckJsonOutput = new CheckBox();
            txtConsoleOutput = new TextBox();
            btnCopyOutput = new Button();
            btnRun = new Button();
            openPBIDesktopFileDialog = new OpenFileDialog();
            openRulesFileDialog = new OpenFileDialog();
            openRulesCatalogFileDialog = new OpenFileDialog();
            outputFolderBrowserDialog = new FolderBrowserDialog();
            lblMessage = new Label();
            lnkHelp = new LinkLabel();
            lnkLicense = new LinkLabel();
            lnkAbout = new LinkLabel();
            lnkLatestRelease = new LinkLabel();
            lnkReportIssue = new LinkLabel();
            chkBlank = new CheckBox();
            inputToolTip = new ToolTip(components);
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(lblInfoRulesCatalog);
            groupBox1.Controls.Add(lblInfoRulesFile);
            groupBox1.Controls.Add(lblInfoFabricItem);
            groupBox1.Controls.Add(lblInfoFabricWorkspace);
            groupBox1.Controls.Add(btnBrowseRulesCatalogFile);
            groupBox1.Controls.Add(txtRulesCatalogPath);
            groupBox1.Controls.Add(label6);
            groupBox1.Controls.Add(chkBlank);
            groupBox1.Controls.Add(txtFabricWorkspaceId);
            groupBox1.Controls.Add(label5);
            groupBox1.Controls.Add(btnBrowseRulesFile);
            groupBox1.Controls.Add(btnBrowsePBIDesktopFile);
            groupBox1.Controls.Add(label2);
            groupBox1.Controls.Add(label1);
            groupBox1.Controls.Add(chkUseBaseRules);
            groupBox1.Controls.Add(chckUseSamplePBIFile);
            groupBox1.Controls.Add(txtRulesFilePath);
            groupBox1.Controls.Add(txtFabricItem);
            groupBox1.Location = new Point(31, 63);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(933, 295);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Inputs";
            groupBox1.Enter += groupBox1_Enter;
            // 
            // lblInfoRulesCatalog
            // 
            lblInfoRulesCatalog.BorderStyle = BorderStyle.None;
            lblInfoRulesCatalog.Font = new Font("Segoe UI Symbol", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblInfoRulesCatalog.Location = new Point(16, 208);
            lblInfoRulesCatalog.Name = "lblInfoRulesCatalog";
            lblInfoRulesCatalog.Size = new Size(32, 32);
            lblInfoRulesCatalog.TabIndex = 20;
            lblInfoRulesCatalog.Text = "ℹ";
            lblInfoRulesCatalog.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblInfoRulesFile
            // 
            lblInfoRulesFile.BorderStyle = BorderStyle.None;
            lblInfoRulesFile.Font = new Font("Segoe UI Symbol", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblInfoRulesFile.Location = new Point(16, 156);
            lblInfoRulesFile.Name = "lblInfoRulesFile";
            lblInfoRulesFile.Size = new Size(32, 32);
            lblInfoRulesFile.TabIndex = 19;
            lblInfoRulesFile.Text = "ℹ";
            lblInfoRulesFile.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblInfoFabricItem
            // 
            lblInfoFabricItem.BorderStyle = BorderStyle.None;
            lblInfoFabricItem.Font = new Font("Segoe UI Symbol", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblInfoFabricItem.Location = new Point(16, 105);
            lblInfoFabricItem.Name = "lblInfoFabricItem";
            lblInfoFabricItem.Size = new Size(32, 32);
            lblInfoFabricItem.TabIndex = 18;
            lblInfoFabricItem.Text = "ℹ";
            lblInfoFabricItem.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblInfoFabricWorkspace
            // 
            lblInfoFabricWorkspace.BorderStyle = BorderStyle.None;
            lblInfoFabricWorkspace.Font = new Font("Segoe UI Symbol", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblInfoFabricWorkspace.Location = new Point(16, 49);
            lblInfoFabricWorkspace.Name = "lblInfoFabricWorkspace";
            lblInfoFabricWorkspace.Size = new Size(32, 32);
            lblInfoFabricWorkspace.TabIndex = 17;
            lblInfoFabricWorkspace.Text = "ℹ";
            lblInfoFabricWorkspace.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // btnBrowseRulesCatalogFile
            // 
            btnBrowseRulesCatalogFile.Location = new Point(640, 210);
            btnBrowseRulesCatalogFile.Name = "btnBrowseRulesCatalogFile";
            btnBrowseRulesCatalogFile.Size = new Size(112, 34);
            btnBrowseRulesCatalogFile.TabIndex = 16;
            btnBrowseRulesCatalogFile.Text = "Browse";
            btnBrowseRulesCatalogFile.UseVisualStyleBackColor = true;
            btnBrowseRulesCatalogFile.Click += btnBrowseRulesCatalogFile_Click;
            // 
            // txtRulesCatalogPath
            // 
            txtRulesCatalogPath.Location = new Point(204, 210);
            txtRulesCatalogPath.Name = "txtRulesCatalogPath";
            txtRulesCatalogPath.Size = new Size(430, 31);
            txtRulesCatalogPath.TabIndex = 15;
            txtRulesCatalogPath.TextChanged += txtRulesCatalogPath_TextChanged;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(49, 211);
            label6.Name = "label6";
            label6.Size = new Size(108, 25);
            label6.TabIndex = 14;
            label6.Text = "Rules catalog";
            // 
            // txtFabricWorkspaceId
            // 
            txtFabricWorkspaceId.Location = new Point(312, 54);
            txtFabricWorkspaceId.Name = "txtFabricWorkspaceId";
            txtFabricWorkspaceId.Size = new Size(440, 31);
            txtFabricWorkspaceId.TabIndex = 12;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(49, 52);
            label5.Name = "label5";
            label5.Size = new Size(254, 25);
            label5.TabIndex = 11;
            label5.Text = "Fabric Workspace ID (optional)";
            label5.Click += label5_Click;
            // 
            // btnBrowseRulesFile
            // 
            btnBrowseRulesFile.Location = new Point(640, 159);
            btnBrowseRulesFile.Name = "btnBrowseRulesFile";
            btnBrowseRulesFile.Size = new Size(112, 34);
            btnBrowseRulesFile.TabIndex = 10;
            btnBrowseRulesFile.Text = "Browse";
            btnBrowseRulesFile.UseVisualStyleBackColor = true;
            btnBrowseRulesFile.Click += btnBrowseRulesFile_Click;
            // 
            // btnBrowsePBIDesktopFile
            // 
            btnBrowsePBIDesktopFile.Location = new Point(640, 106);
            btnBrowsePBIDesktopFile.Name = "btnBrowsePBIDesktopFile";
            btnBrowsePBIDesktopFile.Size = new Size(112, 34);
            btnBrowsePBIDesktopFile.TabIndex = 9;
            btnBrowsePBIDesktopFile.Text = "Browse";
            btnBrowsePBIDesktopFile.UseVisualStyleBackColor = true;
            btnBrowsePBIDesktopFile.Click += btnBrowsePBIDesktopFile_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(49, 159);
            label2.Name = "label2";
            label2.Size = new Size(82, 25);
            label2.TabIndex = 7;
            label2.Text = "Rules file";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(49, 108);
            label1.Name = "label1";
            label1.Size = new Size(99, 25);
            label1.TabIndex = 6;
            label1.Text = "Fabric Item";
            // 
            // chkUseBaseRules
            // 
            chkUseBaseRules.AutoSize = true;
            chkUseBaseRules.Checked = true;
            chkUseBaseRules.CheckState = CheckState.Checked;
            chkUseBaseRules.Location = new Point(768, 164);
            chkUseBaseRules.Name = "chkUseBaseRules";
            chkUseBaseRules.Size = new Size(151, 29);
            chkUseBaseRules.TabIndex = 4;
            chkUseBaseRules.Text = "Use base rules";
            chkUseBaseRules.UseVisualStyleBackColor = true;
            chkUseBaseRules.CheckedChanged += chkUseBaseRules_CheckedChanged;
            // 
            // chckUseSamplePBIFile
            // 
            chckUseSamplePBIFile.AutoSize = true;
            chckUseSamplePBIFile.Location = new Point(768, 111);
            chckUseSamplePBIFile.Name = "chckUseSamplePBIFile";
            chckUseSamplePBIFile.Size = new Size(129, 29);
            chckUseSamplePBIFile.TabIndex = 3;
            chckUseSamplePBIFile.Text = "Use sample";
            chckUseSamplePBIFile.UseVisualStyleBackColor = true;
            chckUseSamplePBIFile.CheckedChanged += chckUseSamplePBIFile_CheckedChanged;
            // 
            // txtRulesFilePath
            // 
            txtRulesFilePath.Location = new Point(204, 158);
            txtRulesFilePath.Name = "txtRulesFilePath";
            txtRulesFilePath.Size = new Size(430, 31);
            txtRulesFilePath.TabIndex = 1;
            txtRulesFilePath.TextChanged += txtRulesFilePath_TextChanged;
            // 
            // txtFabricItem
            // 
            txtFabricItem.Location = new Point(204, 108);
            txtFabricItem.Name = "txtFabricItem";
            txtFabricItem.Size = new Size(430, 31);
            txtFabricItem.TabIndex = 0;
            txtFabricItem.TextChanged += txtPBIDesktopFile_TextChanged;
            // 
            // btnBrowseOutputDir
            // 
            btnBrowseOutputDir.Location = new Point(640, 35);
            btnBrowseOutputDir.Name = "btnBrowseOutputDir";
            btnBrowseOutputDir.Size = new Size(112, 34);
            btnBrowseOutputDir.TabIndex = 11;
            btnBrowseOutputDir.Text = "Browse";
            btnBrowseOutputDir.UseVisualStyleBackColor = true;
            btnBrowseOutputDir.Click += btnBrowseOutputDir_Click;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(49, 37);
            label3.Name = "label3";
            label3.Size = new Size(144, 25);
            label3.TabIndex = 8;
            label3.Text = "Output directory";
            label3.Click += label3_Click;
            // 
            // chckUseTempFiles
            // 
            chckUseTempFiles.AutoSize = true;
            chckUseTempFiles.Location = new Point(768, 39);
            chckUseTempFiles.Name = "chckUseTempFiles";
            chckUseTempFiles.Size = new Size(150, 29);
            chckUseTempFiles.TabIndex = 5;
            chckUseTempFiles.Text = "Use temp files";
            chckUseTempFiles.UseVisualStyleBackColor = true;
            chckUseTempFiles.CheckedChanged += chckUseTempFiles_CheckedChanged;
            // 
            // txtOutputDirPath
            // 
            txtOutputDirPath.BorderStyle = BorderStyle.FixedSingle;
            txtOutputDirPath.Location = new Point(204, 37);
            txtOutputDirPath.Name = "txtOutputDirPath";
            txtOutputDirPath.Size = new Size(430, 31);
            txtOutputDirPath.TabIndex = 2;
            txtOutputDirPath.TextChanged += txtOutputDirPath_TextChanged;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(lblInfoOutputFormats);
            groupBox2.Controls.Add(lblInfoOutputDirectory);
            groupBox2.Controls.Add(lblInfoOutputOptions);
            groupBox2.Controls.Add(label7);
            groupBox2.Controls.Add(chckParallel);
            groupBox2.Controls.Add(chckVerbose);
            groupBox2.Controls.Add(label4);
            groupBox2.Controls.Add(btnBrowseOutputDir);
            groupBox2.Controls.Add(chckHTMLOutput);
            groupBox2.Controls.Add(chckJsonOutput);
            groupBox2.Controls.Add(txtOutputDirPath);
            groupBox2.Controls.Add(label3);
            groupBox2.Controls.Add(chckUseTempFiles);
            groupBox2.Location = new Point(31, 372);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(933, 183);
            groupBox2.TabIndex = 1;
            groupBox2.TabStop = false;
            groupBox2.Text = "Outputs";
            // 
            // lblInfoOutputOptions
            // 
            lblInfoOutputOptions.BorderStyle = BorderStyle.None;
            lblInfoOutputOptions.Font = new Font("Segoe UI Symbol", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblInfoOutputOptions.Location = new Point(16, 121);
            lblInfoOutputOptions.Name = "lblInfoOutputOptions";
            lblInfoOutputOptions.Size = new Size(32, 32);
            lblInfoOutputOptions.TabIndex = 16;
            lblInfoOutputOptions.Text = "ℹ";
            lblInfoOutputOptions.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblInfoOutputFormats
            // 
            lblInfoOutputFormats.BorderStyle = BorderStyle.None;
            lblInfoOutputFormats.Font = new Font("Segoe UI Symbol", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblInfoOutputFormats.Location = new Point(16, 92);
            lblInfoOutputFormats.Name = "lblInfoOutputFormats";
            lblInfoOutputFormats.Size = new Size(32, 32);
            lblInfoOutputFormats.TabIndex = 15;
            lblInfoOutputFormats.Text = "ℹ";
            lblInfoOutputFormats.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblInfoOutputDirectory
            // 
            lblInfoOutputDirectory.BorderStyle = BorderStyle.None;
            lblInfoOutputDirectory.Font = new Font("Segoe UI Symbol", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblInfoOutputDirectory.Location = new Point(16, 34);
            lblInfoOutputDirectory.Name = "lblInfoOutputDirectory";
            lblInfoOutputDirectory.Size = new Size(32, 32);
            lblInfoOutputDirectory.TabIndex = 14;
            lblInfoOutputDirectory.Text = "ℹ";
            lblInfoOutputDirectory.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(49, 124);
            label7.Name = "label7";
            label7.Size = new Size(124, 25);
            label7.TabIndex = 17;
            label7.Text = "Output options";
            // 
            // chckParallel
            // 
            chckParallel.AutoSize = true;
            chckParallel.Location = new Point(204, 124);
            chckParallel.Name = "chckParallel";
            chckParallel.Size = new Size(104, 29);
            chckParallel.TabIndex = 13;
            chckParallel.Text = "Parallel";
            chckParallel.UseVisualStyleBackColor = true;
            // 
            // chckVerbose
            // 
            chckVerbose.AutoSize = true;
            chckVerbose.Location = new Point(337, 124);
            chckVerbose.Name = "chckVerbose";
            chckVerbose.Size = new Size(102, 29);
            chckVerbose.TabIndex = 2;
            chckVerbose.Text = "Verbose";
            chckVerbose.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(49, 95);
            label4.Name = "label4";
            label4.Size = new Size(136, 25);
            label4.TabIndex = 12;
            label4.Text = "Output formats";
            label4.Click += label4_Click;
            // 
            // chckHTMLOutput
            // 
            chckHTMLOutput.AutoSize = true;
            chckHTMLOutput.Checked = true;
            chckHTMLOutput.CheckState = CheckState.Checked;
            chckHTMLOutput.Location = new Point(337, 94);
            chckHTMLOutput.Name = "chckHTMLOutput";
            chckHTMLOutput.Size = new Size(84, 29);
            chckHTMLOutput.TabIndex = 1;
            chckHTMLOutput.Text = "HTML";
            chckHTMLOutput.UseVisualStyleBackColor = true;
            // 
            // chckJsonOutput
            // 
            chckJsonOutput.AutoSize = true;
            chckJsonOutput.Location = new Point(204, 94);
            chckJsonOutput.Name = "chckJsonOutput";
            chckJsonOutput.Size = new Size(81, 29);
            chckJsonOutput.TabIndex = 0;
            chckJsonOutput.Text = "JSON";
            chckJsonOutput.UseVisualStyleBackColor = true;
            // 
            // txtConsoleOutput
            // 
            txtConsoleOutput.Location = new Point(31, 622);
            txtConsoleOutput.Multiline = true;
            txtConsoleOutput.Name = "txtConsoleOutput";
            txtConsoleOutput.ReadOnly = true;
            txtConsoleOutput.ScrollBars = ScrollBars.Vertical;
            txtConsoleOutput.Size = new Size(933, 112);
            txtConsoleOutput.TabIndex = 2;
            // 
            // btnCopyOutput
            // 
            btnCopyOutput.Location = new Point(31, 582);
            btnCopyOutput.Name = "btnCopyOutput";
            btnCopyOutput.Size = new Size(42, 34);
            btnCopyOutput.TabIndex = 14;
            btnCopyOutput.Text = "C";
            btnCopyOutput.UseVisualStyleBackColor = true;
            btnCopyOutput.Click += btnCopyOutput_Click;
            // 
            // btnRun
            // 
            btnRun.Location = new Point(852, 569);
            btnRun.Name = "btnRun";
            btnRun.Size = new Size(112, 34);
            btnRun.TabIndex = 3;
            btnRun.Text = "Run";
            btnRun.UseVisualStyleBackColor = true;
            btnRun.Click += btnRun_Click;
            // 
            // openPBIDesktopFileDialog
            // 
            openPBIDesktopFileDialog.Filter = "Power BI Project Report file (*.pbip)|*.pbip|All Files (*.*)|*.*";
            openPBIDesktopFileDialog.FileOk += openPBIDesktopFileDialog_FileOk;
            // 
            // openRulesFileDialog
            // 
            openRulesFileDialog.Filter = "Json files (*.json)|*.json|All files (*.*)|*.*";
            openRulesFileDialog.FileOk += openRulesFileDialog_FileOk;
            // 
            // openRulesCatalogFileDialog
            // 
            openRulesCatalogFileDialog.Filter = "Json files (*.json)|*.json|All files (*.*)|*.*";
            openRulesCatalogFileDialog.FileOk += openRulesCatalogFileDialog_FileOk;
            // 
            // outputFolderBrowserDialog
            // 
            outputFolderBrowserDialog.HelpRequest += outputFolderBrowserDialog_HelpRequest;
            // 
            // lblMessage
            // 
            lblMessage.AutoSize = true;
            lblMessage.Location = new Point(747, 527);
            lblMessage.Name = "lblMessage";
            lblMessage.Size = new Size(82, 25);
            lblMessage.TabIndex = 4;
            lblMessage.Text = "Message";
            lblMessage.Visible = false;
            // 
            // lnkHelp
            // 
            lnkHelp.AutoSize = true;
            lnkHelp.Location = new Point(392, 23);
            lnkHelp.Name = "lnkHelp";
            lnkHelp.Size = new Size(81, 25);
            lnkHelp.TabIndex = 5;
            lnkHelp.TabStop = true;
            lnkHelp.Text = "Read me";
            lnkHelp.LinkClicked += lnkHelp_LinkClicked;
            // 
            // lnkLicense
            // 
            lnkLicense.AutoSize = true;
            lnkLicense.Location = new Point(809, 23);
            lnkLicense.Name = "lnkLicense";
            lnkLicense.Size = new Size(68, 25);
            lnkLicense.TabIndex = 6;
            lnkLicense.TabStop = true;
            lnkLicense.Text = "License";
            lnkLicense.LinkClicked += lnkLicense_LinkClicked;
            // 
            // lnkAbout
            // 
            lnkAbout.AutoSize = true;
            lnkAbout.Location = new Point(896, 23);
            lnkAbout.Name = "lnkAbout";
            lnkAbout.Size = new Size(62, 25);
            lnkAbout.TabIndex = 7;
            lnkAbout.TabStop = true;
            lnkAbout.Text = "About";
            lnkAbout.LinkClicked += lnkAbout_LinkClicked;
            // 
            // lnkLatestRelease
            // 
            lnkLatestRelease.AutoSize = true;
            lnkLatestRelease.Location = new Point(666, 23);
            lnkLatestRelease.Name = "lnkLatestRelease";
            lnkLatestRelease.Size = new Size(117, 25);
            lnkLatestRelease.TabIndex = 8;
            lnkLatestRelease.TabStop = true;
            lnkLatestRelease.Text = "Latest release";
            lnkLatestRelease.LinkClicked += lnkLatestRelease_LinkClicked;
            // 
            // lnkReportIssue
            // 
            lnkReportIssue.AutoSize = true;
            lnkReportIssue.Location = new Point(505, 23);
            lnkReportIssue.Name = "lnkReportIssue";
            lnkReportIssue.Size = new Size(133, 25);
            lnkReportIssue.TabIndex = 9;
            lnkReportIssue.TabStop = true;
            lnkReportIssue.Text = "Report an issue";
            lnkReportIssue.LinkClicked += lnkReportIssue_LinkClicked;
            // 
            // chkBlank
            // 
            chkBlank.AutoSize = true;
            chkBlank.Location = new Point(768, 56);
            chkBlank.Name = "chkBlank";
            chkBlank.Size = new Size(80, 29);
            chkBlank.TabIndex = 13;
            chkBlank.Text = "Blank";
            chkBlank.UseVisualStyleBackColor = true;
            chkBlank.CheckedChanged += chkBlank_CheckedChanged;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1002, 757);
            Controls.Add(lnkReportIssue);
            Controls.Add(lnkLatestRelease);
            Controls.Add(lnkAbout);
            Controls.Add(lnkLicense);
            Controls.Add(lnkHelp);
            Controls.Add(lblMessage);
            Controls.Add(btnCopyOutput);
            Controls.Add(btnRun);
            Controls.Add(txtConsoleOutput);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "MainForm";
            Text = "Fab Inspector";
            Load += Form1_Load;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private GroupBox groupBox1;
        private GroupBox groupBox2;
        private CheckBox chckHTMLOutput;
        private CheckBox chckJsonOutput;
        private TextBox txtConsoleOutput;
        private Button btnRun;
        private Label label3;
        private Label label2;
        private Label label1;
        private CheckBox chckUseTempFiles;
        private CheckBox chkUseBaseRules;
        private CheckBox chckUseSamplePBIFile;
        private TextBox txtOutputDirPath;
        private TextBox txtRulesFilePath;
        private TextBox txtFabricItem;
        private Button btnBrowseOutputDir;
        private Button btnBrowseRulesFile;
        private Button btnBrowsePBIDesktopFile;
        private OpenFileDialog openPBIDesktopFileDialog;
        private OpenFileDialog openRulesFileDialog;
        private FolderBrowserDialog outputFolderBrowserDialog;
        private CheckBox chckVerbose;
        private CheckBox chckParallel;
        private Label label4;
        private Label lblMessage;
        private LinkLabel lnkHelp;
        private LinkLabel lnkLicense;
        private LinkLabel lnkAbout;
        private LinkLabel lnkLatestRelease;
        private LinkLabel lnkReportIssue;
        private Label label5;
        private TextBox txtFabricWorkspaceId;
        private CheckBox chkBlank;
        private Label label6;
        private TextBox txtRulesCatalogPath;
        private Button btnBrowseRulesCatalogFile;
        private OpenFileDialog openRulesCatalogFileDialog;
        private ToolTip inputToolTip;
        private Button btnCopyOutput;
        private Label lblInfoFabricWorkspace;
        private Label lblInfoFabricItem;
        private Label lblInfoRulesFile;
        private Label lblInfoRulesCatalog;
        private Label lblInfoOutputDirectory;
        private Label lblInfoOutputFormats;
        private Label lblInfoOutputOptions;
        private Label label7;
    }
}