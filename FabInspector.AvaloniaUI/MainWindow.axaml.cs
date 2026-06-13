using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FabInspector.ClientLibrary;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FabInspector.AvaloniaUI;

public partial class MainWindow : Window
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IReportPageWireframeRenderer _pageRenderer;
    private readonly IEnumerable<JsonLogicOperatorRegistry> _registries;
    private bool _syncingRulesInputs;

    public MainWindow()
    {
        InitializeComponent();
        _serviceProvider = AppServices.CreateServiceProvider();
        _pageRenderer = _serviceProvider.GetRequiredService<IReportPageWireframeRenderer>();
        _registries = _serviceProvider.GetRequiredService<IEnumerable<JsonLogicOperatorRegistry>>();
        Title = AppUtils.About();
        Icon = LoadWindowIcon();
        Opened += MainWindow_Opened;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        Main.WinMessageIssued += Main_MessageIssued;
        UseSamplePBIFileStateCheck();
        UseBaseRulesCheck();
        UseTempFilesStateCheck();
        UpdateRulesInputOptions();
        FabricItemTextBox.Focus();
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        Main.WinMessageIssued -= Main_MessageIssued;
        Clear();
        _serviceProvider.Dispose();
    }

    private void Main_MessageIssued(object? sender, MessageIssuedEventArgs e)
    {
        if (e.MessageType == MessageTypeEnum.Dialog)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                e.DialogOKResponse = ShowConfirmationAsync(e.Message).GetAwaiter().GetResult();
                return;
            }

            e.DialogOKResponse = Dispatcher.UIThread
                .InvokeAsync(() => ShowConfirmationAsync(e.Message).GetAwaiter().GetResult())
                .GetAwaiter()
                .GetResult();
            return;
        }

        AppendToTextOutput($"{e.MessageType}: {e.Message}{Environment.NewLine}");
    }

    private Task<bool> ShowConfirmationAsync(string message)
    {
        return DialogService.ShowConfirmationAsync(this, "Delete directory?", message);
    }

    private void AppendToTextOutput(string text)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ConsoleOutputTextBox.Text += text;
            ConsoleOutputTextBox.CaretIndex = ConsoleOutputTextBox.Text?.Length ?? 0;
            return;
        }

        Dispatcher.UIThread.Post(() => AppendToTextOutput(text));
    }

    private async void BrowseFabricItemButton_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Fabric item",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Power BI Project files") { Patterns = new[] { "*.pbip", "*.pbir" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0)
        {
            FabricItemTextBox.Text = ToPath(files[0]);
        }
    }

    private async void BrowseRulesFileButton_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select rules file",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Json files") { Patterns = new[] { "*.json" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0)
        {
            SetRulesFilePath(ToPath(files[0]));
        }
    }

    private async void BrowseRulesCatalogButton_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select rules catalog",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Json files") { Patterns = new[] { "*.json" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0)
        {
            SetRulesCatalogPath(ToPath(files[0]));
        }
    }

    private async void BrowseOutputDirButton_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select output directory",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            OutputDirPathTextBox.Text = ToPath(folders[0]);
        }
    }

    private void UseSamplePBIFileStateCheck()
    {
        var enabled = UseSampleCheckBox.IsChecked != true;
        var samplePBIPPath = AppUtils.ResolveFromExecutableDirectory(Constants.SamplePBIPReportFolderPath);
        FabricItemTextBox.Text = enabled ? FabricItemTextBox.Text : samplePBIPPath;
        if (!enabled)
        {
            FabricItemTextBox.Text = samplePBIPPath;
        }
        else if (FabricItemTextBox.Text == samplePBIPPath)
        {
            FabricItemTextBox.Clear();
        }

        FabricItemTextBox.IsEnabled = enabled;
        BrowseFabricItemButton.IsEnabled = enabled;
    }

    private void UseBaseRulesCheck()
    {
        var enabled = UseBaseRulesCheckBox.IsChecked != true;
        var baseRulesPath = AppUtils.ResolveFromExecutableDirectory(Constants.SampleRulesFilePath);
        if (!enabled)
        {
            SetRulesFilePath(baseRulesPath);
        }
        else if (RulesFilePathTextBox.Text == baseRulesPath)
        {
            RulesFilePathTextBox.Clear();
        }

        RulesFilePathTextBox.IsEnabled = enabled;
        UpdateRulesInputOptions();
    }

    private void UseTempFilesStateCheck()
    {
        var enabled = UseTempFilesCheckBox.IsChecked != true;
        if (!enabled)
        {
            OutputDirPathTextBox.Clear();
        }

        OutputDirPathTextBox.IsEnabled = enabled;
        BrowseOutputDirButton.IsEnabled = enabled;
    }

    private async void RunButton_Click(object? sender, RoutedEventArgs e)
    {
        Clear();
        RunButton.IsEnabled = false;
        SetStatus("Inspection is running...");

        var rulesFilePath = RulesFilePathTextBox.Text?.Trim() ?? string.Empty;
        var rulesCatalogPath = RulesCatalogPathTextBox.Text?.Trim() ?? string.Empty;
        var hasRulesFilePath = !string.IsNullOrWhiteSpace(rulesFilePath);
        var hasRulesCatalogPath = !string.IsNullOrWhiteSpace(rulesCatalogPath);

        if (hasRulesFilePath == hasRulesCatalogPath)
        {
            await DialogService.ShowMessageAsync(this, "Invalid rule source", "Provide either a Rules file path or a Rules catalog path, but not both.");
            SetStatus("Select a single rule source before running.");
            RunButton.IsEnabled = true;
            return;
        }

        try
        {
            await Main.AttendedRun(
                FabricWorkspaceIdTextBox.Text?.Trim() ?? string.Empty,
                FabricItemTextBox.Text?.Trim() ?? string.Empty,
                rulesFilePath,
                rulesCatalogPath,
                OutputDirPathTextBox.Text?.Trim() ?? string.Empty,
                VerboseCheckBox.IsChecked == true,
                ParallelCheckBox.IsChecked == true,
                JsonOutputCheckBox.IsChecked == true,
                HtmlOutputCheckBox.IsChecked == true,
                _pageRenderer,
                _registries);

            SetStatus("Inspection completed.");
        }
        catch (Exception ex)
        {
            AppendToTextOutput($"Error: {ex.Message}{Environment.NewLine}");
            SetStatus("Inspection failed.");
            await DialogService.ShowMessageAsync(this, "Run failed", ex.Message);
        }
        finally
        {
            RunButton.IsEnabled = true;
        }
    }

    private void Clear()
    {
        ConsoleOutputTextBox.Clear();
        Main.CleanUpTestRunTempFolder();
    }

    private async void ReadMeButton_Click(object? sender, RoutedEventArgs e)
    {
        await OpenTargetAsync(Constants.ReadmePageUrl);
    }

    private async void ReportIssueButton_Click(object? sender, RoutedEventArgs e)
    {
        await OpenTargetAsync(Constants.IssuesPageUrl);
    }

    private async void LatestReleaseButton_Click(object? sender, RoutedEventArgs e)
    {
        await OpenTargetAsync(Constants.LatestReleasePageUrl);
    }

    private async void LicenseButton_Click(object? sender, RoutedEventArgs e)
    {
        await OpenTargetAsync(Constants.LicensePageUrl);
    }

    private async void AboutButton_Click(object? sender, RoutedEventArgs e)
    {
        await DialogService.ShowMessageAsync(this, "About", AppUtils.About());
    }

    private async Task OpenTargetAsync(string target)
    {
        try
        {
            AppUtils.OpenUrl(target);
        }
        catch (Exception)
        {
            await DialogService.ShowMessageAsync(this, "Unable to open", "Unable to open the requested link or file.");
        }
    }

    private void UseSampleCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        UseSamplePBIFileStateCheck();
    }

    private void UseBaseRulesCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        UseBaseRulesCheck();
    }

    private void UseTempFilesCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        UseTempFilesStateCheck();
    }

    private void BlankWorkspaceCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        if (BlankWorkspaceCheckBox.IsChecked == true)
        {
            FabricWorkspaceIdTextBox.Clear();
            FabricWorkspaceIdTextBox.IsEnabled = false;
            return;
        }

        FabricWorkspaceIdTextBox.IsEnabled = true;
        FabricWorkspaceIdTextBox.Focus();
    }

    private void RulesFilePathTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingRulesInputs)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(RulesFilePathTextBox.Text) && !string.IsNullOrWhiteSpace(RulesCatalogPathTextBox.Text))
        {
            _syncingRulesInputs = true;
            RulesCatalogPathTextBox.Clear();
            _syncingRulesInputs = false;
        }

        UpdateRulesInputOptions();
    }

    private void RulesCatalogPathTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingRulesInputs)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(RulesCatalogPathTextBox.Text) && !string.IsNullOrWhiteSpace(RulesFilePathTextBox.Text))
        {
            _syncingRulesInputs = true;
            RulesFilePathTextBox.Clear();
            _syncingRulesInputs = false;
        }

        UpdateRulesInputOptions();
    }

    private void SetRulesFilePath(string path)
    {
        _syncingRulesInputs = true;
        RulesFilePathTextBox.Text = path;
        if (!string.IsNullOrWhiteSpace(path))
        {
            RulesCatalogPathTextBox.Clear();
        }
        _syncingRulesInputs = false;
        UpdateRulesInputOptions();
    }

    private void SetRulesCatalogPath(string path)
    {
        _syncingRulesInputs = true;
        RulesCatalogPathTextBox.Text = path;
        if (!string.IsNullOrWhiteSpace(path))
        {
            RulesFilePathTextBox.Clear();
        }
        _syncingRulesInputs = false;
        UpdateRulesInputOptions();
    }

    private void UpdateRulesInputOptions()
    {
        var hasRulesCatalogPath = !string.IsNullOrWhiteSpace(RulesCatalogPathTextBox.Text);
        if (hasRulesCatalogPath && UseBaseRulesCheckBox.IsChecked == true)
        {
            UseBaseRulesCheckBox.IsChecked = false;
        }

        UseBaseRulesCheckBox.IsEnabled = true;
        BrowseRulesFileButton.IsEnabled = RulesFilePathTextBox.IsEnabled;
        BrowseRulesCatalogButton.IsEnabled = true;
    }

    private void SetStatus(string status)
    {
        StatusTextBlock.Text = status;
    }

    private static string ToPath(IStorageItem item)
    {
        return item.TryGetLocalPath() ?? item.Path.AbsolutePath;
    }

    private static WindowIcon? LoadWindowIcon()
    {
        var iconPath = AppUtils.ResolveFromExecutableDirectory(Path.Combine("Assets", "pbiinspector.ico"));
        return File.Exists(iconPath) ? new WindowIcon(iconPath) : null;
    }
}
