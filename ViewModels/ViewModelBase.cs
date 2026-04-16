using CommunityToolkit.Mvvm.ComponentModel;
using DiplomHelpDeskOka.Models;

namespace DiplomHelpDeskOka.ViewModels
{
    public class ViewModelBase : ObservableObject
    {
        public PpHelp41Context db = new PpHelp41Context();
        public User currentUser;
    }
}