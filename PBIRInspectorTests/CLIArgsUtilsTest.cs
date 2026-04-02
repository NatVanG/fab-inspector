using PBIRInspectorClientLibrary.Utils;

#pragma warning disable CS8602 
namespace PBIRInspectorTests
{
    public class CLIArgsUtilsTest
    {
        //[Test]
        //public void TestCLIArgsUtilsSuccess()
        //{
        //    string[] args = "-pbix pbixPath -rules rulesPath -verbose true".Split(" ");
        //    var parsedArgs = ArgsUtils.ParseArgs(args);

        //    Assert.True(parsedArgs.PBIFilePath.Equals("pbixPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.Verbose);
        //}

        //[Test]
        //public void TestCLIArgsUtilsSwappedParamsSuccess()
        //{
        //    string[] args = "-rules rulesPath -pbix pbixPath -verbose true".Split(" ");
        //    var parsedArgs = ArgsUtils.ParseArgs(args);

        //    Assert.True(parsedArgs.PBIFilePath.Equals("pbixPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.Verbose);
        //}

        [Test]
        public void TestCLIArgsUtilsSuccess_FabricItemVerbose()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -verbose true".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("fabricitempath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.Verbose);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_FabricItemOverwriteOutputTrue()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -output outputPath -formats HTML -overwriteoutput true".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("fabricitempath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.HTMLOutput && parsedArgs.OverwriteOutput);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_FabricItemOverwriteOutputFalse()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -output outputPath -formats HTML -overwriteoutput false".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("fabricitempath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.HTMLOutput && !parsedArgs.OverwriteOutput);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_FabricItemOverwriteOutputFalse2()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -output outputPath -formats HTML".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("fabricitempath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.HTMLOutput && !parsedArgs.OverwriteOutput);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_FabricItemOverwriteOutputTrueCaseInvariant()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -output outputPath -formats Html -OverwriteOutput true".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("fabricitempath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.HTMLOutput && parsedArgs.OverwriteOutput);
        }


        [Test]
        public void TestCLIArgsUtilsSuccess_FabricItemOptionVerboseParallel()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -verbose true -parallel true".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("fabricitempath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.Verbose && parsedArgs.Parallel);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_PBIPOption()
        {
            string[] args = "-pbip pbipPath -rules rulesPath -verbose true".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("pbipPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.Verbose);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_PBIPOption2()
        {
            string[] args = "-pbipreport pbipPath -rules rulesPath -verbose true".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("pbipPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.Verbose);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_VerboseOptionMissing()
        {
            string[] args = "-pbipreport pbipPath -rules rulesPath".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("pbipPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && !parsedArgs.Verbose);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_VerboseOptionFalse()
        {
            string[] args = "-pbipreport pbipPath -rules rulesPath -verbose false".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("pbipPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && !parsedArgs.Verbose);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_VerboseOptionUnparseable()
        {
            string[] args = "-pbipreport pbipPath -rules rulesPath -verbose XYZ".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("pbipPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && !parsedArgs.Verbose);
        }

        //[Test]
        //public void TestCLIArgsUtilsSuccess_FavourPBIPReportOption()
        //{
        //    string[] args = "-pbix pbixPath -pbipreport pbipPath -rules rulesPath -verbose true".Split(" ");
        //    var parsedArgs = ArgsUtils.ParseArgs(args);

        //    Assert.True(parsedArgs.PBIFilePath.Equals("pbipPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase));
        //}

        [Test]
        public void TestCLIArgsUtilsRules()
        {
            string[] args = "-pbipreport path -rules rulesPath".Split(" ");
            Args? parsedArgs = null;

            parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void TestCLIArgsUtilsFormats()
        {
            string[] args = "-pbipreport path -rules rulepath -formats CONSOLE,HTML,PNG,JSON".Split(" ");
            Args? parsedArgs = null;

            parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.CONSOLEOutput && parsedArgs.HTMLOutput && parsedArgs.PNGOutput && parsedArgs.JSONOutput);
        }

        [Test]
        public void TestCLIArgsUtilsDefaults()
        {
            string[] args = "-pbipreport path -rules rulespath".Split(" ");
            Args? parsedArgs = null;

            parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.CONSOLEOutput
                && !parsedArgs.Verbose
                && !parsedArgs.Parallel
                && parsedArgs.DeleteOutputDirOnExit
                && !string.IsNullOrEmpty(parsedArgs.OutputDirPath)
                && !parsedArgs.HTMLOutput
                && !parsedArgs.JSONOutput
                && !parsedArgs.PNGOutput);
        }

        [Test]
        public void TestCLIArgsUtilsThrows()
        {
            string[] args = "-pbipreport pbipreportpath".Split(" ");
            Args? parsedArgs = null;

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => parsedArgs = ArgsUtils.ParseArgs(args));
        }

        [Test]
        public void TestCLIArgsUtilsThrows1()
        {
            string[] args = "-pbip pbippath".Split(" ");
            Args? parsedArgs = null;

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => parsedArgs = ArgsUtils.ParseArgs(args));
        }

        [Test]
        public void TestCLIArgsUtilsThrows2()
        {
            string[] args = "-rules rulesPath".Split(" ");
            Args? parsedArgs = null;

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => parsedArgs = ArgsUtils.ParseArgs(args));
        }

        [Test]
        public void TestCLIArgsUtilsThrows3()
        {
            string[] args = "-other other".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
            () => parsedArgs = ArgsUtils.ParseArgs(args));
        }

        [Test]
        public void TestCLIArgsUtilsThrows4()
        {
            string[] args = "-rules -pbix pbixPath -other".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
            () => parsedArgs = ArgsUtils.ParseArgs(args));
        }

        [Test]
        public void TestCLIArgsUtilsThrows5()
        {
            string[] args = "-rules rulesPath -pbip pbipPath -other stuff".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
            () => parsedArgs = ArgsUtils.ParseArgs(args));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_FormatsOption()
        {
            string[] args = "-pbip pbipPath -rules rulesPath -formats CONSOLE,HTML,PNG,JSON -verbose true".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("pbipPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.CONSOLEOutput && parsedArgs.HTMLOutput && parsedArgs.PNGOutput && parsedArgs.JSONOutput && parsedArgs.Verbose);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_FormatsOptionMissing()
        {
            string[] args = "-pbip pbipPath -rules rulesPath -verbose true".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("pbipPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.CONSOLEOutput && !parsedArgs.HTMLOutput && !parsedArgs.PNGOutput && !parsedArgs.JSONOutput && parsedArgs.Verbose);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_FormatsOptionUnparseable()
        {
            string[] args = "-pbip pbipPath -rules rulesPath -formats XYZ -verbose true".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("pbipPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.CONSOLEOutput && !parsedArgs.HTMLOutput && !parsedArgs.PNGOutput && !parsedArgs.JSONOutput && parsedArgs.Verbose);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_VerboseOption()
        {
            string[] args = "-pbip pbipPath -rules rulesPath -verbose true".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("pbipPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.Verbose);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_VerboseOptionFalse2()
        {
            string[] args = "-pbip pbipPath -rules rulesPath -verbose false".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Equals("pbipPath", StringComparison.OrdinalIgnoreCase) && parsedArgs.RulesFilePath.Equals("rulesPath", StringComparison.OrdinalIgnoreCase) && !parsedArgs.Verbose);
        }

        // Authentication Method Tests

        [Test]
        public void TestCLIArgsUtilsSuccess_AuthMethodLocalDefault()
        {
            string[] args = "-pbip pbipPath -rules rulesPath".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("local", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_AuthMethodLocalExplicit()
        {
            string[] args = "-pbip pbipPath -rules rulesPath -authmethod local".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("local", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_AuthMethodDeviceCode()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod devicecode -clientid test-client-id".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("devicecode", StringComparison.OrdinalIgnoreCase) 
                && parsedArgs.ClientId.Equals("test-client-id", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_AuthMethodDeviceCodeWithTenantId()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod devicecode -clientid test-client-id -tenantid test-tenant-id".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("devicecode", StringComparison.OrdinalIgnoreCase) 
                && parsedArgs.ClientId.Equals("test-client-id", StringComparison.OrdinalIgnoreCase)
                && parsedArgs.TenantId.Equals("test-tenant-id", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_AuthMethodInteractive()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod interactive -clientid test-client-id".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("interactive", StringComparison.OrdinalIgnoreCase) 
                && parsedArgs.ClientId.Equals("test-client-id", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_AuthMethodClientSecret()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod clientsecret -tenantid test-tenant-id -clientid test-client-id -clientsecret test-secret".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("clientsecret", StringComparison.OrdinalIgnoreCase) 
                && parsedArgs.TenantId.Equals("test-tenant-id", StringComparison.OrdinalIgnoreCase)
                && parsedArgs.ClientId.Equals("test-client-id", StringComparison.OrdinalIgnoreCase)
                && parsedArgs.ClientSecret.Equals("test-secret", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_AuthMethodCaseInsensitive()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod DeviceCode -clientid test-client-id".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("devicecode", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_InvalidAuthMethod()
        {
            string[] args = "-pbip pbipPath -rules rulesPath -authmethod invalid".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("Invalid auth method"));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_DeviceCodeMissingClientId()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod devicecode".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("Client ID is required"));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_InteractiveMissingClientId()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod interactive".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("Client ID is required"));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_ClientSecretMissingTenantId()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod clientsecret -clientid test-client-id -clientsecret test-secret".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("Tenant ID is required"));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_ClientSecretMissingClientSecret()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod clientsecret -tenantid test-tenant-id -clientid test-client-id".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("Client secret is required"));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_ClientSecretMissingClientId()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod clientsecret -tenantid test-tenant-id -clientsecret test-secret".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("Client ID is required"));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_EnvironmentVariablesFallback()
        {
            // Set environment variables for the test
            Environment.SetEnvironmentVariable("FABRIC_TENANT_ID", "env-tenant-id");
            Environment.SetEnvironmentVariable("FABRIC_CLIENT_ID", "env-client-id");
            Environment.SetEnvironmentVariable("FABRIC_CLIENT_SECRET", "env-client-secret");

            try
            {
                string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod clientsecret".Split(" ");
                var parsedArgs = ArgsUtils.ParseArgs(args);

                Assert.That(parsedArgs.TenantId.Equals("env-tenant-id", StringComparison.OrdinalIgnoreCase)
                    && parsedArgs.ClientId.Equals("env-client-id", StringComparison.OrdinalIgnoreCase)
                    && parsedArgs.ClientSecret.Equals("env-client-secret", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                // Clean up environment variables
                Environment.SetEnvironmentVariable("FABRIC_TENANT_ID", null);
                Environment.SetEnvironmentVariable("FABRIC_CLIENT_ID", null);
                Environment.SetEnvironmentVariable("FABRIC_CLIENT_SECRET", null);
            }
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_ArgumentsOverrideEnvironmentVariables()
        {
            // Set environment variables for the test
            Environment.SetEnvironmentVariable("FABRIC_TENANT_ID", "env-tenant-id");
            Environment.SetEnvironmentVariable("FABRIC_CLIENT_ID", "env-client-id");
            Environment.SetEnvironmentVariable("FABRIC_CLIENT_SECRET", "env-client-secret");

            try
            {
                string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod clientsecret -tenantid arg-tenant-id -clientid arg-client-id -clientsecret arg-secret".Split(" ");
                var parsedArgs = ArgsUtils.ParseArgs(args);

                Assert.That(parsedArgs.TenantId.Equals("arg-tenant-id", StringComparison.OrdinalIgnoreCase)
                    && parsedArgs.ClientId.Equals("arg-client-id", StringComparison.OrdinalIgnoreCase)
                    && parsedArgs.ClientSecret.Equals("arg-secret", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                // Clean up environment variables
                Environment.SetEnvironmentVariable("FABRIC_TENANT_ID", null);
                Environment.SetEnvironmentVariable("FABRIC_CLIENT_ID", null);
                Environment.SetEnvironmentVariable("FABRIC_CLIENT_SECRET", null);
            }
        }


        [Test]
        public void TestCLIArgsUtilsThrows_DeviceCodeWithADOFormat()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod devicecode -clientid test-client-id -formats ADO".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("authentication is not compatible with ADO or GitHub"));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_DeviceCodeWithGitHubFormat()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod devicecode -clientid test-client-id -formats GitHub".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("authentication is not compatible with ADO or GitHub"));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_DeviceCodeWithADOAndGitHubFormats()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod devicecode -clientid test-client-id -formats ADO,GitHub".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("authentication is not compatible with ADO or GitHub"));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_DeviceCodeWithMixedFormatsIncludingADO()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod devicecode -clientid test-client-id -formats HTML,ADO,JSON".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("authentication is not compatible with ADO or GitHub"));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_DeviceCodeWithAllowedFormats()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod devicecode -clientid test-client-id -formats HTML,JSON,PNG,CONSOLE".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("devicecode", StringComparison.OrdinalIgnoreCase) 
                && parsedArgs.ClientId.Equals("test-client-id", StringComparison.OrdinalIgnoreCase)
                && parsedArgs.HTMLOutput 
                && parsedArgs.JSONOutput 
                && parsedArgs.PNGOutput 
                && parsedArgs.CONSOLEOutput);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_ClientSecretWithADOFormat()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod clientsecret -tenantid test-tenant-id -clientid test-client-id -clientsecret test-secret -formats ADO".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("clientsecret", StringComparison.OrdinalIgnoreCase) 
                && parsedArgs.ADOOutput);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_ClientSecretWithGitHubFormat()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod clientsecret -tenantid test-tenant-id -clientid test-client-id -clientsecret test-secret -formats GitHub".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("clientsecret", StringComparison.OrdinalIgnoreCase) 
                && parsedArgs.GITHUBOutput);
        }

        [Test]
        public void TestCLIArgsUtilsThrows_InteractiveWithADOFormat()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod interactive -clientid test-client-id -formats ADO".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("interactive authentication is not compatible with ADO or GitHub"));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_InteractiveWithGitHubFormat()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod interactive -clientid test-client-id -formats GitHub".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("interactive authentication is not compatible with ADO or GitHub"));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_InteractiveWithMixedFormatsIncludingADO()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod interactive -clientid test-client-id -formats HTML,ADO,JSON".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("interactive authentication is not compatible with ADO or GitHub"));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_InteractiveWithAllowedFormats()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod interactive -clientid test-client-id -formats HTML,JSON,PNG,CONSOLE".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("interactive", StringComparison.OrdinalIgnoreCase) 
                && parsedArgs.HTMLOutput 
                && parsedArgs.JSONOutput 
                && parsedArgs.PNGOutput 
                && parsedArgs.CONSOLEOutput);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_FabricWorkspaceScoped_DeviceCode()
        {
            string workspaceId = Guid.NewGuid().ToString();
            string[] args = $"-fabricworkspace {workspaceId} -rules rulesPath -authmethod devicecode -clientid testclientid".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricWorkspaceId == workspaceId 
                && parsedArgs.AuthMethod.Equals("devicecode", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(parsedArgs.FabricItem));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_FabricItemScoped_Interactive()
        {
            string workspaceId = Guid.NewGuid().ToString();
            string itemId = Guid.NewGuid().ToString();
            string[] args = $"-fabricworkspace {workspaceId} -fabricitem {itemId} -rules rulesPath -authmethod interactive -clientid testclientid".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricWorkspaceId == workspaceId 
                && parsedArgs.FabricItem == itemId
                && parsedArgs.AuthMethod.Equals("interactive", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_FabricItemScoped_ClientSecret()
        {
            string workspaceId = Guid.NewGuid().ToString();
            string itemId = Guid.NewGuid().ToString();
            string[] args = $"-fabricworkspace {workspaceId} -fabricitem {itemId} -rules rulesPath -authmethod clientsecret -clientid testclient -tenantid testtenant -clientsecret testsecret".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricWorkspaceId == workspaceId 
                && parsedArgs.FabricItem == itemId
                && parsedArgs.AuthMethod.Equals("clientsecret", StringComparison.OrdinalIgnoreCase)
                && parsedArgs.ClientId.Equals("testclient")
                && parsedArgs.TenantId.Equals("testtenant")
                && parsedArgs.ClientSecret.Equals("testsecret"));
        }

        [Test]
        public void TestCLIArgsUtilsError_FabricWorkspace_LocalAuth()
        {
            string workspaceId = Guid.NewGuid().ToString();
            string[] args = $"-fabricworkspace {workspaceId} -rules rulesPath".Split(" ");

            var ex = Assert.Throws<ArgumentException>(() => ArgsUtils.ParseArgs(args));
            Assert.That(ex.Message.Contains("Fabric workspace access requires authentication"));
        }

        [Test]
        public void TestCLIArgsUtilsError_FabricWorkspace_InvalidGuid()
        {
            string[] args = "-fabricworkspace not-a-guid -rules rulesPath -authmethod devicecode -clientid testclientid".Split(" ");

            var ex = Assert.Throws<ArgumentException>(() => ArgsUtils.ParseArgs(args));
            Assert.That(ex.Message.Contains("Invalid Fabric workspace ID") && ex.Message.Contains("Must be a valid GUID"));
        }

        [Test]
        public void TestCLIArgsUtilsError_FabricWorkspace_ItemNotGuid()
        {
            string workspaceId = Guid.NewGuid().ToString();
            string[] args = $"-fabricworkspace {workspaceId} -fabricitem C:\\path\\to\\folder -rules rulesPath -authmethod devicecode -clientid testclientid".Split(" ");

            var ex = Assert.Throws<ArgumentException>(() => ArgsUtils.ParseArgs(args));
            Assert.That(ex.Message.Contains("must be a valid GUID"));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_LocalMode_BackwardsCompatibility()
        {
            // Ensure existing local mode behavior still works
            string[] args = "-fabricitem C:\\path\\to\\folder -rules rulesPath".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.FabricItem.Contains("C:\\path\\to\\folder") 
                && parsedArgs.AuthMethod.Equals("local")
                && string.IsNullOrEmpty(parsedArgs.FabricWorkspaceId));
        }

        // Parallel execution with remote auth validation tests (lines 90-94 of ArgsUtils.cs)

        [Test]
        public void TestCLIArgsUtilsThrows_ParallelWithDeviceCodeAuth()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod devicecode -clientid test-client-id -parallel true".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("Parallel execution is not supported when using remote authentication methods"));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_ParallelWithInteractiveAuth()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod interactive -clientid test-client-id -parallel true".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("Parallel execution is not supported when using remote authentication methods"));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_ParallelWithClientSecretAuth()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod clientsecret -tenantid test-tenant-id -clientid test-client-id -clientsecret test-secret -parallel true".Split(" ");
            Args? parsedArgs = null;

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => parsedArgs = ArgsUtils.ParseArgs(args));
            
            Assert.That(ex.Message.Contains("Parallel execution is not supported when using remote authentication methods"));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_ParallelWithLocalAuth()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -parallel true".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("local", StringComparison.OrdinalIgnoreCase) 
                && parsedArgs.Parallel);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_ParallelWithLocalAuthExplicit()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod local -parallel true".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("local", StringComparison.OrdinalIgnoreCase) 
                && parsedArgs.Parallel);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_NoParallelWithDeviceCodeAuth()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod devicecode -clientid test-client-id".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("devicecode", StringComparison.OrdinalIgnoreCase) 
                && !parsedArgs.Parallel);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_NoParallelWithInteractiveAuth()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod interactive -clientid test-client-id".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("interactive", StringComparison.OrdinalIgnoreCase) 
                && !parsedArgs.Parallel);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_NoParallelWithClientSecretAuth()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod clientsecret -tenantid test-tenant-id -clientid test-client-id -clientsecret test-secret".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("clientsecret", StringComparison.OrdinalIgnoreCase) 
                && !parsedArgs.Parallel);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_ParallelFalseWithDeviceCodeAuth()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod devicecode -clientid test-client-id -parallel false".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("devicecode", StringComparison.OrdinalIgnoreCase) 
                && !parsedArgs.Parallel);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_ParallelFalseWithInteractiveAuth()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod interactive -clientid test-client-id -parallel false".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("interactive", StringComparison.OrdinalIgnoreCase) 
                && !parsedArgs.Parallel);
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_ParallelFalseWithClientSecretAuth()
        {
            string[] args = "-fabricitem fabricitempath -rules rulesPath -authmethod clientsecret -tenantid test-tenant-id -clientid test-client-id -clientsecret test-secret -parallel false".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.AuthMethod.Equals("clientsecret", StringComparison.OrdinalIgnoreCase) 
                && !parsedArgs.Parallel);
        }

        [Test]
        public void TestCLIArgsUtilsThrows_OneLakeRulesUrlWithLocalAuth()
        {
            string oneLakeRulesUrl = "https://onelake.dfs.fabric.microsoft.com/MyWorkspace/MyLakehouse.Lakehouse/Files/rules.json";
            string[] args = $"-fabricitem fabricitempath -rules {oneLakeRulesUrl}".Split(" ");

            var ex = Assert.Throws<ArgumentException>(() => ArgsUtils.ParseArgs(args));
            Assert.That(ex.Message.Contains("OneLake rules URL requires authentication"));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_OneLakeRulesUrlWithDeviceCodeAuth()
        {
            string oneLakeRulesUrl = "https://onelake.dfs.fabric.microsoft.com/MyWorkspace/MyLakehouse.Lakehouse/Files/rules.json";
            string[] args = $"-fabricitem fabricitempath -rules {oneLakeRulesUrl} -authmethod devicecode -clientid test-client-id".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.RulesFilePath.Equals(oneLakeRulesUrl, StringComparison.OrdinalIgnoreCase)
                && parsedArgs.AuthMethod.Equals("devicecode", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_OneLakeRulesUrlWithClientSecretAuth()
        {
            string oneLakeRulesUrl = "https://onelake.dfs.fabric.microsoft.com/MyWorkspace/MyLakehouse.Lakehouse/Files/rules.json";
            string[] args = $"-fabricitem fabricitempath -rules {oneLakeRulesUrl} -authmethod clientsecret -tenantid test-tenant-id -clientid test-client-id -clientsecret test-secret".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.RulesFilePath.Equals(oneLakeRulesUrl, StringComparison.OrdinalIgnoreCase)
                && parsedArgs.AuthMethod.Equals("clientsecret", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void TestCLIArgsUtilsThrows_OneLakeOutputUrlWithLocalAuth()
        {
            string oneLakeOutputUrl = "https://onelake.dfs.fabric.microsoft.com/MyWorkspace/MyLakehouse.Lakehouse/Files/output";
            string[] args = $"-fabricitem fabricitempath -rules rulesPath -output {oneLakeOutputUrl}".Split(" ");

            var ex = Assert.Throws<ArgumentException>(() => ArgsUtils.ParseArgs(args));
            Assert.That(ex.Message.Contains("OneLake output URL requires authentication"));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_OneLakeOutputUrlWithDeviceCodeAuth()
        {
            string oneLakeOutputUrl = "https://onelake.dfs.fabric.microsoft.com/MyWorkspace/MyLakehouse.Lakehouse/Files/output";
            string[] args = $"-fabricitem fabricitempath -rules rulesPath -output {oneLakeOutputUrl} -authmethod devicecode -clientid test-client-id".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.OutputDirPath.Equals(oneLakeOutputUrl, StringComparison.OrdinalIgnoreCase)
                && parsedArgs.AuthMethod.Equals("devicecode", StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void TestCLIArgsUtilsSuccess_OneLakeOutputUrlWithClientSecretAuth()
        {
            string oneLakeOutputUrl = "https://onelake.dfs.fabric.microsoft.com/MyWorkspace/MyLakehouse.Lakehouse/Files/output";
            string[] args = $"-fabricitem fabricitempath -rules rulesPath -output {oneLakeOutputUrl} -authmethod clientsecret -tenantid test-tenant-id -clientid test-client-id -clientsecret test-secret".Split(" ");
            var parsedArgs = ArgsUtils.ParseArgs(args);

            Assert.That(parsedArgs.OutputDirPath.Equals(oneLakeOutputUrl, StringComparison.OrdinalIgnoreCase)
                && parsedArgs.AuthMethod.Equals("clientsecret", StringComparison.OrdinalIgnoreCase));
        }


    }
}

#pragma warning restore CS8602