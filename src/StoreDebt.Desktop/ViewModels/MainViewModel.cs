using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using StoreDebt.Desktop.Data;
using StoreDebt.Desktop.Infrastructure;
using StoreDebt.Desktop.Models;
using StoreDebt.Desktop.Services;

namespace StoreDebt.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private static readonly CultureInfo ArabicCulture = CultureInfo.GetCultureInfo("ar-IQ");

    private readonly StoreDatabase _database;
    private readonly DialogService _dialogs;
    private readonly DebtSpeechService _speech;
    private readonly StatementService _statementService;
    private readonly WhatsAppService _whatsAppService;

    private readonly List<Customer> _allCustomers = [];
    private readonly List<ActivityRecord> _allActivity = [];

    private Customer? _selectedCustomer;
    private DebtTransaction? _selectedTransaction;
    private StatementReceipt? _currentStatement;

    private string _searchText = string.Empty;
    private string _activitySearchText = string.Empty;
    private string _statusMessage = "جاهز";
    private bool _isBusy;
    private bool _isActivityView;

    private bool _isCustomerDialogOpen;
    private bool _isEditingCustomer;
    private bool _isDebtOpen;
    private bool _isPaymentOpen;
    private bool _isTransactionEditOpen;
    private bool _isStatementOpen;

    private string _customerNameInput = string.Empty;
    private string _customerPhoneInput = string.Empty;
    private string _customerAddressInput = string.Empty;
    private string _customerNotesInput = string.Empty;

    private string _debtAmountInput = string.Empty;
    private string _debtItemsInput = string.Empty;
    private string _debtNoteInput = string.Empty;

    private string _paymentAmountInput = string.Empty;
    private string _paymentNoteInput = string.Empty;

    private string _editTransactionAmountInput = string.Empty;
    private string _editTransactionItemsInput = string.Empty;
    private string _editTransactionNoteInput = string.Empty;

    private string _statementText = string.Empty;
    private string _statementImagePath = string.Empty;
    private string _statementGeneratedAtText = string.Empty;
    private string _statementTransactionCountText = "0";

    private long _totalDebt;
    private long _todayCollections;
    private int _customerCount;

    private readonly RelayCommand _showDebtCommand;
    private readonly RelayCommand _showPaymentCommand;
    private readonly RelayCommand _showEditCustomerCommand;
    private readonly RelayCommand _deleteCustomerCommand;
    private readonly RelayCommand _showEditTransactionCommand;
    private readonly RelayCommand _deleteTransactionCommand;
    private readonly AsyncRelayCommand _showStatementCommand;
    private readonly RelayCommand _sendStatementWhatsAppCommand;

    public MainViewModel(
        StoreDatabase database,
        DialogService dialogs,
        DebtSpeechService speech,
        StatementService statementService,
        WhatsAppService whatsAppService)
    {
        _database = database;
        _dialogs = dialogs;
        _speech = speech;
        _statementService = statementService;
        _whatsAppService = whatsAppService;

        ShowAddCustomerCommand = new RelayCommand(_ => OpenAddCustomer());
        SaveCustomerCommand = new AsyncRelayCommand(SaveCustomerAsync);
        CancelDialogCommand = new RelayCommand(_ => CloseDialogs());
        EscapeCommand = new RelayCommand(_ => HandleEscape());

        _showDebtCommand = new RelayCommand(_ => OpenDebt(), _ => SelectedCustomer is not null);
        _showPaymentCommand = new RelayCommand(_ => OpenPayment(), _ => SelectedCustomer is not null);
        _showEditCustomerCommand = new RelayCommand(_ => OpenEditCustomer(), _ => SelectedCustomer is not null);
        _deleteCustomerCommand = new RelayCommand(_ => DeleteCustomer(), _ => SelectedCustomer is not null);
        _showEditTransactionCommand = new RelayCommand(
            _ => OpenEditTransaction(),
            _ => SelectedTransaction is not null);
        _deleteTransactionCommand = new RelayCommand(
            _ => DeleteTransaction(),
            _ => SelectedTransaction is not null);
        _showStatementCommand = new AsyncRelayCommand(
            ShowStatementAsync,
            () => SelectedCustomer is not null);
        _sendStatementWhatsAppCommand = new RelayCommand(
            _ => SendStatementWhatsApp(),
            _ => CanSendStatementWhatsApp);

        ShowDebtCommand = _showDebtCommand;
        ShowPaymentCommand = _showPaymentCommand;
        ShowEditCustomerCommand = _showEditCustomerCommand;
        DeleteCustomerCommand = _deleteCustomerCommand;
        ShowEditTransactionCommand = _showEditTransactionCommand;
        DeleteTransactionCommand = _deleteTransactionCommand;
        ShowStatementCommand = _showStatementCommand;
        SendStatementWhatsAppCommand = _sendStatementWhatsAppCommand;

        SaveDebtCommand = new AsyncRelayCommand(SaveDebtAsync);
        SavePaymentCommand = new AsyncRelayCommand(SavePaymentAsync);
        SaveTransactionEditCommand = new AsyncRelayCommand(SaveTransactionEditAsync);

        CopyStatementTextCommand = new RelayCommand(_ => CopyStatementText());
        OpenStatementImageCommand = new RelayCommand(_ => OpenStatementImage());
        OpenStatementFolderCommand = new RelayCommand(_ => OpenStatementFolder());

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ShowActivityCommand = new AsyncRelayCommand(ShowActivityAsync);
        ShowDashboardCommand = new RelayCommand(_ => ShowDashboard());

        SetDebtAmountCommand = new RelayCommand(SetDebtAmount);
        SetPaymentAmountCommand = new RelayCommand(SetPaymentAmount);
    }

    public ObservableCollection<Customer> Customers { get; } = [];
    public ObservableCollection<DebtTransaction> Transactions { get; } = [];
    public ObservableCollection<ActivityRecord> Activity { get; } = [];

    public ICommand ShowAddCustomerCommand { get; }
    public ICommand SaveCustomerCommand { get; }
    public ICommand CancelDialogCommand { get; }
    public ICommand EscapeCommand { get; }
    public ICommand ShowDebtCommand { get; }
    public ICommand ShowPaymentCommand { get; }
    public ICommand ShowEditCustomerCommand { get; }
    public ICommand DeleteCustomerCommand { get; }
    public ICommand ShowEditTransactionCommand { get; }
    public ICommand DeleteTransactionCommand { get; }
    public ICommand SaveDebtCommand { get; }
    public ICommand SavePaymentCommand { get; }
    public ICommand SaveTransactionEditCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ShowActivityCommand { get; }
    public ICommand ShowDashboardCommand { get; }
    public ICommand SetDebtAmountCommand { get; }
    public ICommand SetPaymentAmountCommand { get; }
    public ICommand ShowStatementCommand { get; }
    public ICommand CopyStatementTextCommand { get; }
    public ICommand OpenStatementImageCommand { get; }
    public ICommand OpenStatementFolderCommand { get; }
    public ICommand SendStatementWhatsAppCommand { get; }

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (!SetProperty(ref _selectedCustomer, value)) return;

            SelectedTransaction = null;
            RaiseCustomerCommandStates();

            OnPropertyChanged(nameof(SelectedCustomerName));
            OnPropertyChanged(nameof(SelectedCustomerPhone));
            OnPropertyChanged(nameof(SelectedCustomerAddress));
            OnPropertyChanged(nameof(SelectedCustomerNotes));
            OnPropertyChanged(nameof(SelectedCustomerDebtText));
            OnPropertyChanged(nameof(HasSelectedCustomer));
            OnPropertyChanged(nameof(CanSendStatementWhatsApp));

            _ = LoadSelectedTransactionsAsync();
        }
    }

    public DebtTransaction? SelectedTransaction
    {
        get => _selectedTransaction;
        set
        {
            if (!SetProperty(ref _selectedTransaction, value)) return;
            _showEditTransactionCommand.RaiseCanExecuteChanged();
            _deleteTransactionCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasSelectedCustomer => SelectedCustomer is not null;
    public string SelectedCustomerName => SelectedCustomer?.Name ?? "اختر زبوناً لعرض حسابه";
    public string SelectedCustomerPhone => SelectedCustomer?.ContactText ?? "—";
    public string SelectedCustomerAddress => string.IsNullOrWhiteSpace(SelectedCustomer?.Address)
        ? "لا يوجد عنوان مسجل"
        : SelectedCustomer.Address;
    public string SelectedCustomerNotes => string.IsNullOrWhiteSpace(SelectedCustomer?.Notes)
        ? "لا توجد ملاحظات"
        : SelectedCustomer.Notes;
    public string SelectedCustomerDebtText => SelectedCustomer?.DebtText ?? "0 د.ع";

    public bool CanSendStatementWhatsApp =>
        SelectedCustomer is not null &&
        _currentStatement is not null &&
        IraqiPhoneService.TryToWhatsAppDigits(SelectedCustomer.Phone, out _);

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, EnglishDigits.Normalize(value)))
                FilterCustomers();
        }
    }

    public string ActivitySearchText
    {
        get => _activitySearchText;
        set
        {
            if (SetProperty(ref _activitySearchText, EnglishDigits.Normalize(value)))
                FilterActivity();
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

    public bool IsActivityView
    {
        get => _isActivityView;
        private set
        {
            if (!SetProperty(ref _isActivityView, value)) return;
            OnPropertyChanged(nameof(IsDashboardView));
        }
    }

    public bool IsDashboardView => !IsActivityView;

    public bool IsCustomerDialogOpen
    {
        get => _isCustomerDialogOpen;
        private set => SetProperty(ref _isCustomerDialogOpen, value);
    }

    public bool IsEditingCustomer
    {
        get => _isEditingCustomer;
        private set
        {
            if (!SetProperty(ref _isEditingCustomer, value)) return;
            OnPropertyChanged(nameof(CustomerDialogTitle));
            OnPropertyChanged(nameof(CustomerDialogActionText));
        }
    }

    public string CustomerDialogTitle => IsEditingCustomer ? "تعديل بيانات الزبون" : "إضافة زبون جديد";
    public string CustomerDialogActionText => IsEditingCustomer ? "حفظ التعديلات" : "حفظ الزبون";

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

    public bool IsTransactionEditOpen
    {
        get => _isTransactionEditOpen;
        private set => SetProperty(ref _isTransactionEditOpen, value);
    }

    public bool IsStatementOpen
    {
        get => _isStatementOpen;
        private set => SetProperty(ref _isStatementOpen, value);
    }

    public string CustomerNameInput
    {
        get => _customerNameInput;
        set => SetProperty(ref _customerNameInput, value);
    }

    public string CustomerPhoneInput
    {
        get => _customerPhoneInput;
        set => SetProperty(ref _customerPhoneInput, EnglishDigits.Normalize(value));
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
        set => SetProperty(ref _debtAmountInput, EnglishDigits.Normalize(value));
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
        set => SetProperty(ref _paymentAmountInput, EnglishDigits.Normalize(value));
    }

    public string PaymentNoteInput
    {
        get => _paymentNoteInput;
        set => SetProperty(ref _paymentNoteInput, value);
    }

    public string EditTransactionAmountInput
    {
        get => _editTransactionAmountInput;
        set => SetProperty(ref _editTransactionAmountInput, EnglishDigits.Normalize(value));
    }

    public string EditTransactionItemsInput
    {
        get => _editTransactionItemsInput;
        set => SetProperty(ref _editTransactionItemsInput, value);
    }

    public string EditTransactionNoteInput
    {
        get => _editTransactionNoteInput;
        set => SetProperty(ref _editTransactionNoteInput, value);
    }

    public string EditTransactionTypeText => SelectedTransaction?.TypeText ?? "حركة";

    public string StatementText
    {
        get => _statementText;
        private set => SetProperty(ref _statementText, value);
    }

    public string StatementImagePath
    {
        get => _statementImagePath;
        private set => SetProperty(ref _statementImagePath, value);
    }

    public string StatementGeneratedAtText
    {
        get => _statementGeneratedAtText;
        private set => SetProperty(ref _statementGeneratedAtText, value);
    }

    public string StatementTransactionCountText
    {
        get => _statementTransactionCountText;
        private set => SetProperty(ref _statementTransactionCountText, value);
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
    public string CustomerCountText => EnglishDigits.Number(CustomerCount);
    public string TodayText => EnglishDigits.Normalize(
        DateTime.Now.ToString("dddd، yyyy/MM/dd", ArabicCulture));

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

        SelectedCustomer = selectedId is null
            ? SelectedCustomer
            : _allCustomers.FirstOrDefault(c => c.Id == selectedId.Value);

        var summary = await _database.GetDashboardSummaryAsync();
        TotalDebt = summary.TotalDebt;
        CustomerCount = summary.CustomerCount;
        TodayCollections = summary.TodayCollections;

        if (SelectedCustomer is not null)
            await LoadSelectedTransactionsAsync();

        if (IsActivityView)
            await LoadActivityAsync();
    }

    private void FilterCustomers()
    {
        var query = SearchText.Trim();

        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allCustomers
            : _allCustomers.Where(c =>
                c.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                c.Phone.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                IraqiPhoneService.Display(c.Phone).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Address.Contains(query, StringComparison.CurrentCultureIgnoreCase))
              .ToList();

        Customers.Clear();
        foreach (var customer in filtered)
            Customers.Add(customer);
    }

    private async Task LoadSelectedTransactionsAsync()
    {
        Transactions.Clear();
        SelectedTransaction = null;
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

    private async Task LoadActivityAsync()
    {
        var items = await _database.GetAllActivityAsync();
        _allActivity.Clear();
        _allActivity.AddRange(items);
        FilterActivity();
    }

    private void FilterActivity()
    {
        var query = ActivitySearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allActivity
            : _allActivity.Where(a =>
                a.CustomerName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                a.ItemsSummary.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                a.Note.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                EnglishDigits.Number(a.Amount).Contains(query, StringComparison.Ordinal))
              .ToList();

        Activity.Clear();
        foreach (var item in filtered)
            Activity.Add(item);
    }

    private async Task ShowActivityAsync()
    {
        CloseDialogs();
        IsActivityView = true;
        await LoadActivityAsync();
        StatusMessage = $"عرض آخر {EnglishDigits.Number(Activity.Count)} حركة.";
    }

    private void ShowDashboard()
    {
        CloseDialogs();
        IsActivityView = false;
        StatusMessage = "العودة إلى إدارة حسابات الزبائن.";
    }

    private void OpenAddCustomer()
    {
        CloseDialogs();
        IsEditingCustomer = false;
        CustomerNameInput = string.Empty;
        CustomerPhoneInput = string.Empty;
        CustomerAddressInput = string.Empty;
        CustomerNotesInput = string.Empty;
        IsCustomerDialogOpen = true;
    }

    private void OpenEditCustomer()
    {
        var customer = SelectedCustomer;
        if (customer is null) return;

        CloseDialogs();
        IsEditingCustomer = true;
        CustomerNameInput = customer.Name;
        CustomerPhoneInput = IraqiPhoneService.Display(customer.Phone) == "بدون رقم هاتف"
            ? string.Empty
            : IraqiPhoneService.Display(customer.Phone);
        CustomerAddressInput = customer.Address;
        CustomerNotesInput = customer.Notes;
        IsCustomerDialogOpen = true;
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

    private void OpenEditTransaction()
    {
        var tx = SelectedTransaction;
        if (tx is null) return;

        CloseDialogs();
        EditTransactionAmountInput = tx.Amount.ToString(CultureInfo.InvariantCulture);
        EditTransactionItemsInput = tx.ItemsSummary;
        EditTransactionNoteInput = tx.Note;
        OnPropertyChanged(nameof(EditTransactionTypeText));
        IsTransactionEditOpen = true;
    }

    private async Task ShowStatementAsync()
    {
        var customer = SelectedCustomer;
        if (customer is null) return;

        try
        {
            IsBusy = true;
            CloseDialogs();

            var transactions = await _database.GetStatementTransactionsAsync(customer.Id);
            _currentStatement = _statementService.Create(customer, transactions);

            StatementText = _currentStatement.Text;
            StatementImagePath = _currentStatement.ImagePath;
            StatementGeneratedAtText = _currentStatement.GeneratedAtText;
            StatementTransactionCountText = EnglishDigits.Number(_currentStatement.TransactionCount);

            IsStatementOpen = true;
            OnPropertyChanged(nameof(CanSendStatementWhatsApp));
            _sendStatementWhatsAppCommand.RaiseCanExecuteChanged();

            StatusMessage = "تم إنشاء كشف الحساب كصورة ونص.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر إنشاء كشف الحساب: {ex.Message}";
            _dialogs.Info(ex.Message, "تعذر إنشاء كشف الحساب");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CopyStatementText()
    {
        if (_currentStatement is null) return;

        try
        {
            _statementService.CopyText(_currentStatement.Text);
            StatusMessage = "تم نسخ نص كشف الحساب.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر نسخ النص: {ex.Message}";
        }
    }

    private void OpenStatementImage()
    {
        if (_currentStatement is null) return;

        try
        {
            _statementService.OpenImage(_currentStatement.ImagePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر فتح صورة الكشف: {ex.Message}";
        }
    }

    private void OpenStatementFolder()
    {
        if (_currentStatement is null) return;

        try
        {
            _statementService.OpenFolder(_currentStatement.ImagePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر فتح مجلد الكشوفات: {ex.Message}";
        }
    }

    private void SendStatementWhatsApp()
    {
        var customer = SelectedCustomer;
        var receipt = _currentStatement;
        if (customer is null || receipt is null) return;

        try
        {
            StatusMessage = _whatsAppService.OpenStatementInDesktopApp(customer, receipt);
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر فتح واتساب: {ex.Message}";
            _dialogs.Info(ex.Message, "تعذر إرسال كشف الحساب");
        }
    }

    private void CloseDialogs()
    {
        IsCustomerDialogOpen = false;
        IsDebtOpen = false;
        IsPaymentOpen = false;
        IsTransactionEditOpen = false;
        IsStatementOpen = false;
    }

    private void HandleEscape()
    {
        if (IsCustomerDialogOpen ||
            IsDebtOpen ||
            IsPaymentOpen ||
            IsTransactionEditOpen ||
            IsStatementOpen)
        {
            CloseDialogs();
            return;
        }

        if (IsActivityView)
        {
            ShowDashboard();
            return;
        }

        SelectedTransaction = null;
    }

    private async Task SaveCustomerAsync()
    {
        var name = CustomerNameInput.Trim();
        if (name.Length < 2)
        {
            StatusMessage = "اكتب اسم الزبون أولاً.";
            return;
        }

        if (!IraqiPhoneService.TryNormalizeForStorage(CustomerPhoneInput, out var normalizedPhone))
        {
            const string message =
                "رقم الهاتف يجب أن يكون عراقياً. مثال: 07701234567. يمكن أيضاً إدخال +964 أو 00964 وسيحوّله البرنامج تلقائياً.";
            StatusMessage = message;
            _dialogs.Info(message, "رقم هاتف غير صالح");
            return;
        }

        try
        {
            IsBusy = true;

            if (IsEditingCustomer)
            {
                var customer = SelectedCustomer
                    ?? throw new InvalidOperationException("لم يتم تحديد زبون.");

                await _database.UpdateCustomerAsync(
                    customer.Id,
                    name,
                    normalizedPhone,
                    CustomerAddressInput,
                    CustomerNotesInput);

                CloseDialogs();
                await RefreshAsync();
                StatusMessage = $"تم تحديث بيانات {name}.";
            }
            else
            {
                var id = await _database.AddCustomerAsync(
                    name,
                    normalizedPhone,
                    CustomerAddressInput,
                    CustomerNotesInput);

                CloseDialogs();
                await RefreshAsync();
                SelectedCustomer = _allCustomers.FirstOrDefault(c => c.Id == id);
                StatusMessage = $"تمت إضافة {name} بنجاح.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر حفظ بيانات الزبون: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void DeleteCustomer()
    {
        var customer = SelectedCustomer;
        if (customer is null) return;

        if (customer.TotalDebt != 0)
        {
            _dialogs.Info(
                $"لا يمكن حذف {customer.Name} لأن عليه ديناً قدره {MoneyFormatter.Format(customer.TotalDebt)}.\nصفّر الحساب أولاً.",
                "حماية حساب الزبون");
            return;
        }

        if (!_dialogs.Confirm(
                $"سيتم حذف الزبون «{customer.Name}» وجميع حركاته القديمة نهائياً.\n\nهل تريد المتابعة؟",
                "حذف الزبون"))
            return;

        try
        {
            IsBusy = true;
            await _database.DeleteCustomerAsync(customer.Id);
            SelectedCustomer = null;
            await RefreshAsync();
            StatusMessage = $"تم حذف {customer.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر حذف الزبون: {ex.Message}";
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

            // Start speech immediately after the debt is safely committed,
            // before closing the dialog or refreshing any UI data.
            _ = _speech.SpeakDebtAsync(amount);

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

    private async Task SaveTransactionEditAsync()
    {
        var tx = SelectedTransaction;
        if (tx is null) return;

        if (!MoneyFormatter.TryParse(EditTransactionAmountInput, out var amount))
        {
            StatusMessage = "أدخل مبلغاً صحيحاً أكبر من صفر.";
            return;
        }

        if (!_dialogs.Confirm(
                $"سيتم تعديل حركة «{tx.TypeText}» القديمة وإعادة حساب رصيد الزبون وكل الأرصدة اللاحقة.\n\nهل تريد حفظ التعديل؟",
                "تعديل حركة حساب"))
            return;

        try
        {
            IsBusy = true;
            await _database.UpdateTransactionAsync(
                tx.Id,
                amount,
                EditTransactionItemsInput,
                EditTransactionNoteInput);

            CloseDialogs();
            await RefreshAsync();
            StatusMessage = "تم تعديل الحركة وإعادة حساب الرصيد بنجاح.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"لم يتم تعديل الحركة: {ex.Message}";
            _dialogs.Info(ex.Message, "تعذر تعديل الحركة");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void DeleteTransaction()
    {
        var tx = SelectedTransaction;
        var customer = SelectedCustomer;
        if (tx is null || customer is null) return;

        if (!_dialogs.Confirm(
                $"حذف حركة «{tx.TypeText}» بقيمة {MoneyFormatter.Format(tx.Amount)} من حساب {customer.Name}؟\n\nسيُعاد حساب الرصيد تلقائياً بعد الحذف.",
                "حذف حركة حساب"))
            return;

        try
        {
            IsBusy = true;
            await _database.DeleteTransactionAsync(tx.Id);
            SelectedTransaction = null;
            await RefreshAsync();
            StatusMessage = "تم حذف الحركة وإعادة حساب الحساب.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"لم يتم حذف الحركة: {ex.Message}";
            _dialogs.Info(ex.Message, "تعذر حذف الحركة");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetDebtAmount(object? parameter)
    {
        if (parameter is null) return;

        if (!MoneyFormatter.TryParse(parameter.ToString(), out var increment))
            return;

        long current = 0;
        if (!string.IsNullOrWhiteSpace(DebtAmountInput) &&
            !MoneyFormatter.TryParse(DebtAmountInput, out current))
        {
            current = 0;
        }

        try
        {
            var total = checked(current + increment);
            DebtAmountInput = total.ToString(CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            StatusMessage = "المبلغ أكبر من الحد المسموح.";
        }
    }

    private void SetPaymentAmount(object? parameter)
    {
        if (parameter is null) return;
        PaymentAmountInput = EnglishDigits.Normalize(parameter.ToString());
    }

    private void RaiseCustomerCommandStates()
    {
        _showDebtCommand.RaiseCanExecuteChanged();
        _showPaymentCommand.RaiseCanExecuteChanged();
        _showEditCustomerCommand.RaiseCanExecuteChanged();
        _deleteCustomerCommand.RaiseCanExecuteChanged();
        _showStatementCommand.RaiseCanExecuteChanged();
        _sendStatementWhatsAppCommand.RaiseCanExecuteChanged();
    }

    public void PrepareSpeech()
    {
        _speech.Prepare();
    }

    public void Dispose()
    {
        _speech.Dispose();
    }
}
