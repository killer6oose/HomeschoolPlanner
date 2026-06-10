using System.Windows;

namespace HomeschoolPlanner.Dialogs;

public enum WelcomeAction { None, AddStudent, AddSubject, TakeTour }

public partial class WelcomeStartupDialog : Window
{
    public WelcomeAction ChosenAction { get; private set; } = WelcomeAction.None;

    public WelcomeStartupDialog()
    {
        InitializeComponent();
    }

    private void AddStudent_Click(object sender, RoutedEventArgs e)
    {
        ChosenAction = WelcomeAction.AddStudent;
        Close();
    }

    private void AddSubject_Click(object sender, RoutedEventArgs e)
    {
        ChosenAction = WelcomeAction.AddSubject;
        Close();
    }

    private void TakeTour_Click(object sender, RoutedEventArgs e)
    {
        ChosenAction = WelcomeAction.TakeTour;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        ChosenAction = WelcomeAction.None;
        Close();
    }
}
