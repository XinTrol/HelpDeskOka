using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiplomHelpDeskOka.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace DiplomHelpDeskOka.ViewModels
{
    public partial class AddOrEditUserScreenViewModel : ViewModelBase
    {
        private readonly long? _editingUserId;

        [ObservableProperty] private User _currentUser;

        // Поля формы
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _surname = "";
        [ObservableProperty] private string _patronymic = "";
        [ObservableProperty] private string _phone = ""; // Есть свойство
        [ObservableProperty] private string _email = "";
        [ObservableProperty] private string _login = "";
        [ObservableProperty] private string _password = "";

        // Коллекции для выпадающих списков
        [ObservableProperty] private ObservableCollection<Department> _departments = new();
        [ObservableProperty] private ObservableCollection<Role> _roles = new();
        [ObservableProperty] private ObservableCollection<Position> _positions = new();

        [ObservableProperty] private Department? _selectedDepartment;
        [ObservableProperty] private Role? _selectedRole;
        [ObservableProperty] private Position? _selectedPosition;

        public bool IsEditMode => _editingUserId.HasValue;
        public string Title => IsEditMode ? "Редактирование пользователя" : "Добавление пользователя";
        public string SaveButtonText => IsEditMode ? "Сохранить" : "Создать";

        public AddOrEditUserScreenViewModel(User currentUser, long? userId = null)
        {
            CurrentUser = currentUser;
            _editingUserId = userId;
            _ = LoadData();
        }

        private async Task LoadData()
        {
            // Загружаем справочники без трекинга
            var departments = await db.Departments.AsNoTracking().ToListAsync();
            var roles = await db.Roles.AsNoTracking().ToListAsync();
            var positions = await db.Positions.AsNoTracking().ToListAsync();

            Departments = new ObservableCollection<Department>(departments);
            Roles = new ObservableCollection<Role>(roles);
            Positions = new ObservableCollection<Position>(positions);

            if (IsEditMode)
            {
                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == _editingUserId);
                if (user == null) return;

                Name = user.Name ?? "";
                Surname = user.Surname ?? "";
                Patronymic = user.Patronymic ?? "";
                Phone = user.Phone ?? ""; // 🔥 ЗАГРУЗКА ТЕЛЕФОНА
                Email = user.Email ?? "";
                Login = user.Login ?? "";

                SelectedDepartment = Departments.FirstOrDefault(d => d.Id == user.DepartmentId);
                SelectedRole = Roles.FirstOrDefault(r => r.Id == user.RoleId);
                SelectedPosition = Positions.FirstOrDefault(p => p.Id == user.PositionId);
            }
            else
            {
                SelectedDepartment = null;
                SelectedRole = null;
                SelectedPosition = null;
            }
        }

        // === ДОБАВЛЕНИЕ НОВОГО ОТДЕЛА ===
        [RelayCommand]
        private async Task AddDepartment()
        {
            string newName = await ShowInputDialogAsync("Новый отдел", "Введите название отдела:");
            if (string.IsNullOrWhiteSpace(newName))
                return;

            var existing = await db.Departments.AnyAsync(d => d.Name == newName);
            if (existing)
            {
                await ShowMessageAsync("Ошибка", "Отдел с таким названием уже существует.");
                return;
            }

            var newDept = new Department { Name = newName };
            await db.Departments.AddAsync(newDept);
            await db.SaveChangesAsync();

            var updated = await db.Departments.AsNoTracking().ToListAsync();
            Departments = new ObservableCollection<Department>(updated);
            SelectedDepartment = Departments.FirstOrDefault(d => d.Id == newDept.Id);
        }

        // === ДОБАВЛЕНИЕ НОВОЙ ДОЛЖНОСТИ ===
        [RelayCommand]
        private async Task AddPosition()
        {
            string newName = await ShowInputDialogAsync("Новая должность", "Введите название должности:");
            if (string.IsNullOrWhiteSpace(newName))
                return;

            var existing = await db.Positions.AnyAsync(p => p.Name == newName);
            if (existing)
            {
                await ShowMessageAsync("Ошибка", "Должность с таким названием уже существует.");
                return;
            }

            var newPos = new Position { Name = newName };
            await db.Positions.AddAsync(newPos);
            await db.SaveChangesAsync();

            var updated = await db.Positions.AsNoTracking().ToListAsync();
            Positions = new ObservableCollection<Position>(updated);
            SelectedPosition = Positions.FirstOrDefault(p => p.Id == newPos.Id);
        }

        // === СОХРАНЕНИЕ ===
        [RelayCommand]
        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Login) || SelectedRole == null)
                return;

            if (IsEditMode)
            {
                var user = await db.Users.FirstOrDefaultAsync(x => x.Id == _editingUserId);
                if (user == null) return;

                user.Name = Name;
                user.Surname = Surname;
                user.Patronymic = Patronymic;
                user.Phone = Phone; // 🔥 СОХРАНЕНИЕ ТЕЛЕФОНА
                user.Email = Email;
                user.Login = Login;
                user.DepartmentId = SelectedDepartment?.Id;
                user.RoleId = SelectedRole.Id;
                user.PositionId = SelectedPosition?.Id;

                if (!string.IsNullOrWhiteSpace(Password))
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Password)) return;

                var newUser = new User
                {
                    Name = Name,
                    Surname = Surname,
                    Patronymic = Patronymic,
                    Phone = Phone, // 🔥 СОЗДАНИЕ С ТЕЛЕФОНОМ
                    Email = Email,
                    Login = Login,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                    DepartmentId = SelectedDepartment?.Id,
                    RoleId = SelectedRole.Id,
                    PositionId = SelectedPosition?.Id
                };
                await db.Users.AddAsync(newUser);
            }

            await db.SaveChangesAsync();
            GoBack();
        }

        // === КОМАНДА ПЕРЕХОДА К ЗАЯВКАМ ===
        [RelayCommand]
        public void GoToTicketsScreen()
        {
            MainWindowViewModel.Instance.CurrentViewModel = new AdminTicketsScreenViewModel(CurrentUser);
        }

        // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДИАЛОГОВ ===
        private async Task<string?> ShowInputDialogAsync(string title, string prompt)
        {
            var textBox = new TextBox { Watermark = prompt, Width = 300 };

            var okButton = new Button { Content = "OK", Width = 80 };
            var cancelButton = new Button { Content = "Отмена", Width = 80 };

            string? result = null;

            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 10,
                    Children =
            {
                new TextBlock { Text = prompt },
                textBox,
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 10,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Children =
                    {
                        okButton,
                        cancelButton
                    }
                }
                }
                }
            };

            okButton.Click += (_, _) =>
            {
                result = textBox.Text;
                dialog.Close();
            };

            cancelButton.Click += (_, _) => dialog.Close();

            var mainWindow =
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                ?.MainWindow;

            if (mainWindow != null)
                await dialog.ShowDialog(mainWindow);
            else
                dialog.Show();

            return result;
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            var window = new Window
            {
                Title = title,
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 10,
                    Children =
            {
                new TextBlock { Text = message },
                okButton
                }
                }
            };

            okButton.Click += (_, _) => window.Close();

            var mainWindow =
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                ?.MainWindow;

            if (mainWindow != null)
                await window.ShowDialog(mainWindow);
            else
                window.Show();
        }

        [RelayCommand] private void Cancel() => GoBack();
        private void GoBack() => MainWindowViewModel.Instance.CurrentViewModel = new AdminUsersScreenViewModel(CurrentUser);

        [RelayCommand] public void GoToMainScreen() => MainWindowViewModel.Instance.CurrentViewModel = new AdminMainScreenViewModel(CurrentUser);
        [RelayCommand] public void GoToUsersScreen() => GoBack();
    }
}