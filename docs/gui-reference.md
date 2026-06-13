# GUI Reference

`FabInspector.AvaloniaUI` is a cross-platform desktop GUI.

![AvaloniaUI](../DocsImages/FabInspector.AvaloniaUI.png)

1. Specify the Workspace Id to test. Leave blank to test Fabric item definitions on the local file system.
2. If Workspace Id is blank, browse to a local Fabric CI/CD folder containing one or more Fabric CI/CD item definitions or paste the folder path. Alternatively if a Workspace ID is defined, optional scope the rules to a Fabric Item ID
3. Either use the base (Power BI) rules file included in the application or select your own local rules file or a DFS URL to a rules file in OneLake.
4. Use the "Browse" button to select a local output directory to which the results will be written. Another option is to specify a DFS URL to a folder in OneLake storage. Alternatively, select the "Use temp files" check box to write the resuls to a temporary local folder that will be deleted upon exiting the application.
5. Select output formats, either JSON or HTML or both. To simply view the test results in a formatted page select the HTML output.
6. Select "Verbose" to output both test passes and fails, if left unselected then only failed test results will be reported.
7. Select "Run". The test run log messages are displayed at the bottom of the window. If "Use temp files" is selected (or the Output directory field is left blank) along with the HTML output check box, then the browser will open to display the HTML results.
8. Test run information, warnings or errors are displayed in the console output textbox.
