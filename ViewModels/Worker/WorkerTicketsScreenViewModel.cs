using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiplomHelpDeskOka.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DiplomHelpDeskOka.ViewModels;

public partial class WorkerTicketsScreenViewModel : ViewModelBase
{
    [ObservableProperty] private User _currentUser;

    [ObservableProperty] private ObservableCollection<Ticket> _tickets = new();

    [ObservableProperty] private ObservableCollection<Status> _statuses = new();
    [ObservableProperty] private ObservableCollection<Priority> _priorities = new();
    [ObservableProperty] private ObservableCollection<TicketType> _types = new();
    [ObservableProperty] private ObservableCollection<Department> _departments = new(); // Добавлено

    [ObservableProperty] private Status? _selectedStatus;
    [ObservableProperty] private Priority? _selectedPriority;
    [ObservableProperty] private TicketType? _selectedType;
    [ObservableProperty] private Department? _selectedDepartment; // Добавлено

    [ObservableProperty] private string _searchText = "";

    // Режим отображения: false = заявки отдела, true = мои заявки
    [ObservableProperty] private bool _showMyTickets = false;

    private bool _isResetting;

    public WorkerTicketsScreenViewModel(User currentUser)
    {
        CurrentUser = currentUser;
        _ = LoadDataAsync();
    }

    [RelayCommand]
    private void Logout()
    {
        MainWindowViewModel.Instance.CurrentViewModel =
            new AuthScreenViewModel(); // или твоя LoginScreenViewModel
    }

    // =========================
    // ЗАГРУЗКА ДАННЫХ
    // =========================

    private async Task LoadDataAsync()
    {
        // Загружаем справочники
        Statuses = new ObservableCollection<Status>(
            await db.Statuses.AsNoTracking().ToListAsync());

        Priorities = new ObservableCollection<Priority>(
            await db.Priorities.AsNoTracking().ToListAsync());

        Types = new ObservableCollection<TicketType>(
            await db.TicketTypes.AsNoTracking().ToListAsync());

        Departments = new ObservableCollection<Department>(
            await db.Departments.AsNoTracking().ToListAsync());

        await LoadTicketsAsync();
    }

    // =========================
    // РЕАКТИВНЫЕ ФИЛЬТРЫ
    // =========================

    partial void OnSearchTextChanged(string value)
    {
        if (_isResetting) return;
        _ = LoadTicketsAsync();
    }

    partial void OnSelectedStatusChanged(Status? value)
    {
        if (_isResetting) return;
        _ = LoadTicketsAsync();
    }

    partial void OnSelectedPriorityChanged(Priority? value)
    {
        if (_isResetting) return;
        _ = LoadTicketsAsync();
    }

    partial void OnSelectedTypeChanged(TicketType? value)
    {
        if (_isResetting) return;
        _ = LoadTicketsAsync();
    }

    partial void OnSelectedDepartmentChanged(Department? value)
    {
        if (_isResetting) return;
        _ = LoadTicketsAsync();
    }

    partial void OnShowMyTicketsChanged(bool value)
    {
        _ = LoadTicketsAsync();
    }

    // =========================
    // ОСНОВНОЙ ЗАПРОС
    // =========================

    private async Task LoadTicketsAsync()
    {
        var query = db.Tickets
            .Include(t => t.Status)
            .Include(t => t.Priority)
            .Include(t => t.TicketType)
            .Include(t => t.Department)
            .Include(t => t.Author)
            .AsQueryable();

        // Режим отображения
        if (ShowMyTickets)
        {
            // Только мои заявки
            query = query.Where(t => t.AuthorId == CurrentUser.Id);
        }
        else
        {
            // Заявки моего отдела
            query = query.Where(t => t.DepartmentId == CurrentUser.DepartmentId);
        }

        // Поиск по названию и описанию
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLower().Trim();
            query = query.Where(t =>
                t.Title.ToLower().Contains(search) ||
                t.Description.ToLower().Contains(search));
        }

        // Фильтры
        if (SelectedStatus != null)
            query = query.Where(t => t.StatusId == SelectedStatus.Id);

        if (SelectedPriority != null)
            query = query.Where(t => t.PriorityId == SelectedPriority.Id);

        if (SelectedType != null)
            query = query.Where(t => t.TicketTypeId == SelectedType.Id);

        if (SelectedDepartment != null)
            query = query.Where(t => t.DepartmentId == SelectedDepartment.Id);

        var list = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        Tickets = new ObservableCollection<Ticket>(list);
    }

    // =========================
    // КОМАНДЫ
    // =========================

    [RelayCommand]
    private async Task ResetFilters()
    {
        _isResetting = true;

        SearchText = "";
        SelectedStatus = null;
        SelectedPriority = null;
        SelectedType = null;
        SelectedDepartment = null;

        _isResetting = false;

        await LoadTicketsAsync();
    }

    [RelayCommand]
    private void ShowDepartmentTickets()
    {
        ShowMyTickets = false;
    }

    [RelayCommand]
    private void ShowMyTicketsList()
    {
        ShowMyTickets = true;
    }

    [RelayCommand]
    private void CreateTicket()
    {
        MainWindowViewModel.Instance.CurrentViewModel =
            new WorkerAddOrEditTicketsScreenViewModel(CurrentUser);
    }

    [RelayCommand]
    private void EditTicket(Ticket? ticket)
    {
        if (ticket == null) return;

        MainWindowViewModel.Instance.CurrentViewModel =
            new WorkerAddOrEditTicketsScreenViewModel(CurrentUser, ticket.Id);
    }
}