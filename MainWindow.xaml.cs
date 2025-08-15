using System.Windows;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;

namespace SboxCllGui;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    //============================================================
    // Button Clicks
    //============================================================

    private void BtnExtract_Click(object sender, RoutedEventArgs e)
    {
        var cllPath = PickFileToOpen("CLL Files|*.*");
        if (string.IsNullOrEmpty(cllPath)) return;

        var folderPath = PickFolder();
        if (string.IsNullOrEmpty(folderPath)) return;

        try
        {
            var cll = CllCodec.ExtractCll(cllPath, folderPath);
            TxtPackageIdent.Text = cll.PackageIdent;
            TxtCompilerSettings.Text = cll.CompilerSettings;
            TxtProjectReferences.Text = cll.ProjectReferences;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnPack_Click(object sender, RoutedEventArgs e)
    {
        var folderToPack = PickFolder();
        if (string.IsNullOrEmpty(folderToPack)) return;

        var outputCllFile = PickFileToSave("CLL Files|*.cll|All Files|*.*", "my_package.cll");
        if (string.IsNullOrEmpty(outputCllFile)) return;

        try
        {
            CllCodec.PackCll(
                folderToPack,
                outputCllFile,
                TxtPackageIdent.Text,
                TxtCompilerSettings.Text,
                TxtProjectReferences.Text
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    //============================================================
    // Dialogs
    //============================================================

    private string? PickFileToOpen(string filter)
    {
        var dlg = new OpenFileDialog
        {
            Filter = filter,
            FilterIndex = 1
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private string? PickFileToSave(string filter, string defaultFileName)
    {
        var dlg = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private string? PickFolder()
    {
        var dlg = new VistaFolderBrowserDialog
        {
            Description = "Select a folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        return dlg.ShowDialog(this) == true ? dlg.SelectedPath : null;
    }
}