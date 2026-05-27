using System.Windows;
using System.Windows.Controls;

namespace VoicePipe.UI;

public partial class MainWindow : Window
{
    private bool _isDark = true;
    private string _currentLang = "zh-CN";

    private ResourceDictionary? _themeDict;
    private ResourceDictionary? _langDict;
    private LogConsoleWindow?   _consoleWindow;

    public MainWindow()
    {
        InitializeComponent();

        var settings = Core.AppSettings.Load();
        SetLanguage(settings.Language);

        foreach (ComboBoxItem item in LangSelector.Items)
        {
            if (item.Tag.ToString() == settings.Language)
            {
                LangSelector.SelectedItem = item;
                break;
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _consoleWindow?.Close();
        if (DataContext is ViewModels.MainViewModel vm)
            _ = vm.StopPipelineCommand.ExecuteAsync(null);
    }

    private void OpenConsole_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_consoleWindow == null || !_consoleWindow.IsLoaded)
        {
            _consoleWindow = new LogConsoleWindow();
            _consoleWindow.Show();
        }
        else
        {
            _consoleWindow.Activate();
            if (_consoleWindow.WindowState == WindowState.Minimized)
                _consoleWindow.WindowState = WindowState.Normal;
        }
    }

    private void ToggleTheme_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isDark = !_isDark;
        UpdateResourceDictionaries();

        ThemeIcon.Text = _isDark ? "🌙" : "☀";
        string dark = Application.Current.TryFindResource("StrThemeDark") as string ?? "Dark";
        string light = Application.Current.TryFindResource("StrThemeLight") as string ?? "Light";
        ThemeLabel.Text = " " + (_isDark ? dark : light);
    }

    private void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LangSelector.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            string newLang = item.Tag.ToString()!;
            if (newLang != _currentLang)
            {
                SetLanguage(newLang);
                var settings = Core.AppSettings.Load();
                settings.Language = newLang;
                settings.Save();

                // Update theme button label after lang change
                string dark = Application.Current.TryFindResource("StrThemeDark") as string ?? "Dark";
                string light = Application.Current.TryFindResource("StrThemeLight") as string ?? "Light";
                ThemeLabel.Text = " " + (_isDark ? dark : light);
            }
        }
    }

    private void SetLanguage(string langCode)
    {
        _currentLang = langCode;
        UpdateResourceDictionaries();
    }

    private void UpdateResourceDictionaries()
    {
        var merged = Application.Current.Resources.MergedDictionaries;

        var newTheme = new ResourceDictionary
        {
            Source = new Uri(_isDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        };

        var newLang = new ResourceDictionary
        {
            Source = new Uri($"Langs/{_currentLang}.xaml", UriKind.Relative)
        };

        if (_themeDict != null && merged.Contains(_themeDict))
            merged[merged.IndexOf(_themeDict)] = newTheme;
        else
            merged.Add(newTheme);
        _themeDict = newTheme;

        if (_langDict != null && merged.Contains(_langDict))
            merged[merged.IndexOf(_langDict)] = newLang;
        else
            merged.Add(newLang);
        _langDict = newLang;
    }
}