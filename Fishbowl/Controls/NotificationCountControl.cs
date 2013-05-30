
namespace FacebookClient
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using Standard;

    public class NotificationCountControl : Control
    {
        public static readonly DependencyProperty DisplayCountProperty = DependencyProperty.Register(
            "DisplayCount",
            typeof(int), 
            typeof(NotificationCountControl),
            new UIPropertyMetadata(0,
                (d, e) => ((NotificationCountControl)d)._OnDisplayCountChanged()));

        public int DisplayCount
        {
            get { return (int)GetValue(DisplayCountProperty); }
            set { SetValue(DisplayCountProperty, value); }
        }

        private static readonly DependencyPropertyKey ImageSourcePropertyKey = DependencyProperty.RegisterReadOnly(
            "ImageSource", 
            typeof(ImageSource), 
            typeof(NotificationCountControl),
            new PropertyMetadata(null));

        public static readonly DependencyProperty ImageSourceProperty = ImageSourcePropertyKey.DependencyProperty;

        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            private set { SetValue(ImageSourcePropertyKey, value); }
        }

        private void _OnDisplayCountChanged()
        {
            _UpdateImageSource();
        }

        private void _UpdateImageSource()
        {
            if (DisplayCount == 0)
            {
                ImageSource = null;
            }
            else
            {
                // TODO: Switch this to XAML so we can easily, properly pick up theme resources.
                var element = new Grid
                {
                    Width = SystemParameters.SmallIconWidth,
                    Height = SystemParameters.SmallIconHeight,
                    Children =
                    {
                        new Ellipse
                        {
                            Fill = Background
                        },
                        new Viewbox
                        {
                            Margin = new Thickness(0,2,0,2),
                            Child = new TextBlock
                            {
                                Foreground = Brushes.White,
                                FontSize = 9,
                                Text = DisplayCount.ToString(),
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                // Duplicated from CommonFontResources rather than dynamically looked up.
                                FontFamily = new FontFamily("Segoe UI")
                            },
                        },
                    },
                };

                ImageSource = Utility.GenerateBitmapSource(element, (int)element.Width, (int)element.Height, true);
            }
        }
    }
}
