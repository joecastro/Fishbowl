
namespace FacebookClient
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;
    using Standard;

    public class UIElementToImageSourceConverter : IValueConverter
    {
        private static readonly SizeConverter _SizeConverter = new SizeConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Verify.IsNotNull(value, "value");
            Verify.IsNotNull(parameter, "parameter");

            var element = (UIElement)value;
            var size = (Size)_SizeConverter.ConvertFromString((string)parameter);

            return Utility.GenerateBitmapSource(element, size.Width, size.Height, true);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
