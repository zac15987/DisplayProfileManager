using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DisplayProfileManager
{
    /// <summary>
    /// Converter to create a Rect from width and height values
    /// </summary>
    public class RectConverter : IMultiValueConverter
    {
        private static RectConverter _instance;
        public static RectConverter Instance => _instance ?? (_instance = new RectConverter());

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is double width && values[1] is double height)
            {
                return new Rect(0, 0, width, height);
            }
            return new Rect(0, 0, 0, 0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}