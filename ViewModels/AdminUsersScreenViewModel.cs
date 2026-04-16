using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiplomHelpDeskOka.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DiplomHelpDeskOka.ViewModels
{
    public partial class AdminUsersScreenViewModel : ViewModelBase
    {
        [ObservableProperty]
        private User _currentUser;

        [ObservableProperty]
        private ObservableCollection<User> _allUsers = new();

        [ObservableProperty]
        private ObservableCollection<Department> _departments = new();

        [ObservableProperty]
        private ObservableCollection<Role> _roles = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Department? _selectedDepartment;

        [ObservableProperty]
        private Role? _selectedRole;

        [ObservableProperty]
        private bool _isBusy;

        private bool _isResetting;

        public AdminUsersScreenViewModel(User currentUser)
        {
            CurrentUser = currentUser;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                IsBusy = true;

                // Загружаем справочники тоже без трекинга
                var departmentsList = await db.Departments.AsNoTracking().ToListAsync();
                departmentsList.Insert(0, new Department { Id = 0, Name = "Все отделы" });

                var rolesList = await db.Roles.AsNoTracking().ToListAsync();
                rolesList.Insert(0, new Role { Id = 0, Name = "Все роли" });

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Departments = new ObservableCollection<Department>(departmentsList);
                    Roles = new ObservableCollection<Role>(rolesList);

                    SelectedDepartment = Departments.FirstOrDefault(d => d.Id == 0);
                    SelectedRole = Roles.FirstOrDefault(r => r.Id == 0);
                });

                await LoadUsersFilteredAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            if (_isResetting) return;
            _ = LoadUsersFilteredAsync();
        }

        partial void OnSelectedDepartmentChanged(Department? value)
        {
            if (_isResetting) return;
            _ = LoadUsersFilteredAsync();
        }

        partial void OnSelectedRoleChanged(Role? value)
        {
            if (_isResetting) return;
            _ = LoadUsersFilteredAsync();
        }

        private async Task LoadUsersFilteredAsync()
        {
            try
            {
                IsBusy = true;

                var query = db.Users
                    .Include(u => u.Department)
                    .Include(u => u.Position)
                    .Include(u => u.Role)
                    .AsNoTracking() // 🔥 КРИТИЧНО: Это гарантирует, что мы берем свежие данные из БД, а не из кэша
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var search = SearchText.Trim().ToLower();

                    query = query.Where(u =>
                        (u.Name ?? "").ToLower().Contains(search) ||
                        (u.Surname ?? "").ToLower().Contains(search) ||
                        (u.Patronymic ?? "").ToLower().Contains(search) ||
                        (u.Email ?? "").ToLower().Contains(search) ||
                        (u.Login ?? "").ToLower().Contains(search));
                }

                if (SelectedDepartment != null && SelectedDepartment.Id != 0)
                    query = query.Where(u => u.DepartmentId == SelectedDepartment.Id);

                if (SelectedRole != null && SelectedRole.Id != 0)
                    query = query.Where(u => u.RoleId == SelectedRole.Id);

                var list = await query.ToListAsync();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AllUsers.Clear();
                    foreach (var user in list)
                        AllUsers.Add(user);
                });
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ResetFilters()
        {
            _isResetting = true;

            SearchText = string.Empty;
            SelectedDepartment = Departments.FirstOrDefault(d => d.Id == 0);
            SelectedRole = Roles.FirstOrDefault(r => r.Id == 0);

            _isResetting = false;

            await LoadUsersFilteredAsync();
        }

        [RelayCommand]
        private void AddUser()
        {
            MainWindowViewModel.Instance.CurrentViewModel =
                new AddOrEditUserScreenViewModel(CurrentUser);
        }

        [RelayCommand]
        private void EditUser(User user)
        {
            MainWindowViewModel.Instance.CurrentViewModel =
                new AddOrEditUserScreenViewModel(CurrentUser, user.Id);
        }

        // === ЗАМЕНА НА ДЕЙСТВИЯ БЛОКИРОВКИ/РАЗБЛОКИРОВКИ ===
        [RelayCommand]
        private async Task BlockUser(User user)
        {
            if (user == null) return;

            // 1. Получаем пользователя из БД. Это создаст "отслеживаемую" копию (tracked entity).
            var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id);

            if (dbUser != null)
            {
                // 2. Меняем статус именно у отслеживаемого объекта
                dbUser.IsDeleted = true;

                // 3. Теперь SaveChanges гарантированно запишет это в БД
                await db.SaveChangesAsync();
            }

            // 4. Обновляем список на экране
            await LoadUsersFilteredAsync();
        }

        [RelayCommand]
        private async Task UnblockUser(User user)
        {
            if (user == null) return;

            // Аналогично для разблокировки
            var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id);

            if (dbUser != null)
            {
                dbUser.IsDeleted = false;
                await db.SaveChangesAsync();
            }

            await LoadUsersFilteredAsync();
        }

        [RelayCommand]
        public void GoToMainScreen()
        {
            MainWindowViewModel.Instance.CurrentViewModel =
                new AdminMainScreenViewModel(CurrentUser);
        }

        [RelayCommand]
        public void GoToTicketsScreen()
        {
            MainWindowViewModel.Instance.CurrentViewModel =
                new AdminTicketsScreenViewModel(CurrentUser);
        }
    }
}