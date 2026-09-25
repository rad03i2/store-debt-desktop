using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StoreDebt.Desktop.Data;
using StoreDebt.Desktop.Models;
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
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ApplySafeMaximizedBounds();

        await _viewModel.InitializeAsync();
        _viewModel.PrepareSpeech();
        SearchBox.Focus();
    }

    private void ApplySafeMaximizedBounds()
    {
        var workArea = SystemParameters.WorkArea;

        MaxWidth = Math.Max(MinWidth, workArea.Width);
        MaxHeight = Math.Max(MinHeight, workArea.Height - 8);
        WindowState = WindowState.Maximized;
    }

    private void OnSystemParametersChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SystemParameters.WorkArea))
            ApplySafeMaximizedBounds();
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
            else if (!_viewModel.IsSettingsView)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }

            e.Handled = true;
        }
    }

    private void EditCustomerMenu_Click(object sender, RoutedEventArgs e)
    {
        if (TrySelectContextCustomer(sender) &&
            _viewModel.ShowEditCustomerCommand.CanExecute(null))
        {
            _viewModel.ShowEditCustomerCommand.Execute(null);
        }
    }

    private void StatementMenu_Click(object sender, RoutedEventArgs e)
    {
        if (TrySelectContextCustomer(sender) &&
            _viewModel.ShowStatementCommand.CanExecute(null))
        {
            _viewModel.ShowStatementCommand.Execute(null);
        }
    }

    private bool TrySelectContextCustomer(object sender)
    {
        if (sender is not MenuItem menuItem ||
            menuItem.Parent is not ContextMenu contextMenu ||
            contextMenu.PlacementTarget is not FrameworkElement placementTarget ||
            placementTarget.DataContext is not Customer customer)
        {
            return false;
        }

        _viewModel.SelectedCustomer = customer;
        return true;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        _viewModel.Dispose();
    }
}
