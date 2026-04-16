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
    public partial class WorkerTicketsScreenViewModel : ViewModelBase
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

        private bool _isResetting;

        public WorkerTicketsScreenViewModel(User currentUser)
        {
            CurrentUser = currentUser;
            _ = LoadData();
        }

        private async Task LoadData()
        {
            Statuses = new ObservableCollection<Status>(
                await db.Statuses.AsNoTracking().ToListAsync());
            Priorities = new ObservableCollection<Priority>(
                await db.Priorities.AsNoTracking().ToListAsync());
            Types = new ObservableCollection<TicketType>(
                await db.TicketTypes.AsNoTracking().ToListAsync());
            Departments = new ObservableCollection<Department>(
                await db.Departments.AsNoTracking().ToListAsync());

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
                .Where(t => t.DepartmentId == CurrentUser.DepartmentId) // Только заявки своего отдела
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.ToLower();
                query = query.Where(t =>
                    t.Title.ToLower().Contains(s) ||
                    t.Description.ToLower().Contains(s));
            }

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
            SearchText = "";
            SelectedStatus = null;
            SelectedPriority = null;
            SelectedType = null;
            SelectedDepartment = null;
            _isResetting = false;
            await LoadTickets();
        }

        [RelayCommand]
        private void CreateTicket()
        {
            MainWindowViewModel.Instance.CurrentViewModel = new WorkerAddOrEditTicketsScreenViewModel(CurrentUser);
        }

        [RelayCommand]
        private void EditTicket(Ticket ticket)
        {
            if (ticket == null) return;
            MainWindowViewModel.Instance.CurrentViewModel = new WorkerAddOrEditTicketsScreenViewModel(CurrentUser, ticket.Id);
        }
    }
}