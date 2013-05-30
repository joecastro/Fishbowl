namespace FacebookClient
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Contigo;
    using Standard;

    public class ShinyImageControl : Control
    {
        private static readonly Dictionary<long, LinearGradientBrush> _gradientBrushes = new Dictionary<long, LinearGradientBrush>();
        private static readonly Dictionary<Color, Pen> _borderPens = new Dictionary<Color, Pen>();
        private static readonly LinearGradientBrush _glareBrush;
        private static readonly ImageBrush _avatarBrush;

        private LinearGradientBrush _gradientBrush;
        private ImageBrush _userImageBrush;
        private Rect _brushRect = Rect.Empty;
        private Pen _borderPen;

        private static long _MakeColorGradientKey(Color top, Color bottom)
        {
            return (long)((ulong)(uint)Utility.AlphaRGB(top) << 32 | (ulong)(uint)Utility.AlphaRGB(bottom));
        }

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

        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
            "CornerRadius",
            typeof(CornerRadius),
            typeof(ShinyImageControl),
            new FrameworkPropertyMetadata(default(CornerRadius), FrameworkPropertyMetadataOptions.AffectsRender));

        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }

        public static readonly DependencyProperty ImagePaddingProperty = DependencyProperty.Register(
            "ImagePadding",
            typeof(double),
            typeof(ShinyImageControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double ImagePadding
        {
            get { return (double)GetValue(ImagePaddingProperty); }
            set { SetValue(ImagePaddingProperty, value); }
        }

        public static readonly DependencyProperty ShowGlareProperty = DependencyProperty.Register(
            "ShowGlare",
            typeof(bool),
            typeof(ShinyImageControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Indicates whether the 'glare' effect should be drawn.</summary>
        public bool ShowGlare
        {
            get { return (bool)GetValue(ShowGlareProperty); }
            set { SetValue(ShowGlareProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ActivityColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ActivityColorProperty = DependencyProperty.Register(
            "ActivityColor",
            typeof(Color),
            typeof(ShinyImageControl),
            new FrameworkPropertyMetadata(
                Colors.White,
                (d, e) => ((ShinyImageControl)d)._OnFrameColorChanged()));

        /// <summary>Color to use in the gradient behind the user's photo.</summary>
        public Color ActivityColor
        {
            get { return (Color)GetValue(ActivityColorProperty); }
            set { SetValue(ActivityColorProperty, value); }
        }

        public static readonly DependencyProperty FrameColorProperty = DependencyProperty.Register(
            "FrameColor",
            typeof(Color),
            typeof(ShinyImageControl),
            new FrameworkPropertyMetadata(
                Colors.White,
                (d, e) => ((ShinyImageControl)d)._OnFrameColorChanged()));

        public Color FrameColor
        {
            get { return (Color)GetValue(FrameColorProperty); }
            set { SetValue(FrameColorProperty, value); }
        }

        public static readonly DependencyProperty PenColorProperty = DependencyProperty.Register(
            "PenColor",
            typeof(Color),
            typeof(ShinyImageControl),
            new FrameworkPropertyMetadata(
                Colors.Silver,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (d,e) => ((ShinyImageControl)d)._OnPenColorChanged()));

        public Color PenColor
        {
            get { return (Color)GetValue(PenColorProperty); }
            set { SetValue(PenColorProperty, value); }
        }

        static ShinyImageControl()
        {
            VerticalAlignmentProperty.OverrideMetadata(typeof(ShinyImageControl), new FrameworkPropertyMetadata(VerticalAlignment.Stretch));
            HorizontalAlignmentProperty.OverrideMetadata(typeof(ShinyImageControl), new FrameworkPropertyMetadata(HorizontalAlignment.Stretch));
            RenderOptions.BitmapScalingModeProperty.OverrideMetadata(typeof(ShinyImageControl), new FrameworkPropertyMetadata(BitmapScalingMode.Linear));

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
            _OnFrameColorChanged();
            _OnPenColorChanged();
        }

        private void _OnFrameColorChanged()
        {
            long key = _MakeColorGradientKey(FrameColor, ActivityColor);
            if (!_gradientBrushes.TryGetValue(key, out _gradientBrush))
            {
                var lgb = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = 
                    {
                        new GradientStop(FrameColor, 0.0),
                        new GradientStop(ActivityColor, 1.0),
                    }
                };
                lgb.Freeze();

                _gradientBrushes.Add(key, lgb);
                _gradientBrush = lgb;
            }
        }

        private void _OnPenColorChanged()
        {
            if (!_borderPens.TryGetValue(PenColor, out _borderPen))
            {
                var pen = new Pen(new SolidColorBrush(PenColor), 0.8);
                pen.Freeze();

                _borderPens.Add(PenColor, pen);
                _borderPen = pen;
            }
        }
        
        protected override void OnRender(DrawingContext drawingContext)
        {
            if (this.CornerRadius != default(CornerRadius))
            {
                drawingContext.DrawRoundedRectangle(this._gradientBrush, _borderPen, new Rect(0, 0, this.ActualWidth, this.ActualHeight), this.CornerRadius.TopLeft, this.CornerRadius.TopLeft);
            }
            else
            {
                drawingContext.DrawRectangle(this._gradientBrush, _borderPen, new Rect(0, 0, this.ActualWidth, this.ActualHeight));
            }

            double pad = this.ImagePadding;
            Size size = new Size(
                this.ActualWidth - 2.0 * pad > 0.0 ? this.ActualWidth - 2.0 * pad : 0.0,
                this.ActualHeight - 2.0 * pad > 0.0 ? this.ActualHeight - 2.0 * pad : 0.0);

            if (_brushRect.Size != size)
            {
                _brushRect = new Rect(new Point(pad, pad), size);
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
                _brushRect, 
                CornerRadius.TopLeft, 
                CornerRadius.TopLeft);

            if (this.ShowGlare)
            {
                drawingContext.DrawRoundedRectangle(_glareBrush, null, _brushRect, this.CornerRadius.TopLeft, this.CornerRadius.TopLeft);
            }
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
