using System;
using System.Windows.Data;

namespace FacebookClient
{
    public class EmptyStringToBooleanConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            String s = value as String;
            if (value != null && String.Empty.Equals(value)) return false;
            else return true;

        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }

        #endregion
    }
}
