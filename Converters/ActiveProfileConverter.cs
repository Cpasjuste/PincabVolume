using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PincabVolume.Converters;

/// <summary>[profile id, active profile id] → Visible when they match.</summary>
public sealed class ActiveProfileConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values is [string id, string activeId] && id == activeId
            ? Visibility.Visible
            : Visibility.Hidden;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
