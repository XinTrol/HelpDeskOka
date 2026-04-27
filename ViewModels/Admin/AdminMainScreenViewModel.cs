using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiplomHelpDeskOka.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DiplomHelpDeskOka.ViewModels
{
    public partial class AdminMainScreenViewModel : ViewModelBase
    {
        [ObservableProperty]
        private User _currentUser;

        [ObservableProperty]
        private int _overdueCount;

        [ObservableProperty]
        private int _newCount;

        [ObservableProperty]
        private int _inProgressCount;

        [ObservableProperty]
        private int _closedCount;

        // 0 = Неделя, 1 = Месяц, 2 = Год
        [ObservableProperty]
        private int _selectedFilterIndex = 0;

        [RelayCommand]
        private void Logout()
        {
            MainWindowViewModel.Instance.CurrentViewModel =
                new AuthScreenViewModel(); // или твоя LoginScreenViewModel
        }

        private DateTime _currentFilterStartDate;

        [ObservableProperty]
        private ObservableCollection<string> _recentEvents = new();

        public string TodayText => $"Сегодня: {DateTime.Now:dd MMMM yyyy}";

        public AdminMainScreenViewModel(User user)
        {
            CurrentUser = user;
            // 🔥 ИСПРАВЛЕНИЕ: Запускаем инициализацию последовательно, чтобы не было конфликта db-контекста
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadMetricsAsync();
            await LoadRecentEventsAsync();
        }

        partial void OnSelectedFilterIndexChanged(int value)
        {
            _ = LoadMetricsAsync();
        }

        private async Task LoadMetricsAsync()
        {
            if (db == null) return;

            try
            {
                DateTime startDate = _selectedFilterIndex switch
                {
                    0 => DateTime.SpecifyKind(DateTime.Now.AddDays(-7), DateTimeKind.Unspecified),  // Неделя
                    1 => new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0, DateTimeKind.Unspecified), // Месяц
                    2 => new DateTime(DateTime.Now.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), // Год
                    _ => DateTime.SpecifyKind(DateTime.Now.AddYears(-50), DateTimeKind.Unspecified)
                };

                _currentFilterStartDate = startDate;

                var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

                var tickets = await db.Tickets
                    .Include(t => t.Status)
                    .AsNoTracking()
                    .Where(t => t.CreatedAt >= startDate)
                    .ToListAsync();

                OverdueCount = tickets.Count(t =>
                    t.ClosedAt == null &&
                    t.PlannedCompletionDate.HasValue &&
                    t.PlannedCompletionDate.Value < now);

                NewCount = tickets.Count(t => t.Status?.Name == "новая");
                InProgressCount = tickets.Count(t => t.Status?.Name == "в работе");
                ClosedCount = tickets.Count(t => t.Status?.Name == "закрыта");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadMetrics] Ошибка: {ex.Message}");
            }
        }

        private async Task LoadRecentEventsAsync()
        {
            if (db == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RecentEvents.Clear();
                    RecentEvents.Add("• Ошибка доступа к базе данных");
                });
                return;
            }

            try
            {
                // 🔥 УБРАЛ Include(h => h.Ticket) - это лишнее, нам нужен только ID
                var histories = await db.TicketHistories
                    .OrderByDescending(h => h.ChangeDate)
                    .Take(8)
                    .AsNoTracking()
                    .ToListAsync();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RecentEvents.Clear();

                    if (!histories.Any())
                    {
                        RecentEvents.Add("• Пока нет недавних событий");
                        return;
                    }

                    foreach (var h in histories)
                    {
                        string text = h.FieldName.ToLower() switch
                        {
                            "status" or "statusid" => $"Заявка #{h.TicketId} — статус изменён на «{h.NewValue}»",
                            "responsibleuserid" or "responsibleuser" => $"Заявка #{h.TicketId} — назначен новый ответственный",
                            "closedat" => $"Заявка #{h.TicketId} — закрыта",
                            "title" => $"Заявка #{h.TicketId} — изменено название",
                            _ => $"Заявка #{h.TicketId} — изменено поле {h.FieldName}"
                        };

                        RecentEvents.Add($"• {text}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadRecentEvents] Ошибка: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RecentEvents.Clear();
                    RecentEvents.Add("• Ошибка загрузки событий");
                });
            }
        }

        // ==================== КОМАНДЫ ====================

        [RelayCommand]
        public void GoToUsers()
        {
            MainWindowViewModel.Instance.CurrentViewModel = new AdminUsersScreenViewModel(CurrentUser);
        }

        [RelayCommand]
        public void GoToTicketsScreen()
        {
            MainWindowViewModel.Instance.CurrentViewModel = new AdminTicketsScreenViewModel(CurrentUser);
        }

        [RelayCommand]
        public void GoToFilteredTickets(string filterType)
        {
            MainWindowViewModel.Instance.CurrentViewModel =
                new AdminTicketsScreenViewModel(CurrentUser, filterType, _currentFilterStartDate);
        }

        [RelayCommand]
        private async Task AddUser()
        {
            MainWindowViewModel.Instance.CurrentViewModel = new AddOrEditUserScreenViewModel(CurrentUser);
        }

        [RelayCommand]
        private void CreateTicket()
        {
            MainWindowViewModel.Instance.CurrentViewModel = new AddOrEditTicketsScreenViewModel(CurrentUser);
        }
    }
}