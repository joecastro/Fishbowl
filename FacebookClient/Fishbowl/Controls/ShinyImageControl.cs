namespace FacebookClient
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Contigo;

    public class ShinyImageControl : Control
    {
        private static Dictionary<Color, LinearGradientBrush> _gradientBrushes = new Dictionary<Color, LinearGradientBrush>();
        private static Pen _borderPen;
        private static LinearGradientBrush _glareBrush;
        private static ImageBrush _avatarBrush;

        private LinearGradientBrush _gradientBrush;
        private ImageBrush _userImageBrush;
        private Rect brushRect = Rect.Empty;

        public static readonly DependencyProperty FacebookImageProperty = DependencyProperty.Register(
            "FacebookImage",
            typeof(FacebookImage),
            typeof(ShinyImageControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (d, e) => ((ShinyImageControl)d)._UpdateImage()));

        /// <summary>Gets or sets the photo to display.</summary>
        public FacebookImage FacebookImage
        {
            get { return (FacebookImage)GetValue(FacebookImageProperty); }
            set { SetValue(FacebookImageProperty, value); }
        }

        /// <summary>Dependency Property backing store for ImageSource.</summary>
        private static readonly DependencyPropertyKey ImageSourcePropertyKey = DependencyProperty.RegisterReadOnly(
            "ImageSource",
            typeof(ImageSource),
            typeof(ShinyImageControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (d, e) => ((ShinyImageControl)d)._OnImageSourceChanged()));

        public static readonly DependencyProperty ImageSourceProperty = ImageSourcePropertyKey.DependencyProperty;

        /// <summary>
        /// Gets the actual image content to display.
        /// </summary>
        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            protected set { SetValue(ImageSourcePropertyKey, value); }
        }

        private void _OnImageSourceChanged()
        {
            _userImageBrush = null;
        }

        /// <summary>Dependency Property backing store for FacebookImage.</summary>
        public static readonly DependencyProperty FacebookImageDimensionsProperty = DependencyProperty.Register(
            "FacebookImageDimensions",
            typeof(FacebookImageDimensions),
            typeof(ShinyImageControl),
            new UIPropertyMetadata(FacebookImageDimensions.Normal));

        public FacebookImageDimensions FacebookImageDimensions
        {
            get { return (FacebookImageDimensions)GetValue(FacebookImageDimensionsProperty); }
            set { SetValue(FacebookImageDimensionsProperty, value); }
        }

        /// <summary>
        /// SizeBasedOnContent Dependency Property
        /// </summary>
        public static readonly DependencyProperty SizeToContentProperty = DependencyProperty.Register(
            "SizeToContent",
            typeof(SizeToContent),
            typeof(ShinyImageControl),
            new UIPropertyMetadata(
                System.Windows.SizeToContent.Manual,
                (d, e) => ((ShinyImageControl)d)._UpdateImage()));


        public SizeToContent SizeToContent
        {
            get { return (SizeToContent)GetValue(SizeToContentProperty); }
            set { SetValue(SizeToContentProperty, value); }
        }
        /// <summary>
        /// Corner radius to use for rounded rects.
        /// </summary>
        public double CornerRadius { get; set; }

        /// <summary>
        /// The amount of padding to use when framing the Image.
        /// </summary>
        public double ImagePadding
        {
            get { return (double)GetValue(ImagePaddingProperty); }
            set { SetValue(ImagePaddingProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ImagePadding.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ImagePaddingProperty = DependencyProperty.Register(
            "ImagePadding",
            typeof(double),
            typeof(ShinyImageControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Indicates whether the 'glare' effect should be drawn.
        /// </summary>
        public bool ShowGlare { get; set; }

        /// <summary>
        /// Color to use in the gradient behind the user's photo.
        /// </summary>
        public Color ActivityColor
        {
            get { return (Color)GetValue(ActivityColorProperty); }
            set { SetValue(ActivityColorProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ActivityColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ActivityColorProperty = DependencyProperty.Register(
            "ActivityColor",
            typeof(Color),
            typeof(ShinyImageControl),
            new FrameworkPropertyMetadata(
                Colors.White,
                (d, e) => ((ShinyImageControl)d)._OnActivityColorChanged()));

        /// <summary>
        /// Initializes static members of the ShinyImageControl class.
        /// </summary>
        static ShinyImageControl()
        {
            VerticalAlignmentProperty.OverrideMetadata(typeof(ShinyImageControl), new FrameworkPropertyMetadata(VerticalAlignment.Stretch));
            HorizontalAlignmentProperty.OverrideMetadata(typeof(ShinyImageControl), new FrameworkPropertyMetadata(HorizontalAlignment.Stretch));
            RenderOptions.BitmapScalingModeProperty.OverrideMetadata(typeof(ShinyImageControl), new FrameworkPropertyMetadata(BitmapScalingMode.Linear));

            _borderPen = new Pen(Brushes.Silver, 0.8);
            _borderPen.Freeze();

            _glareBrush = new LinearGradientBrush()
            {
                StartPoint = new Point(0.5, 0.4),
                EndPoint = new Point(0.65, 0.85),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(51, 255, 255, 255), 0.0),
                    new GradientStop(Color.FromArgb(51, 255, 255, 255), 0.6),
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.6),
                },
            };

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.DecodePixelWidth = 100;
            bi.UriSource = new Uri(@"pack://application:,,,/Resources/Images/avatar_background.png");
            bi.EndInit();

            _avatarBrush = new ImageBrush(bi);
            _avatarBrush.Freeze();
        }

        public ShinyImageControl()
        {
            _OnActivityColorChanged();
        }

        private void _OnActivityColorChanged()
        {
            if (!_gradientBrushes.TryGetValue(ActivityColor, out _gradientBrush))
            {
                _gradientBrush = _AddKnownGradient(ActivityColor);
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (this.CornerRadius > 0)
            {
                drawingContext.DrawRoundedRectangle(this._gradientBrush, _borderPen, new Rect(0, 0, this.ActualWidth, this.ActualHeight), this.CornerRadius, this.CornerRadius);
            }
            else
            {
                drawingContext.DrawRectangle(this._gradientBrush, _borderPen, new Rect(0, 0, this.ActualWidth, this.ActualHeight));
            }

            double pad = this.ImagePadding;
            double width = this.ActualWidth - 2.0 * pad > 0.0 ? this.ActualWidth - 2.0 * pad : 0.0;
            double height = this.ActualHeight - 2.0 * pad > 0.0 ? this.ActualHeight - 2.0 * pad : 0.0;

            if (brushRect.Width != width || brushRect.Height != height)
            {
                brushRect = new Rect(pad, pad, width, height);
            }

            if (this.ImageSource != null && _userImageBrush == null)
            {
                _userImageBrush = new ImageBrush(this.ImageSource);

                if (this.ImageSource.Height > this.ImageSource.Width)
                {
                    _userImageBrush.Viewport = new Rect(0, 0, 1.0, this.ImageSource.Height / this.ImageSource.Width);
                }
                else if (this.ImageSource.Width > this.ImageSource.Height)
                {
                    _userImageBrush.Viewport = new Rect(0, 0, this.ImageSource.Width / this.ImageSource.Height, 1.0);
                }
            }

            drawingContext.DrawRoundedRectangle(
                _userImageBrush ?? _avatarBrush,
                null,
                brushRect, 
                CornerRadius, 
                CornerRadius);

            if (this.ShowGlare)
            {
                drawingContext.DrawRoundedRectangle(_glareBrush, null, brushRect, this.CornerRadius, this.CornerRadius);
            }
        }

        /// <summary>
        /// Adds a new known gradient to the linear gradient cache.
        /// </summary>
        private static LinearGradientBrush _AddKnownGradient(Color color)
        {
            var lgb = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = 
                {
                    new GradientStop(Colors.White, 0.0),
                    new GradientStop(color, 1.0),
                }
            };
            lgb.Freeze();

            _gradientBrushes.Add(color, lgb);
            return lgb;
        }

        private void _UpdateImage()
        {
            if (FacebookImage != null)
            {
                FacebookImage.GetImageAsync(this.FacebookImageDimensions, _OnGetImageSourceCompleted);
            }
            else
            {
                ImageSource = null;
                InvalidateVisual();
            }
        }

        private void _OnGetImageSourceCompleted(object sender, GetImageSourceCompletedEventArgs e)
        {
            if (sender != FacebookImage)
            {
                return;
            }

            if (e.Error == null && !e.Cancelled)
            {
                this.ImageSource = e.ImageSource;
                if (SizeToContent != SizeToContent.Manual)
                {
                    // Not bothering to detect more granular values for SizeToContent.
                    SetValue(WidthProperty, e.NaturalSize.Value.Width);
                    SetValue(HeightProperty, e.NaturalSize.Value.Height);
                }
            }
            else
            {
                this.ImageSource = null;
            }
            this.InvalidateVisual();
        }
    }
}
