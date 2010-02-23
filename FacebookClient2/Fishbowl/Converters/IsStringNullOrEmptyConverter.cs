
namespace FacebookClient
{
    using System;
    using System.Windows.Data;
    using System.Globalization;

    public class IsStringNullOrEmptyConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool ret = string.IsNullOrEmpty(value as string);

            if ((string)parameter == "Inverse")
            {
                ret = !ret;
            }

            return ret;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}
