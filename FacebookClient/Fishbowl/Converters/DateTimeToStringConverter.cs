//-----------------------------------------------------------------------
// <copyright file="DateTimeToStringConverter.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
//      Converts a Date and Time into a string for display.
// </summary>
//-----------------------------------------------------------------------

namespace FacebookClient
{
    using System;
    using System.Globalization;
    using System.Windows.Data;

    /// <summary>
    /// Converts a Date and Time into a string for display.
    /// </summary>
    public class DateTimeToStringConverter : IValueConverter
    {
        #region IValueConverter Members

        /// <summary>
        /// Converts a DateTime into a string.
        /// </summary>
        /// <param name="value">The DateTime to convert.</param>
        /// <param name="targetType">The target type of the conversion.</param>
        /// <param name="parameter">The conversion parameter.</param>
        /// <param name="culture">The conversion culture.</param>
        /// <returns>A string representation of the provided date and time.</returns>
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string dateTimeString = string.Empty;
            var now = DateTime.Now;
            DateTime inputDateTime = DateTime.MinValue;

            if (value != null)
            {
                if (DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out inputDateTime))
                {
                    if (inputDateTime.Day != now.Day)
                    {
                        if (inputDateTime.Day == now.Subtract(TimeSpan.FromDays(1)).Day)
                        {
                            dateTimeString = "yesterday ";
                        }
                        else
                        {
                            dateTimeString = inputDateTime.ToString("M/d/yy") + " ";
                        }
                    }

                    dateTimeString += inputDateTime.ToString("h:mm");
                    dateTimeString += " " + inputDateTime.ToString("tt").ToLower();
                }
            }

            return dateTimeString;
        }

        /// <summary>
        /// Converts a date and time string into a DateTime object.  Not implemented.
        /// </summary>
        /// <param name="value">The string to convert.</param>
        /// <param name="targetType">The target type of the conversion.</param>
        /// <param name="parameter">The conversion parameter.</param>
        /// <param name="culture">The conversion culture.</param>
        /// <returns>A DateTime object of the provided string.  Not implemented.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
