
namespace FacebookClient
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using Contigo;

    public class PhotoButton : Button
    {
        public static readonly DependencyProperty PhotoProperty = DependencyProperty.Register(
            "Photo",
            typeof(FacebookImage), 
            typeof(PhotoButton),
            new FrameworkPropertyMetadata((FacebookImage)null));

        public FacebookImage Photo
        {
            get { return (FacebookImage)GetValue(PhotoProperty); }
            set { SetValue(PhotoProperty, value); }
        }

    }
}
