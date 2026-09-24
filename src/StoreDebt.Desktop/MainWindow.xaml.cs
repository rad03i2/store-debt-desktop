using System.Windows;
using System.Windows.Input;
using StoreDebt.Desktop.Data;
using StoreDebt.Desktop.Services;
using StoreDebt.Desktop.ViewModels;

namespace StoreDebt.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        var statementService = new StatementService();

        _viewModel = new MainViewModel(
            new StoreDatabase(),
            new DialogService(),
            new DebtSpeechService(),
            statementService,
            new WhatsAppService(statementService));

        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
        _viewModel.PrepareSpeech();
        SearchBox.Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            if (_viewModel.IsActivityView)
            {
                ActivitySearchBox.Focus();
                ActivitySearchBox.SelectAll();
            }
            else
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }

            e.Handled = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.Dispose();
    }
}
