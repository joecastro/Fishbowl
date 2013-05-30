//-----------------------------------------------------------------------
// <copyright file="PhotoDisplayControl.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
//     Control used to display and animate a photo.
// </summary>
//-----------------------------------------------------------------------

namespace FacebookClient
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media;
    using ClientManager.Controls;
    using Contigo;
    using Standard;

    [
        TemplatePart(Name = "PART_PhotoImage", Type = typeof(FacebookImageControl)),
        TemplatePart(Name = "PART_ManipulationCanvas", Type = typeof(Canvas)),
        TemplatePart(Name = "PART_PhotoFrameControl", Type = typeof(ContentControl)),
    ]
    public class PhotoDisplayControl : Control
    {
        private Canvas _canvas;
        private ContentControl _frameControl;
        private bool _isImageFit;

        public static readonly DependencyProperty FacebookPhotoProperty = DependencyProperty.Register(
            "FacebookPhoto",
            typeof(FacebookPhoto),
            typeof(PhotoDisplayControl));

        public FacebookPhoto FacebookPhoto
        {
            get { return (FacebookPhoto)GetValue(FacebookPhotoProperty); }
            set { SetValue(FacebookPhotoProperty, value); }
        }

        public static readonly DependencyProperty FitControlProperty = DependencyProperty.Register(
            "FitControl",
            typeof(Control),
            typeof(PhotoDisplayControl),
            new FrameworkPropertyMetadata(
                null,
                (d, e) => ((PhotoDisplayControl)d)._OnFitControlChanged(e)));

        private void _OnFitControlChanged(DependencyPropertyChangedEventArgs e)
        {
            var oldControl = (Control)e.OldValue;
            var newControl = (Control)e.NewValue;

            Utility.RemoveDependencyPropertyChangeListener(oldControl, Control.ActualWidthProperty, _OnFitControlSizeChanged);
            Utility.RemoveDependencyPropertyChangeListener(oldControl, Control.ActualHeightProperty, _OnFitControlSizeChanged);

            if (!IsLoaded)
            {
                return;
            }

            Utility.AddDependencyPropertyChangeListener(newControl, Control.ActualWidthProperty, _OnFitControlSizeChanged);
            Utility.AddDependencyPropertyChangeListener(newControl, Control.ActualHeightProperty, _OnFitControlSizeChanged);
        }

        private void _OnFitControlSizeChanged(object sender, EventArgs e)
        {
            if (_isImageFit || !_IsPhotoOnScreen())
            {
                FitToWindow();
            }
        }

        public Control FitControl
        {
            get { return (Control)GetValue(FitControlProperty); }
            set { SetValue(FitControlProperty, value); }
        }

        private static readonly DependencyProperty FacebookImageControlProperty = DependencyProperty.Register(
            "FacebookImageControl", 
            typeof(FacebookImageControl), 
            typeof(PhotoDisplayControl),
                new FrameworkPropertyMetadata(null,
                    (d, e) => ((PhotoDisplayControl)d)._OnFacebookImageChanged(e)));

        private void _OnFacebookImageChanged(DependencyPropertyChangedEventArgs e)
        {
            var oldControl = (FacebookImageControl)e.OldValue;
            var newControl = (FacebookImageControl)e.NewValue;

            Utility.RemoveDependencyPropertyChangeListener(oldControl, FacebookImageControl.ImageSourceProperty, _OnImageSourceChanged);

            if (!IsLoaded)
            {
                return;
            }

            Utility.AddDependencyPropertyChangeListener(newControl, FacebookImageControl.ImageSourceProperty, _OnImageSourceChanged);
        }

        private void _OnImageSourceChanged(object sender, EventArgs e)
        {
            _NotifyStateChanged();
            FitToWindow();
        }

        private FacebookImageControl FacebookImageControl
        {
            get { return (FacebookImageControl)GetValue(FacebookImageControlProperty); }
            set { SetValue(FacebookImageControlProperty, value); }
        }

        /// <summary>Event that's raised when there has been some change to the way that the photo is being displayed.</summary>
        public event PhotoStateChangedEventHandler PhotoStateChanged;

        public static readonly RoutedCommand ZoomPhotoInCommand = new RoutedCommand("ZoomPhotoIn", typeof(PhotoDisplayControl));
        public static readonly RoutedCommand ZoomPhotoOutCommand = new RoutedCommand("ZooomPhotoOut", typeof(PhotoDisplayControl));
        public static readonly RoutedCommand FitPhotoToWindowCommand = new RoutedCommand("FitPhotoToWindow", typeof(PhotoDisplayControl));

        public PhotoDisplayControl()
        {
            CommandBindings.Add(new CommandBinding(ZoomPhotoInCommand, (sender, e) => _ZoomPhotoIn()));
            CommandBindings.Add(new CommandBinding(ZoomPhotoOutCommand, (sender, e) => _ZoomPhotoOut()));
            CommandBindings.Add(new CommandBinding(FitPhotoToWindowCommand, (sender, e) => FitToWindow()));

            Loaded += (sender, e) =>
            {
                FitPhotoToWindow();

                Utility.AddDependencyPropertyChangeListener(FitControl, Control.ActualWidthProperty, _OnFitControlSizeChanged);
                Utility.AddDependencyPropertyChangeListener(FitControl, Control.ActualHeightProperty, _OnFitControlSizeChanged);

                Utility.AddDependencyPropertyChangeListener(FacebookImageControl, FacebookImageControl.ImageSourceProperty, _OnImageSourceChanged);
            };

            Unloaded += (sender, e) =>
            {
                Utility.RemoveDependencyPropertyChangeListener(FitControl, Control.ActualWidthProperty, _OnFitControlSizeChanged);
                Utility.RemoveDependencyPropertyChangeListener(FitControl, Control.ActualHeightProperty, _OnFitControlSizeChanged);

                Utility.RemoveDependencyPropertyChangeListener(FacebookImageControl, FacebookImageControl.ImageSourceProperty, _OnImageSourceChanged);
            };
        }


        public FacebookImageControl PhotoImage { get; private set; }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            
            PhotoImage = this.Template.FindName("PART_PhotoImage", this) as FacebookImageControl;
            Assert.IsNotNull(PhotoImage);

            _frameControl = this.Template.FindName("PART_PhotoFrameControl", this) as ContentControl;
            Assert.IsNotNull(_frameControl);

            _canvas = this.Template.FindName("PART_ManipulationCanvas", this) as Canvas;
            Assert.IsNotNull(_canvas);

            this.FacebookImageControl = PhotoImage;
        }

        /// <summary>
        /// Fits the currently displayed photo to the window size.
        /// </summary>
        /// <param name="initialFit">Whether this is the initial fit-to-window pass, or as a response to a user click.</param>
        private void FitPhotoToWindow()
        {
            _isImageFit = true;
            if (this.PhotoImage != null && this.PhotoImage.ImageSource != null)
            {
                Assert.IsNotNull(FitControl);
                var mt = (MatrixTransform)FitControl.TransformToVisual(_canvas);
                var boundingRect = new Rect(mt.Matrix.OffsetX, mt.Matrix.OffsetY, FitControl.ActualWidth, FitControl.ActualHeight);

                bool tooTall = false;
                if (boundingRect.Width * PhotoImage.ImageSource.Height > boundingRect.Height * PhotoImage.ImageSource.Width)
                {
                    tooTall = true;
                }

                double aspectRatio = PhotoImage.ImageSource.Width / PhotoImage.ImageSource.Height;

                Matrix matrix = Matrix.Identity;

                if (tooTall)
                {
                    // Center horizontally.
                    PhotoImage.Width = aspectRatio * boundingRect.Height;
                    PhotoImage.Height = boundingRect.Height;

                    matrix.OffsetX = boundingRect.Left + ((boundingRect.Width - PhotoImage.Width) / 2);
                    matrix.OffsetY = boundingRect.Top;
                    //Canvas.SetLeft(_frameControl, boundingRect.Left + ((boundingRect.Width - PhotoImage.Width) / 2));
                    //Canvas.SetTop(_frameControl, boundingRect.Top);
                }
                else
                {
                    // Center vertically.
                    PhotoImage.Height = boundingRect.Width / aspectRatio;
                    PhotoImage.Width = boundingRect.Width;

                    matrix.OffsetX = boundingRect.Left;
                    matrix.OffsetY = boundingRect.Top  + ((boundingRect.Height - PhotoImage.Height) / 2);
                    //Canvas.SetLeft(_frameControl, boundingRect.Left);
                    //Canvas.SetTop(_frameControl, boundingRect.Top  + ((boundingRect.Height - PhotoImage.Height) / 2));
                }

                _frameControl.RenderTransform = new MatrixTransform(matrix);
            }
        }

        /// <summary>
        /// Zooms the currently displayed photo in. 
        /// </summary>
        private void _ZoomPhotoIn()
        {
            _NotifyStateChanged();
            _ResizePhoto(1.25);
        }

        /// <summary>
        /// Zooms the currently displayed photo out.
        /// </summary>
        private void _ZoomPhotoOut()
        {
            _NotifyStateChanged();
            _ResizePhoto(.8);
        }

        public void FitToWindow()
        {
            _NotifyStateChanged();
            FitPhotoToWindow();
        }

        private bool _IsPhotoOnScreen()
        {
            // Get axis aligned bounds of the element
            var elementBounds = _frameControl.RenderTransform.TransformBounds(VisualTreeHelper.GetDescendantBounds(_frameControl));

            // Check to see which bouncing from which boundary
            if (elementBounds.Left >= _canvas.ActualWidth)
            {
                return false;
            }

            if (elementBounds.Right <= 0)
            {
                return false;
            }

            if (elementBounds.Top >= _canvas.ActualHeight)
            {
                return false;
            }

            if (elementBounds.Bottom <= 0)
            {
                return false;
            }

            return true;
        }

        private void _NotifyStateChanged()
        {
            var handler = PhotoStateChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void _MovePhoto(Vector movement)
        {
            _isImageFit = false;

            Transform transform = _frameControl.RenderTransform;
            var matrix = transform == null ? Matrix.Identity : transform.Value;

            matrix.OffsetX += movement.X;
            matrix.OffsetY += movement.Y;

            _frameControl.RenderTransform = new MatrixTransform(matrix);
        }

        private void _ResizePhoto(double scale)
        {
            _isImageFit = false;

            UIElement element = _frameControl;
            Transform transform = element.RenderTransform;
            var matrix = transform == null ? Matrix.Identity : transform.Value;

            // Prevent the image from getting too small or too big.
            if (matrix.M11 < .3 && scale < 1)
            {
                return;
            }

            if (matrix.M11 > 10 && scale > 1)
            {
                return;
            }

            // TODO: When this is coming from the mouse or a command then should make the scaling happen centered
            // based on the cursor position if it's over the frame.
            matrix.ScaleAtPrepend(scale, scale, _frameControl.ActualWidth / 2, _frameControl.ActualHeight / 2);

            element.RenderTransform = new MatrixTransform(matrix);
        }

        #region Multi-touch and mouse interaction logic

        Point? _lastMousePosition;
        int _numTouches = 0;
        int[] _touchId = new int[2];
        Point[] _touchInitialPts = new Point[2];
        Point[] _touchLastPts = new Point[2];
        bool _inZooming = false;

        private void _InitializeMultiTouch()
        {
            var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            // Enable multi-touch input for stylus
            NativeMethods.SetProp(hwndSource.Handle, "MicrosoftTabletPenServiceProperty", new IntPtr(0x01000000));
            SetValue(Stylus.IsFlicksEnabledProperty, false);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            _lastMousePosition = e.GetPosition(_canvas);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            // The additonal null check helps with preventing an accidental drag when
            // double-clicking the caption bar to maximize the window.
            if (e.LeftButton == MouseButtonState.Pressed && _lastMousePosition != null)
            {
                Point newPosition = e.GetPosition(_canvas);
                _MovePhoto(new Vector(newPosition.X - _lastMousePosition.Value.X, newPosition.Y - _lastMousePosition.Value.Y));
                _lastMousePosition = newPosition;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            _lastMousePosition = null;
        }

        protected override void OnPreviewStylusDown(StylusDownEventArgs e)
        {
            if (_numTouches == 0)
            {
                CaptureStylus();
                _inZooming = false;
            }

            if (_numTouches < 2)
            {
                _touchId[_numTouches] = e.StylusDevice.Id;
                _touchInitialPts[_numTouches] = e.GetPosition(_canvas);
                _touchLastPts[_numTouches] = _touchInitialPts[_numTouches];

                ++_numTouches;
            }
            e.Handled = true;
        }

        protected override void OnPreviewStylusMove(StylusEventArgs e)
        {
            base.OnPreviewStylusMove(e);

            if (_numTouches == 1 && _touchId[0] == e.StylusDevice.Id && !_inZooming)
            {
                Point newTouchPosition = e.GetPosition(_canvas);
                _MovePhoto(new Vector(newTouchPosition.X - _touchLastPts[0].X, newTouchPosition.Y - _touchLastPts[0].Y));
                _touchLastPts[0] = newTouchPosition;
            }
            else if (_numTouches == 2 && (_touchId[0] == e.StylusDevice.Id || _touchId[1] == e.StylusDevice.Id))
            {
                int index = _touchId[0] == e.StylusDevice.Id ? 0 : 1;
                Point p0 = e.GetPosition(_canvas);
                Point p1 = _touchLastPts[1 - index];
                double distance = (p0 - p1).Length;
                double scale = distance / (_touchInitialPts[0] - _touchInitialPts[1]).Length;
                if (scale > 0.1 && scale < 20)
                {
                    _ResizePhoto(scale);
                }

                _touchLastPts[index] = p0;
                _inZooming = true;
            }
            e.Handled = true;
        }

        protected override void OnPreviewStylusUp(StylusEventArgs e)
        {
            base.OnPreviewStylusUp(e);

            if (_numTouches > 0)
            {
                if (e.StylusDevice.Id == _touchId[0])
                {
                    _touchId[0] = _touchId[1];
                    _touchInitialPts[0] = _touchInitialPts[1];
                    _touchLastPts[0] = _touchLastPts[1];
                    _numTouches--;
                }
                else if (e.StylusDevice.Id == _touchId[1])
                {
                    _numTouches--;
                }
            }

            if (_numTouches == 0)
            {
                ReleaseStylusCapture();
            }
            e.Handled = true;
        }

        protected override void OnStylusSystemGesture(StylusSystemGestureEventArgs e)
        {
            e.Handled = true;
        }

        #endregion
    }

    /// <summary>
    /// Event handler for when photo changes state.
    /// </summary>
    /// <param name="sender">Event source.</param>
    /// <param name="e">Event args.</param>
    public delegate void PhotoStateChangedEventHandler(object sender, EventArgs e);
}
