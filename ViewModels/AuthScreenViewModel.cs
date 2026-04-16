using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiplomHelpDeskOka.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tmds.DBus.Protocol;

namespace DiplomHelpDeskOka.ViewModels
{
    public partial class AuthScreenViewModel: ViewModelBase
    {
        [ObservableProperty] string _login = "";
        [ObservableProperty] string _password = "";
        [ObservableProperty] string _message = "";


        [RelayCommand]
        public void Enter()
        {
            currentUser = db.Users.Include(r => r.Role).FirstOrDefault(x => x.Login == Login);

            if (currentUser == null)
            {
                Message = "Пользователь не найден";
                return;
            }

            bool isValid = BCrypt.Net.BCrypt.Verify(Password, currentUser.PasswordHash);

            //string hash = BCrypt.Net.BCrypt.HashPassword(Password);

            if (!isValid)
            {
                Message = "Неверный пароль";
                return;
            }

            if (currentUser.IsDeleted)
            {
                Message = "Пользователь удален";
                return;
            }

            currentUser.LastLoginAt = DateTime.UtcNow;
            db.SaveChanges();

            if (currentUser.Role.Id == 1)
            {
                MainWindowViewModel.Instance.CurrentViewModel = new AdminMainScreenViewModel(currentUser);
            }
            else if(currentUser.Role.Id == 2)
            {
                MainWindowViewModel.Instance.CurrentViewModel = new WorkerTicketsScreenViewModel(currentUser);
            }
            else if (currentUser.Role.Id == 3)
            {
                MainWindowViewModel.Instance.CurrentViewModel = new UserTicketsScreenViewModel(currentUser);
            }
        }
    }
}
