using System.Collections.ObjectModel;
using System.Windows.Input;
using StoreDebt.Desktop.Data;
using StoreDebt.Desktop.Infrastructure;
using StoreDebt.Desktop.Models;
using StoreDebt.Desktop.Services;

namespace StoreDebt.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly StoreDatabase _database;
    private readonly List<Customer> _allCustomers = [];

    private Customer? _selectedCustomer;
    private string _searchText = string.Empty;
    private string _statusMessage = "جاهز";
    private bool _isBusy;
    private bool _isAddCustomerOpen;
    private bool _isDebtOpen;
    private bool _isPaymentOpen;

    private string _customerNameInput = string.Empty;
    private string _customerPhoneInput = string.Empty;
    private string _customerAddressInput = string.Empty;
    private string _customerNotesInput = string.Empty;

    private string _debtAmountInput = string.Empty;
    private string _debtItemsInput = string.Empty;
    private string _debtNoteInput = string.Empty;

    private string _paymentAmountInput = string.Empty;
    private string _paymentNoteInput = string.Empty;

    private long _totalDebt;
    private long _todayCollections;
    private int _customerCount;

    private readonly RelayCommand _showDebtCommand;
    private readonly RelayCommand _showPaymentCommand;

    public MainViewModel(StoreDatabase database)
    {
        _database = database;

        ShowAddCustomerCommand = new RelayCommand(_ => OpenAddCustomer());
        SaveCustomerCommand = new AsyncRelayCommand(SaveCustomerAsync);
        CancelDialogCommand = new RelayCommand(_ => CloseDialogs());

        _showDebtCommand = new RelayCommand(_ => OpenDebt(), _ => SelectedCustomer is not null);
        _showPaymentCommand = new RelayCommand(_ => OpenPayment(), _ => SelectedCustomer is not null);

        ShowDebtCommand = _showDebtCommand;
        ShowPaymentCommand = _showPaymentCommand;

        SaveDebtCommand = new AsyncRelayCommand(SaveDebtAsync, () => SelectedCustomer is not null);
        SavePaymentCommand = new AsyncRelayCommand(SavePaymentAsync, () => SelectedCustomer is not null);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);

        SetDebtAmountCommand = new RelayCommand(SetDebtAmount);
        SetPaymentAmountCommand = new RelayCommand(SetPaymentAmount);
    }

    public ObservableCollection<Customer> Customers { get; } = [];
    public ObservableCollection<DebtTransaction> Transactions { get; } = [];

    public ICommand ShowAddCustomerCommand { get; }
    public ICommand SaveCustomerCommand { get; }
    public ICommand CancelDialogCommand { get; }
    public ICommand ShowDebtCommand { get; }
    public ICommand ShowPaymentCommand { get; }
    public ICommand SaveDebtCommand { get; }
    public ICommand SavePaymentCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SetDebtAmountCommand { get; }
    public ICommand SetPaymentAmountCommand { get; }

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (!SetProperty(ref _selectedCustomer, value)) return;

            _showDebtCommand.RaiseCanExecuteChanged();
            _showPaymentCommand.RaiseCanExecuteChanged();

            OnPropertyChanged(nameof(SelectedCustomerName));
            OnPropertyChanged(nameof(SelectedCustomerPhone));
            OnPropertyChanged(nameof(SelectedCustomerAddress));
            OnPropertyChanged(nameof(SelectedCustomerDebtText));
            OnPropertyChanged(nameof(HasSelectedCustomer));

            _ = LoadSelectedTransactionsAsync();
        }
    }

    public bool HasSelectedCustomer => SelectedCustomer is not null;
    public string SelectedCustomerName => SelectedCustomer?.Name ?? "اختر زبوناً لعرض حسابه";
    public string SelectedCustomerPhone => SelectedCustomer?.ContactText ?? "—";
    public string SelectedCustomerAddress => string.IsNullOrWhiteSpace(SelectedCustomer?.Address)
        ? "لا يوجد عنوان مسجل"
        : SelectedCustomer.Address;
    public string SelectedCustomerDebtText => SelectedCustomer?.DebtText ?? "0 د.ع";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilterCustomers();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsAddCustomerOpen
    {
        get => _isAddCustomerOpen;
        private set => SetProperty(ref _isAddCustomerOpen, value);
    }

    public bool IsDebtOpen
    {
        get => _isDebtOpen;
        private set => SetProperty(ref _isDebtOpen, value);
    }

    public bool IsPaymentOpen
    {
        get => _isPaymentOpen;
        private set => SetProperty(ref _isPaymentOpen, value);
    }

    public string CustomerNameInput
    {
        get => _customerNameInput;
        set => SetProperty(ref _customerNameInput, value);
    }

    public string CustomerPhoneInput
    {
        get => _customerPhoneInput;
        set => SetProperty(ref _customerPhoneInput, value);
    }

    public string CustomerAddressInput
    {
        get => _customerAddressInput;
        set => SetProperty(ref _customerAddressInput, value);
    }

    public string CustomerNotesInput
    {
        get => _customerNotesInput;
        set => SetProperty(ref _customerNotesInput, value);
    }

    public string DebtAmountInput
    {
        get => _debtAmountInput;
        set => SetProperty(ref _debtAmountInput, value);
    }

    public string DebtItemsInput
    {
        get => _debtItemsInput;
        set => SetProperty(ref _debtItemsInput, value);
    }

    public string DebtNoteInput
    {
        get => _debtNoteInput;
        set => SetProperty(ref _debtNoteInput, value);
    }

    public string PaymentAmountInput
    {
        get => _paymentAmountInput;
        set => SetProperty(ref _paymentAmountInput, value);
    }

    public string PaymentNoteInput
    {
        get => _paymentNoteInput;
        set => SetProperty(ref _paymentNoteInput, value);
    }

    public long TotalDebt
    {
        get => _totalDebt;
        private set
        {
            if (SetProperty(ref _totalDebt, value))
                OnPropertyChanged(nameof(TotalDebtText));
        }
    }

    public long TodayCollections
    {
        get => _todayCollections;
        private set
        {
            if (SetProperty(ref _todayCollections, value))
                OnPropertyChanged(nameof(TodayCollectionsText));
        }
    }

    public int CustomerCount
    {
        get => _customerCount;
        private set
        {
            if (SetProperty(ref _customerCount, value))
                OnPropertyChanged(nameof(CustomerCountText));
        }
    }

    public string TotalDebtText => MoneyFormatter.Format(TotalDebt);
    public string TodayCollectionsText => MoneyFormatter.Format(TodayCollections);
    public string CustomerCountText => CustomerCount.ToString("N0");
    public string TodayText => DateTime.Now.ToString("dddd، yyyy/MM/dd");

    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "جاري تجهيز قاعدة بيانات المحل...";
            await _database.InitializeAsync();
            await RefreshAsync();
            StatusMessage = "البرنامج جاهز للعمل";
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر تشغيل قاعدة البيانات: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshAsync()
    {
        var selectedId = SelectedCustomer?.Id;

        var customers = await _database.GetCustomersAsync();
        _allCustomers.Clear();
        _allCustomers.AddRange(customers);
        FilterCustomers();

        if (selectedId is not null)
        {
            SelectedCustomer = _allCustomers.FirstOrDefault(c => c.Id == selectedId.Value);
        }

        var summary = await _database.GetDashboardSummaryAsync();
        TotalDebt = summary.TotalDebt;
        CustomerCount = summary.CustomerCount;
        TodayCollections = summary.TodayCollections;

        if (SelectedCustomer is not null)
        {
            await LoadSelectedTransactionsAsync();
        }
    }

    private void FilterCustomers()
    {
        var query = SearchText.Trim();

        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allCustomers
            : _allCustomers.Where(c =>
                c.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                c.Phone.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Address.Contains(query, StringComparison.CurrentCultureIgnoreCase))
              .ToList();

        Customers.Clear();
        foreach (var customer in filtered)
            Customers.Add(customer);
    }

    private async Task LoadSelectedTransactionsAsync()
    {
        Transactions.Clear();
        if (SelectedCustomer is null) return;

        try
        {
            var items = await _database.GetTransactionsAsync(SelectedCustomer.Id);
            foreach (var item in items)
                Transactions.Add(item);
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر قراءة سجل الحساب: {ex.Message}";
        }
    }

    private void OpenAddCustomer()
    {
        CloseDialogs();
        CustomerNameInput = string.Empty;
        CustomerPhoneInput = string.Empty;
        CustomerAddressInput = string.Empty;
        CustomerNotesInput = string.Empty;
        IsAddCustomerOpen = true;
    }

    private void OpenDebt()
    {
        if (SelectedCustomer is null) return;
        CloseDialogs();
        DebtAmountInput = string.Empty;
        DebtItemsInput = string.Empty;
        DebtNoteInput = string.Empty;
        IsDebtOpen = true;
    }

    private void OpenPayment()
    {
        if (SelectedCustomer is null) return;
        CloseDialogs();
        PaymentAmountInput = string.Empty;
        PaymentNoteInput = string.Empty;
        IsPaymentOpen = true;
    }

    private void CloseDialogs()
    {
        IsAddCustomerOpen = false;
        IsDebtOpen = false;
        IsPaymentOpen = false;
    }

    private async Task SaveCustomerAsync()
    {
        var name = CustomerNameInput.Trim();
        if (name.Length < 2)
        {
            StatusMessage = "اكتب اسم الزبون أولاً.";
            return;
        }

        try
        {
            IsBusy = true;
            var id = await _database.AddCustomerAsync(
                name,
                CustomerPhoneInput,
                CustomerAddressInput,
                CustomerNotesInput);

            CloseDialogs();
            await RefreshAsync();
            SelectedCustomer = _allCustomers.FirstOrDefault(c => c.Id == id);
            StatusMessage = $"تمت إضافة {name} بنجاح.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذرت إضافة الزبون: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveDebtAsync()
    {
        var customer = SelectedCustomer;
        if (customer is null) return;

        if (!MoneyFormatter.TryParse(DebtAmountInput, out var amount))
        {
            StatusMessage = "أدخل مبلغ دين صحيح أكبر من صفر.";
            return;
        }

        try
        {
            IsBusy = true;
            await _database.RecordDebtAsync(customer.Id, amount, DebtItemsInput, DebtNoteInput);
            CloseDialogs();
            await RefreshAsync();
            StatusMessage = $"تم تسجيل دين {MoneyFormatter.Format(amount)} على {customer.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر تسجيل الدين: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SavePaymentAsync()
    {
        var customer = SelectedCustomer;
        if (customer is null) return;

        if (!MoneyFormatter.TryParse(PaymentAmountInput, out var amount))
        {
            StatusMessage = "أدخل مبلغ تسديد صحيح أكبر من صفر.";
            return;
        }

        if (amount > customer.TotalDebt)
        {
            StatusMessage = "مبلغ التسديد لا يمكن أن يكون أكبر من الدين الحالي.";
            return;
        }

        try
        {
            IsBusy = true;
            await _database.RecordPaymentAsync(customer.Id, amount, PaymentNoteInput);
            CloseDialogs();
            await RefreshAsync();
            StatusMessage = $"تم تسجيل تسديد {MoneyFormatter.Format(amount)} من {customer.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر تسجيل التسديد: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetDebtAmount(object? parameter)
    {
        if (parameter is null) return;
        DebtAmountInput = parameter.ToString() ?? string.Empty;
    }

    private void SetPaymentAmount(object? parameter)
    {
        if (parameter is null) return;
        PaymentAmountInput = parameter.ToString() ?? string.Empty;
    }
}
