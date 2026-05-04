using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiplomHelpDeskOka.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Layout;

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

        [ObservableProperty]
        private bool _isExporting = false;

        private DateTime _currentFilterStartDate;

        [ObservableProperty]
        private ObservableCollection<string> _recentEvents = new();

        public string TodayText => $"Сегодня: {DateTime.Now:dd MMMM yyyy}";

        public AdminMainScreenViewModel(User user)
        {
            CurrentUser = user;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadMetricsAsync();
            await LoadRecentEventsAsync();
        }

        // Обновление метрик при смене фильтра (Неделя/Месяц/Год)
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

        // ==================== ЭКСПОРТ В EXCEL ====================
        [RelayCommand]
        private async Task ExportToExcelAsync()
        {
            if (db == null) return;

            IsExporting = true;

            try
            {
                var tickets = await db.Tickets
                    .Include(t => t.Status)
                    .Include(t => t.Priority)           // ← Добавили!
                    .Include(t => t.ResponsibleUser)
                    .Where(t => t.CreatedAt >= _currentFilterStartDate)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                if (!tickets.Any())
                {
                    await ShowMessage("Нет данных для экспорта за выбранный период.");
                    return;
                }

                var periodName = _selectedFilterIndex switch
                {
                    0 => "Неделя",
                    1 => "Месяц",
                    2 => "Год",
                    _ => "Период"
                };

                var defaultFileName = $"Заявки_{periodName}_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx";

                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Сохранить заявки в Excel",
                    InitialFileName = defaultFileName
                };

                saveFileDialog.Filters.Add(new FileDialogFilter
                {
                    Name = "Excel файлы",
                    Extensions = { "xlsx" }
                });

                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                var selectedPath = await saveFileDialog.ShowAsync(mainWindow);

                if (string.IsNullOrEmpty(selectedPath))
                    return;

                await Task.Run(() =>
                {
                    using var workbook = new XLWorkbook();
                    var ws = workbook.Worksheets.Add("Заявки");

                    // Заголовки
                    ws.Cell(1, 1).Value = "№";
                    ws.Cell(1, 2).Value = "Название";
                    ws.Cell(1, 3).Value = "Статус";
                    ws.Cell(1, 4).Value = "Приоритет";
                    ws.Cell(1, 5).Value = "Ответственный";
                    ws.Cell(1, 6).Value = "Дата создания";
                    ws.Cell(1, 7).Value = "План. завершение";
                    ws.Cell(1, 8).Value = "Дата закрытия";

                    int row = 2;
                    foreach (var t in tickets)
                    {
                        ws.Cell(row, 1).Value = t.Id;
                        ws.Cell(row, 2).Value = t.Title;
                        ws.Cell(row, 3).Value = t.Status?.Name ?? "";
                        ws.Cell(row, 4).Value = t.Priority?.Name ?? "";        // ← Исправлено
                        ws.Cell(row, 5).Value = t.ResponsibleUser?.FullName ?? "";
                        ws.Cell(row, 6).Value = t.CreatedAt;
                        ws.Cell(row, 7).Value = t.PlannedCompletionDate;
                        ws.Cell(row, 8).Value = t.ClosedAt;
                        row++;
                    }

                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(selectedPath);
                });

                await ShowMessage($"Файл успешно сохранён!\n{selectedPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Export error: {ex}");
                await ShowMessage("Ошибка при экспорте:\n" + ex.Message);
            }
            finally
            {
                IsExporting = false;
            }
        }

        // ==================== НЕДАВНИЕ СОБЫТИЯ ====================
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
                var histories = await db.TicketHistories
                    .Include(h => h.ChangedByUser)
                    .OrderByDescending(h => h.ChangeDate)
                    .Take(10)
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
                        string user = h.ChangedByUser?.FullName ?? "Неизвестный пользователь";

                        string action = h.FieldName.ToLower() switch
                        {
                            "status" or "statusid" => $"изменил статус на «{h.NewValue}»",
                            "responsibleuserid" or "responsibleuser" or "responsible" => $"назначил ответственным {h.NewValue}",
                            "closedat" => "закрыл заявку",
                            "title" => "изменил название",
                            "description" => "изменил описание",
                            "plannedcompletiondate" => "изменил плановую дату завершения",
                            "priority" => $"изменил приоритет на «{h.NewValue}»",
                            _ => $"изменил поле «{h.FieldName}»"
                        };

                        // === ИСПРАВЛЕНИЕ ВРЕМЕНИ ===
                        DateTime localTime = h.ChangeDate;
                        if (h.ChangeDate.Kind == DateTimeKind.Utc)
                            localTime = h.ChangeDate.ToLocalTime();
                        else if (h.ChangeDate.Kind == DateTimeKind.Unspecified)
                            localTime = DateTime.SpecifyKind(h.ChangeDate, DateTimeKind.Utc).ToLocalTime();

                        string time = localTime.ToString("dd.MM.yyyy HH:mm");

                        RecentEvents.Add($"• {user} {action} в заявке #{h.TicketId} — {time}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadRecentEvents] Ошибка: {ex}");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RecentEvents.Clear();
                    RecentEvents.Add("• Ошибка загрузки событий");
                });
            }
        }

        // ==================== КОМАНДЫ ====================

        [RelayCommand]
        private void Logout() => MainWindowViewModel.Instance.CurrentViewModel = new AuthScreenViewModel();

        [RelayCommand]
        public void GoToUsers() => MainWindowViewModel.Instance.CurrentViewModel = new AdminUsersScreenViewModel(CurrentUser);

        [RelayCommand]
        public void GoToTicketsScreen() => MainWindowViewModel.Instance.CurrentViewModel = new AdminTicketsScreenViewModel(CurrentUser);

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

        // ==================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ====================

        private Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;

            return null;
        }

        private async Task ShowMessage(string message)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var mainWindow = GetMainWindow();

                var dialog = new Window
                {
                    Title = "Информация",
                    Width = 420,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    MaxWidth = 420,
                    MaxHeight = 200,
                    Content = new TextBlock
                    {
                        Text = message,
                        Margin = new Thickness(20),
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

                if (mainWindow != null)
                {
                    // Основной способ — показать как диалог с владельцем
                    await dialog.ShowDialog(mainWindow);
                }
                else
                {
                    // Запасной вариант
                    dialog.Show();
                }
            });
        }
    }
}