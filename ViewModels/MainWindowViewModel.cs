using CommunityToolkit.Mvvm.ComponentModel;
using DiplomHelpDeskOka.Models;
using System.Collections.Generic;
using System.Data.Common;

namespace DiplomHelpDeskOka.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public static MainWindowViewModel Instance { get; set; }

        [ObservableProperty]
        private ViewModelBase currentViewModel = new AuthScreenViewModel();

        public MainWindowViewModel()
        {
            Instance = this;
        }
    }
}
