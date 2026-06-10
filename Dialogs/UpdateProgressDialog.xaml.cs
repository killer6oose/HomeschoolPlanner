using System.Windows;

namespace HomeschoolPlanner.Dialogs;

public partial class UpdateProgressDialog : Window
{
    public UpdateProgressDialog(string version)
    {
        InitializeComponent();
        StatusText.Text = $"Downloading version {version}...";
    }

    public void SetProgress(double percent)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressBar.Value = percent;
            PercentText.Text  = $"{percent:0}%";
        });
    }
}
