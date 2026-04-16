using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace DiplomHelpDeskOka.Converters
{
    public class BoolToStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 🔥 БЕЗОПАСНАЯ ПРОВЕРКА
            if (value is bool isDeleted)
            {
                return isDeleted ? "Заблокирован" : "Активен";
            }

            // Значение по умолчанию
            return "Активен";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}