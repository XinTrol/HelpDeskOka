using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace DiplomHelpDeskOka.Converters
{
    public class BoolToStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 🔥 БЕЗОПАСНАЯ ПРОВЕРКА: Проверяем, что это именно bool
            if (value is bool isDeleted)
            {
                return isDeleted ? Brushes.Red : Brushes.Green;
            }

            // Значение по умолчанию (если null или ошибка)
            return Brushes.Green;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}