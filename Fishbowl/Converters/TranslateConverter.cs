#if BING_TRANSLATION_SERVICE

namespace FacebookClient
{
    using System;
    using System.Windows;
    using System.Windows.Data;
    using System.Globalization;
    using ClientManager;

    public class TranslateConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var localeString = parameter as string;
            if (string.IsNullOrEmpty(localeString))
            {
                // We weren't told how to translate it, so just return the original string.
                return text;
            }

            string translatedText = null;

            string[] fromAndTo = localeString.Split(',');
            if (fromAndTo.Length == 1)
            {
                translatedText = ServiceProvider.TranslationService.Translate(text, new CultureInfo(localeString));
            }
            else
            {
                translatedText = ServiceProvider.TranslationService.Translate(text, new CultureInfo(fromAndTo[0]), new CultureInfo(fromAndTo[1]));
            }

            return translatedText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

#endif