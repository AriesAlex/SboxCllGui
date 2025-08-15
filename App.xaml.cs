using System.IO;
using System.Windows;

namespace SboxCllGui;

public partial class App : Application
{
    private void App_Startup(object sender, StartupEventArgs e)
    {
        if (e.Args.Length > 0)
        {
            try
            {
                foreach (var arg in e.Args)
                {
                    var path = arg.Trim('"');
                    if (File.Exists(path))
                    {
                        var outDir = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
                        CllCodec.ExtractCll(path, outDir);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Shutdown();
            return;
        }

        var wnd = new MainWindow();
        MainWindow = wnd;
        wnd.Show();
    }
}