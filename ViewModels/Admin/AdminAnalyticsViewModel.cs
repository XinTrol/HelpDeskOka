using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiplomHelpDeskOka.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiplomHelpDeskOka.ViewModels
{
    public partial class AdminAnalyticsViewModel : ViewModelBase
    {
        private readonly User _currentUser;

        [ObservableProperty] private User currentUser;

        // KPI
        [ObservableProperty] private int totalTickets;
        [ObservableProperty] private double avgCloseTimeDays;
        [ObservableProperty] private int overdueCount;
        [ObservableProperty] private double overduePercent;
        [ObservableProperty] private double onTimePercent;

        [ObservableProperty] private int selectedPeriodIndex = 1;

        // Графики
        [ObservableProperty] private ISeries[] ticketsTrendSeries = Array.Empty<ISeries>();
        [ObservableProperty] private ISeries[] departmentSeries = Array.Empty<ISeries>();
        [ObservableProperty] private ISeries[] responseTimeSeries = Array.Empty<ISeries>();
        [ObservableProperty] private ISeries[] statusPieSeries = Array.Empty<ISeries>();

        [ObservableProperty] private Axis[] xAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] yAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] departmentXAxes = Array.Empty<Axis>();

        public AdminAnalyticsViewModel(User currentUser)
        {
            _currentUser = currentUser;
            CurrentUser = currentUser;

            InitializeAxes();
            _ = LoadAnalyticsAsync();
        }

        private void InitializeAxes()
        {
            XAxes = new[] { new Axis { Name = "Период" } };
            YAxes = new[] { new Axis { Name = "Количество" } };
        }

        partial void OnSelectedPeriodIndexChanged(int value)
        {
            _ = LoadAnalyticsAsync();
        }

        private async Task LoadAnalyticsAsync()
        {
            if (db == null) return;

            try
            {
                DateTime startDate = SelectedPeriodIndex switch
                {
                    0 => DateTime.Now.AddDays(-7),
                    1 => DateTime.Now.AddMonths(-1),
                    2 => DateTime.Now.AddMonths(-3),
                    3 => DateTime.Now.AddYears(-1),
                    _ => DateTime.Now.AddMonths(-1)
                };

                var tickets = await db.Tickets
                    .Include(t => t.Status)
                    .Include(t => t.Department)
                    .Where(t => t.CreatedAt >= startDate)
                    .ToListAsync();

                var closedTickets = tickets.Where(t => t.ClosedAt.HasValue).ToList();

                // KPI
                TotalTickets = tickets.Count;

                AvgCloseTimeDays = closedTickets.Any()
                    ? Math.Round(closedTickets.Average(t => (t.ClosedAt!.Value - t.CreatedAt).TotalDays), 1)
                    : 0;

                OverdueCount = tickets.Count(t => t.ClosedAt == null &&
                                               t.PlannedCompletionDate.HasValue &&
                                               t.PlannedCompletionDate.Value < DateTime.Now);

                OverduePercent = TotalTickets > 0 ? Math.Round(OverdueCount * 100.0 / TotalTickets, 1) : 0;

                OnTimePercent = closedTickets.Any()
                    ? Math.Round(closedTickets.Count(t => t.PlannedCompletionDate.HasValue &&
                                                       t.ClosedAt!.Value <= t.PlannedCompletionDate.Value)
                                 * 100.0 / closedTickets.Count, 1)
                    : 0;

                // ==================== ГРАФИКИ ====================

                // 1. Динамика заявок по дням
                var trendData = tickets
                    .GroupBy(t => t.CreatedAt.Date)
                    .OrderBy(g => g.Key)
                    .ToList();

                TicketsTrendSeries = new ISeries[]
                {
                    new LineSeries<int>
                    {
                        Values = trendData.Select(g => g.Count()).ToArray(),
                        Name = "Заявки",
                        Stroke = new SolidColorPaint(SKColors.OrangeRed, 3),
                        Fill = new SolidColorPaint(SKColors.Orange.WithAlpha(60)),
                        GeometrySize = 5
                    }
                };

                XAxes[0].Labels = trendData.Select(g => g.Key.ToString("dd.MM")).ToList();

                // 2. Заявки по отделам
                var deptData = tickets
                    .GroupBy(t => t.Department?.Name ?? "Без отдела")
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                DepartmentSeries = new ISeries[]
                {
                    new ColumnSeries<int>
                    {
                        Values = deptData.Select(x => x.Count).ToArray(),
                        Name = "Заявки",
                        Fill = new SolidColorPaint(SKColors.CornflowerBlue)
                    }
                };

                DepartmentXAxes = new[]
                {
                    new Axis
                    {
                        Labels = deptData.Select(x => x.Name).ToList(),
                        LabelsRotation = 45
                    }
                };

                // 3. Распределение по статусам (Pie)
                var statusData = tickets
                    .GroupBy(t => t.Status?.Name ?? "Неизвестно")
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .ToList();

                StatusPieSeries = statusData.Select(s => new PieSeries<int>
                {
                    Values = new[] { s.Count },
                    Name = s.Name,
                    InnerRadius = 65
                }).ToArray();

                // 4. Среднее время реакции по отделам (реальный расчёт)
                var responseData = tickets
                    .Where(t => t.ClosedAt.HasValue)
                    .GroupBy(t => t.Department?.Name ?? "Без отдела")
                    .Select(g => new
                    {
                        Department = g.Key,
                        AvgDays = Math.Round(g.Average(t => (t.ClosedAt!.Value - t.CreatedAt).TotalDays), 1)
                    })
                    .OrderBy(x => x.AvgDays)
                    .ToList();

                ResponseTimeSeries = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = responseData.Select(x => x.AvgDays).ToArray(),
                        Name = "Дней до закрытия",
                        Fill = new SolidColorPaint(SKColors.MediumSeaGreen)
                    }
                };

                // Подписи отделов для графика (можно привязать отдельно при необходимости)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Analytics Error]: {ex.Message}");
            }
        }

        // Навигация
        [RelayCommand] private void GoToMain() => MainWindowViewModel.Instance.CurrentViewModel = new AdminMainScreenViewModel(_currentUser);
        [RelayCommand] private void GoToUsers() => MainWindowViewModel.Instance.CurrentViewModel = new AdminUsersScreenViewModel(_currentUser);
        [RelayCommand] private void GoToTickets() => MainWindowViewModel.Instance.CurrentViewModel = new AdminTicketsScreenViewModel(_currentUser);
        [RelayCommand] private void Logout() => MainWindowViewModel.Instance.CurrentViewModel = new AuthScreenViewModel();
    }
}