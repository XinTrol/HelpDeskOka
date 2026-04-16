using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiplomHelpDeskOka.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DiplomHelpDeskOka.ViewModels
{
    public partial class AdminTicketsScreenViewModel : ViewModelBase
    {
        [ObservableProperty] private User _currentUser;

        [ObservableProperty] private ObservableCollection<Ticket> _tickets = new();

        [ObservableProperty] private ObservableCollection<Status> _statuses = new();
        [ObservableProperty] private ObservableCollection<Priority> _priorities = new();
        [ObservableProperty] private ObservableCollection<TicketType> _types = new();
        [ObservableProperty] private ObservableCollection<Department> _departments = new();

        [ObservableProperty] private Status? _selectedStatus;
        [ObservableProperty] private Priority? _selectedPriority;
        [ObservableProperty] private TicketType? _selectedType;
        [ObservableProperty] private Department? _selectedDepartment;

        [ObservableProperty] private string _searchText = "";

        // Поля для хранения фильтров с Главного экрана
        private string? _initialFilterType;
        private DateTime? _filterStartDate;

        private bool _isResetting;

        // 🔥 Обновленный конструктор для приема параметров фильтрации
        public AdminTicketsScreenViewModel(User currentUser, string? initialFilterType = null, DateTime? filterStartDate = null)
        {
            CurrentUser = currentUser;
            _initialFilterType = initialFilterType;
            _filterStartDate = filterStartDate;

            _ = LoadData();
        }

        private async Task LoadData()
        {
            // 1. Загружаем справочники
            Statuses = new ObservableCollection<Status>(
                await db.Statuses.AsNoTracking().ToListAsync());

            Priorities = new ObservableCollection<Priority>(
                await db.Priorities.AsNoTracking().ToListAsync());

            Types = new ObservableCollection<TicketType>(
                await db.TicketTypes.AsNoTracking().ToListAsync());

            Departments = new ObservableCollection<Department>(
                await db.Departments.AsNoTracking().ToListAsync());

            // 2. Если фильтр пришел с главного экрана и он относится к Статусу, подставляем его в UI
            // Это позволяет пользователю "сбросить" фильтр через интерфейс, если захочет
            if (!string.IsNullOrEmpty(_initialFilterType))
            {
                switch (_initialFilterType)
                {
                    case "New":
                        SelectedStatus = Statuses.FirstOrDefault(s => s.Name == "новая");
                        break;
                    case "InProgress":
                        SelectedStatus = Statuses.FirstOrDefault(s => s.Name == "в работе");
                        break;
                    case "Closed":
                        SelectedStatus = Statuses.FirstOrDefault(s => s.Name == "закрыта");
                        break;
                        // Для "Overdate" мы не ставим SelectedStatus, так как это не реальный статус в базе, а условие
                }
            }

            await LoadTickets();
        }

        partial void OnSearchTextChanged(string value)
        {
            if (_isResetting) return;
            _ = LoadTickets();
        }

        partial void OnSelectedStatusChanged(Status? value)
        {
            if (_isResetting) return;
            _ = LoadTickets();
        }

        partial void OnSelectedPriorityChanged(Priority? value)
        {
            if (_isResetting) return;
            _ = LoadTickets();
        }

        partial void OnSelectedTypeChanged(TicketType? value)
        {
            if (_isResetting) return;
            _ = LoadTickets();
        }

        partial void OnSelectedDepartmentChanged(Department? value)
        {
            if (_isResetting) return;
            _ = LoadTickets();
        }

        private async Task LoadTickets()
        {
            var query = db.Tickets
                .Include(t => t.Status)
                .Include(t => t.Priority)
                .Include(t => t.TicketType)
                .Include(t => t.Department)
                .Include(t => t.Author)
                .AsQueryable();

            // --- 1. ФИЛЬТР ПО ДАТЕ (С Главного экрана) ---
            if (_filterStartDate.HasValue)
            {
                // Ищем заявки, созданные позже указанной даты
                query = query.Where(t => t.CreatedAt >= _filterStartDate.Value);
            }

            // --- 2. ФИЛЬТР ПО ТИПУ "ПРОСРОЧЕНО" (С Главного экрана) ---
            if (_initialFilterType == "Overdue")
            {
                // Логика: Дата закрытия null И плановая дата меньше текущего момента
                query = query.Where(t =>
                    t.ClosedAt == null &&
                    t.PlannedCompletionDate.HasValue &&
                    t.PlannedCompletionDate.Value < DateTime.Now);
            }

            // --- 3. ПОИСК ---
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.ToLower();
                query = query.Where(t =>
                    t.Title.ToLower().Contains(s) ||
                    t.Description.ToLower().Contains(s));
            }

            // --- 4. ФИЛЬТРЫ UI (СТАТУС, ПРИОРИТЕТ И Т.Д.) ---
            if (SelectedStatus != null)
                query = query.Where(t => t.StatusId == SelectedStatus.Id);

            if (SelectedPriority != null)
                query = query.Where(t => t.PriorityId == SelectedPriority.Id);

            if (SelectedType != null)
                query = query.Where(t => t.TicketTypeId == SelectedType.Id);

            if (SelectedDepartment != null)
                query = query.Where(t => t.DepartmentId == SelectedDepartment.Id);

            var list = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

            Tickets.Clear();
            foreach (var t in list)
                Tickets.Add(t);
        }

        [RelayCommand]
        private async Task ResetFilters()
        {
            _isResetting = true;

            // Сбрасываем только UI фильтры
            SearchText = "";
            SelectedStatus = null;
            SelectedPriority = null;
            SelectedType = null;
            SelectedDepartment = null;

            // Фильтры с Главного экрана (дата, просрочено) остаются, так как это контекст страницы.
            // Если пользователь хочет их сбросить, он должен вернуться назад и выбрать другой пункт меню.

            _isResetting = false;
            await LoadTickets();
        }

        [RelayCommand]
        private void CreateTicket()
        {
            MainWindowViewModel.Instance.CurrentViewModel = new AddOrEditTicketsScreenViewModel(CurrentUser);
        }

        [RelayCommand]
        private void EditTicket(Ticket ticket)
        {
            if (ticket == null) return;
            MainWindowViewModel.Instance.CurrentViewModel = new AddOrEditTicketsScreenViewModel(CurrentUser, ticket.Id);
        }

        [RelayCommand]
        private async Task DeleteTicket(Ticket ticket)
        {
            if (ticket == null) return;

            var result = await ShowConfirmationDialog("Удаление", $"Удалить заявку \"{ticket.Title}\"?");
            if (!result) return;

            db.Tickets.Remove(ticket);
            await db.SaveChangesAsync();
            await LoadTickets();
        }

        private async Task<bool> ShowConfirmationDialog(string title, string message)
        {
            // Заглушка - возвращаем true для теста
            return true;
        }

        [RelayCommand]
        public void GoToMainScreen()
        {
            MainWindowViewModel.Instance.CurrentViewModel = new AdminMainScreenViewModel(CurrentUser);
        }

        [RelayCommand]
        public void GoToUsers()
        {
            MainWindowViewModel.Instance.CurrentViewModel = new AdminUsersScreenViewModel(CurrentUser);
        }
    }
}