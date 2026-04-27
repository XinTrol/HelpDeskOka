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
    public partial class UserAddOrEditTicketsScreenViewModel : ViewModelBase
    {
        private readonly long? _editingTicketId;
        private bool _isLoading = false;
        private long? _originalResponsibleUserId;

        [ObservableProperty] private User _currentUser;

        [ObservableProperty] private string _authorName = "";
        [ObservableProperty] private string _ticketTitle = "";
        [ObservableProperty] private string _description = "";
        [ObservableProperty] private DateTimeOffset? _plannedDate;
        [ObservableProperty] private TimeSpan? _plannedTime;
        [ObservableProperty] private string _closedAtDisplay = "Не закрыта";
        [ObservableProperty] private DateTime? _createdAt;

        [ObservableProperty] private ObservableCollection<Comment> _comments = new();
        [ObservableProperty] private string _newCommentText = "";

        [ObservableProperty] private TicketType? _selectedType;
        [ObservableProperty] private Status? _selectedStatus;
        [ObservableProperty] private Priority? _selectedPriority;
        [ObservableProperty] private Department? _selectedDepartment;
        [ObservableProperty] private User? _selectedResponsibleUser;

        [ObservableProperty] private ObservableCollection<TicketType> _ticketTypes = new();
        [ObservableProperty] private ObservableCollection<Status> _statuses = new();
        [ObservableProperty] private ObservableCollection<Priority> _priorities = new();
        [ObservableProperty] private ObservableCollection<Department> _departments = new();
        [ObservableProperty] private ObservableCollection<User> _responsibleUsers = new();

        [ObservableProperty] private string _errorMessage = "";

        public bool IsEditMode => _editingTicketId.HasValue;
        public string Title => IsEditMode ? "Редактирование заявки" : "Создание заявки";
        public string SaveButtonText => IsEditMode ? "Сохранить" : "Создать";

        // Права пользователя:
        // - Редактировать можно только пока заявка не назначена (ResponsibleUser == null)
        // - Статус, ответственный, плановая дата – всегда только для чтения
        private bool CanEdit => !IsEditMode || _originalResponsibleUserId == null;
        public bool IsTitleEnabled => CanEdit;
        public bool IsDescriptionEnabled => CanEdit;
        public bool IsTypeEnabled => CanEdit;
        public bool IsDepartmentEnabled => CanEdit;
        public bool IsPriorityEnabled => CanEdit;
        public bool IsPlannedDateEnabled => false;   // Пользователь не может менять плановую дату
        public bool IsStatusEnabled => false;        // Статус только для чтения
        public bool IsResponsibleUserEnabled => false; // Ответственного не назначает

        public UserAddOrEditTicketsScreenViewModel(User currentUser, long? ticketId = null)
        {
            CurrentUser = currentUser;
            _editingTicketId = ticketId;
            _ = LoadDataAsync();
        }

        partial void OnSelectedDepartmentChanged(Department? value)
        {
            if (_isLoading || !IsEditMode) return;
            if (value == null)
            {
                ResponsibleUsers.Clear();
                SelectedResponsibleUser = null;
                return;
            }
            _ = LoadUsersByDepartment(value.Id);
        }

        private async Task LoadUsersByDepartment(long departmentId)
        {
            var users = await db.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted && u.DepartmentId == departmentId)
                .ToListAsync();
            ResponsibleUsers = new ObservableCollection<User>(users);
        }

        private async Task LoadDataAsync()
        {
            _isLoading = true;
            try
            {
                TicketTypes = new(await db.TicketTypes.AsNoTracking().ToListAsync());
                Statuses = new(await db.Statuses.AsNoTracking().ToListAsync());
                Priorities = new(await db.Priorities.AsNoTracking().ToListAsync());
                Departments = new(await db.Departments.AsNoTracking().ToListAsync());

                if (IsEditMode)
                {
                    var ticket = await db.Tickets
                        .Include(t => t.Comments).ThenInclude(c => c.User)
                        .Include(t => t.Author)
                        .FirstOrDefaultAsync(t => t.Id == _editingTicketId);
                    if (ticket == null) return;

                    AuthorName = ticket.Author?.FullName ?? "Неизвестен";
                    TicketTitle = ticket.Title;
                    Description = ticket.Description;
                    CreatedAt = ticket.CreatedAt;
                    _originalResponsibleUserId = ticket.ResponsibleUserId;

                    if (ticket.PlannedCompletionDate.HasValue)
                    {
                        var dbDate = ticket.PlannedCompletionDate.Value;
                        PlannedDate = new DateTimeOffset(dbDate.Ticks, TimeSpan.Zero);
                        PlannedTime = dbDate.TimeOfDay;
                    }
                    ClosedAtDisplay = ticket.ClosedAt?.ToString("dd.MM.yyyy HH:mm") ?? "Не закрыта";
                    SelectedType = TicketTypes.FirstOrDefault(x => x.Id == ticket.TicketTypeId);
                    SelectedStatus = Statuses.FirstOrDefault(x => x.Id == ticket.StatusId);
                    SelectedPriority = Priorities.FirstOrDefault(x => x.Id == ticket.PriorityId);
                    var targetDepartment = Departments.FirstOrDefault(x => x.Id == ticket.DepartmentId);
                    if (targetDepartment != null)
                        await LoadUsersByDepartment(targetDepartment.Id);
                    SelectedDepartment = targetDepartment;
                    SelectedResponsibleUser = ResponsibleUsers.FirstOrDefault(u => u.Id == ticket.ResponsibleUserId);

                    var loadedComments = ticket.Comments.OrderBy(c => c.CreatedAt).ToList();
                    foreach (var c in loadedComments)
                        if (c.CreatedAt.Kind == DateTimeKind.Utc)
                            c.CreatedAt = c.CreatedAt.ToLocalTime();
                    Comments = new ObservableCollection<Comment>(loadedComments);
                }
                else
                {
                    AuthorName = CurrentUser.FullName;
                    // Для новой заявки: статус "Новая", ответственный null
                    SelectedStatus = Statuses.FirstOrDefault(s => s.Name == "Новая" || s.Id == 1);
                    SelectedPriority = Priorities.FirstOrDefault(p => p.Name == "Средний" || p.Id == 2);
                    SelectedResponsibleUser = null;
                    PlannedDate = null;
                }

                // Обновляем состояние блокировки
                OnPropertyChanged(nameof(IsTitleEnabled));
                OnPropertyChanged(nameof(IsDescriptionEnabled));
                OnPropertyChanged(nameof(IsTypeEnabled));
                OnPropertyChanged(nameof(IsDepartmentEnabled));
                OnPropertyChanged(nameof(IsPriorityEnabled));
                OnPropertyChanged(nameof(IsPlannedDateEnabled));
                OnPropertyChanged(nameof(IsStatusEnabled));
                OnPropertyChanged(nameof(IsResponsibleUserEnabled));
            }
            finally
            {
                _isLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddComment()
        {
            if (!IsEditMode)
            {
                ErrorMessage = "Сначала создайте заявку";
                return;
            }
            if (string.IsNullOrWhiteSpace(NewCommentText))
            {
                ErrorMessage = "Введите текст";
                return;
            }

            var dbComment = new Comment
            {
                TicketId = _editingTicketId!.Value,
                UserId = CurrentUser.Id,
                Text = NewCommentText,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await db.Comments.AddAsync(dbComment);
            await db.SaveChangesAsync();

            var uiComment = new Comment
            {
                Id = dbComment.Id,
                TicketId = dbComment.TicketId,
                UserId = dbComment.UserId,
                Text = dbComment.Text,
                CreatedAt = dbComment.CreatedAt.ToLocalTime(),
                UpdatedAt = dbComment.UpdatedAt,
                User = CurrentUser
            };
            Comments.Add(uiComment);
            NewCommentText = "";
            ErrorMessage = "";
        }

        [RelayCommand]
        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(TicketTitle))
            {
                ErrorMessage = "Введите название";
                return;
            }

            DateTime? plannedDateTime = null;
            if (PlannedDate.HasValue)
            {
                var uiDate = PlannedDate.Value.DateTime;
                var uiTime = PlannedTime ?? TimeSpan.Zero;
                var finalDate = uiDate.Date + uiTime;
                plannedDateTime = DateTime.SpecifyKind(finalDate, DateTimeKind.Unspecified);
            }

            if (IsEditMode)
            {
                var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == _editingTicketId);
                if (ticket == null) return;

                bool assigned = ticket.ResponsibleUserId != null;
                if (!assigned)
                {
                    // Только если заявка не назначена – можно менять основные поля
                    ticket.Title = TicketTitle;
                    ticket.Description = Description;
                    ticket.TicketTypeId = SelectedType!.Id;
                    ticket.DepartmentId = SelectedDepartment!.Id;
                    ticket.PriorityId = SelectedPriority!.Id;
                }
                // Плановая дата – пользователь не меняет, поэтому игнорируем plannedDateTime
                // Статус и ответственный – пользователь не меняет
                ticket.UpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
            }
            else
            {
                var nowUnspecified = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
                var newTicket = new Ticket
                {
                    Title = TicketTitle,
                    Description = Description,
                    PlannedCompletionDate = null, // Пользователь не задаёт плановую дату
                    TicketTypeId = SelectedType!.Id,
                    StatusId = SelectedStatus!.Id, // "Новая"
                    PriorityId = SelectedPriority!.Id,
                    DepartmentId = SelectedDepartment!.Id,
                    AuthorId = CurrentUser.Id,
                    ResponsibleUserId = null,
                    CreatedAt = nowUnspecified,
                    UpdatedAt = nowUnspecified
                };
                await db.Tickets.AddAsync(newTicket);
            }

            await db.SaveChangesAsync();
            GoBack();
        }

        [RelayCommand]
        private void Cancel() => GoBack();

        private void GoBack()
        {
            MainWindowViewModel.Instance.CurrentViewModel = new UserTicketsScreenViewModel(CurrentUser);
        }
    }
}