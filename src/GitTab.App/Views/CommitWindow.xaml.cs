using System.Windows;

namespace GitTab.App.Views;

public partial class CommitWindow : Window
{
    public CommitWindow(string repositoryName)
    {
        InitializeComponent();
        RepoNameText.Text = repositoryName;
        Title = repositoryName;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
