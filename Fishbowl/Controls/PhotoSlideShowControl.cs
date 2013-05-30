//-----------------------------------------------------------------------
// <copyright file="PhotoSlideShowControl.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
//     Control used to display a slideshow of photo's with transitions.
// </summary>
//-----------------------------------------------------------------------

namespace FacebookClient
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Threading;
    using ClientManager;
    using Contigo;
    using Standard;
    using TransitionEffects;
    using ClientManager.Controls;

    /// <summary>
    /// Control used to display a slideshow of photo's with transitions.
    /// </summary>
    [TemplatePart(Name = "PART_PhotoHost", Type = typeof(Decorator))]
    public class PhotoSlideShowControl : Control
    {
        public static RoutedCommand ToggleShuffleCommand { get; private set; }

        public static readonly DependencyProperty StartingPhotoProperty = DependencyProperty.Register(
            "StartingPhoto",
            typeof(FacebookPhoto),
            typeof(PhotoSlideShowControl),
            new PropertyMetadata(null));

        public FacebookPhoto StartingPhoto
        {
            get { return (FacebookPhoto)GetValue(StartingPhotoProperty); }
            set { SetValue(StartingPhotoProperty, value); }
        }

        public static readonly DependencyProperty FacebookPhotoCollectionProperty = DependencyProperty.Register(
            "FacebookPhotoCollection",
            typeof(FacebookPhotoCollection),
            typeof(PhotoSlideShowControl),
            new PropertyMetadata(
                null,
                (d, e) => ((PhotoSlideShowControl)d)._OnFacebookPhotoCollectionChanged(e)));

        public FacebookPhotoCollection FacebookPhotoCollection
        {
            get { return (FacebookPhotoCollection)GetValue(FacebookPhotoCollectionProperty); }
            set { SetValue(FacebookPhotoCollectionProperty, value); }
        }

        private void _OnFacebookPhotoCollectionChanged(DependencyPropertyChangedEventArgs e)
        {
            _photos.Clear();
            _shuffledPhotoIndices.Clear();
            _currentChild.FacebookImage = null;
            _oldChild.FacebookImage = null;
            _transitionTimer.Stop();
            SetValue(PausedPropertyKey, false);
            _realCurrentIndex = null;

            var photoCollection = e.NewValue as FacebookPhotoCollection;
            if (photoCollection == null || photoCollection.Count == 0)
            {
                return;
            }

            _photos.AddRange(photoCollection);
            _shuffledPhotoIndices.AddRange(Enumerable.Range(0, photoCollection.Count));
            _shuffledPhotoIndices.Shuffle();
            _realCurrentIndex = 0;

            _currentChild.FacebookImage = _CurrentPhoto.Image;
            _oldChild.FacebookImage = _NextPhoto.Image;

            if (_photoHost != null)
            {
                StartTimer();
            }
        }

        private static readonly DependencyPropertyKey IsStoppedPropertyKey = DependencyProperty.RegisterReadOnly(
            "IsStopped",
            typeof(bool), 
            typeof(PhotoSlideShowControl),
            new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty IsStoppedProperty = IsStoppedPropertyKey.DependencyProperty;

        public bool IsStopped
        {
            get { return (bool)GetValue(IsStoppedProperty); }
            private set { SetValue(IsStoppedPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey IsShuffledPropertyKey = DependencyProperty.RegisterReadOnly(
            "IsShuffled", 
            typeof(bool), 
            typeof(PhotoSlideShowControl),
            new PropertyMetadata(
                false,
                (d,e) => ((PhotoSlideShowControl)d)._OnIsShuffledChanged()));

        public static readonly DependencyProperty IsShuffledProperty = IsShuffledPropertyKey.DependencyProperty;

        public bool IsShuffled
        {
            get { return (bool)GetValue(IsShuffledProperty); }
            private set { SetValue(IsShuffledPropertyKey, value); }
        }

        private void _OnIsShuffledChanged()
        {
            if (_realCurrentIndex == null)
            {
                return;
            }

            if (IsShuffled)
            {
                FacebookPhoto currentPhoto = _photos[_realCurrentIndex.Value];
                int shuffledIndex = _shuffledPhotoIndices.FindIndex(index => _photos[index] == currentPhoto);
                CurrentIndex = shuffledIndex;
            }
            else
            {
                FacebookPhoto currentPhoto = _photos[_shuffledPhotoIndices[_realCurrentIndex.Value]];
                int unshuffledIndex = _photos.FindIndex(photo => photo == currentPhoto);
                CurrentIndex = unshuffledIndex;
            }

            _currentChild.FacebookImage = _CurrentPhoto.Image;
            _oldChild.FacebookImage = _NextPhoto.Image;
        }

        private static readonly DependencyPropertyKey CurrentIndexPropertyKey = DependencyProperty.RegisterReadOnly(
            "CurrentIndex",
            typeof(int),
            typeof(PhotoSlideShowControl),
            new PropertyMetadata(
                0,
                (d, e) => ((PhotoSlideShowControl)d)._OnCurrentIndexChanged(e)));

        public static readonly DependencyProperty CurrentIndexProperty = CurrentIndexPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets or sets the CurrentIndex property.  This dependency property 
        /// indicates ....
        /// </summary>
        public int CurrentIndex
        {
            get { return (int)GetValue(CurrentIndexProperty); }
            private set { SetValue(CurrentIndexPropertyKey, value); }
        }

        private void _OnCurrentIndexChanged(DependencyPropertyChangedEventArgs e)
        {
            if (_photoHost == null)
            {
                _currentChild.FacebookImage = _CurrentPhoto.Image;
                _oldChild.FacebookImage = _NextPhoto.Image;
            }
        }

        private FacebookPhoto _CurrentPhoto
        {
            get
            {
                if (_photos.Count == 0 || _realCurrentIndex == null)
                {
                    return null;
                }
                return _photos[CurrentIndex];
            }
        }

        private FacebookPhoto _NextPhoto
        {
            get
            {
                if (_photos.Count == 0 || _realCurrentIndex == null)
                {
                    return null;
                }

                int index = (_realCurrentIndex.Value + 1) % _photos.Count;
                if (IsShuffled)
                {
                    index = _shuffledPhotoIndices[index];
                }

                return _photos[index];
            }
        }

        private static readonly DependencyPropertyKey PausedPropertyKey = DependencyProperty.RegisterReadOnly(
            "Paused",
            typeof(bool),
            typeof(PhotoSlideShowControl),
            new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty PausedProperty = PausedPropertyKey.DependencyProperty;

        private readonly List<FacebookPhoto> _photos = new List<FacebookPhoto>();
        private readonly List<int> _shuffledPhotoIndices = new List<int>();
        private int? _realCurrentIndex;

        /// <summary>
        /// Control hosting the current slide show image.
        /// </summary>
        private FacebookImageControl _currentChild;

        /// <summary>
        /// Control that temporarily hosts the old slide show image upon transition to the next image.
        /// </summary>
        private FacebookImageControl _oldChild;

        /// <summary>
        /// Decorator that hosts photo controls.
        /// </summary>
        private Decorator _photoHost;

        /// <summary>
        /// Timer to control interval between transitions.
        /// </summary>
        private DispatcherTimer _transitionTimer;

        /// <summary>
        /// Timer to hide the mouse pointer and the slide show controls (play, pause, stop, ...).
        /// </summary>
        private DispatcherTimer _mousePointerTimer;

        /// <summary>
        /// PRNG used to select the next transition to be applied.
        /// </summary>
        private static readonly Random _rand = new Random();

        /// <summary>
        /// The list of all kinds of transition effects that are supported
        /// </summary>
        private static Type[] _transitionEffectTypes;

        /// <summary>
        /// List of the different types of animations that are supported.
        /// </summary>
        private enum AnimationType { None, ZoomIn, ZoomOut, PanLeft, PanRight };

        /// <summary>
        /// The type of the animation currently in progress.
        /// </summary>
        AnimationType currentAnimationType;

        /// <summary>
        /// Maximum Frame Rate for the slide show animation and transition animation
        /// </summary>
        private static int animFrameRate = 20;

        /// <summary>
        /// The FROM and TO values to be used for various types of animation.
        /// </summary>
        private static double scaleAnimFrom = 1.25;
        private static double scaleAnimTo = 1.35;
        private static double transAnimFrom = 0;
        private static double transAnimTo = 35;

        /// <summary>
        /// Defines the total amount of time (in milliseconds) to be used as the animation interval.
        /// </summary>
        private static double totalAnimationPeriod = 6000;

        /// <summary>
        /// The amound of time elapsed while animating the slide show. This is used to when pausing & resuming
        /// the animation, in order to figure out how much time is remaining for animation of the paused slide
        /// before going to the next one.
        /// </summary>
        private double elapsedAnimationPeriod = 0;

        /// <summary>
        /// The global transform group and scale/translate transforms used for animation.
        /// </summary>
        private TransformGroup transformGroup = null;
        private ScaleTransform scaleTransform = null;
        private TranslateTransform translateTransform = null;

        /// <summary>
        /// Holds a reference to the Border object that holds the slide show controls (play, pause, stop, ...).
        /// </summary>
        private Border menuBorder = null;

        /// <summary>
        /// Holds the last location of the mouse pointer.
        /// </summary>
        private Point? lastMousePosition = null;

        /// <summary>
        /// Static constructor for retrieving the list of all kinds of supported transition effects
        /// </summary>
        static PhotoSlideShowControl()
        {
            _transitionEffectTypes = Assembly.GetAssembly(typeof(TransitionEffect)).GetTypes();
            ToggleShuffleCommand = new RoutedCommand("ToggleShuffle", typeof(PhotoSlideShowControl));
        }

        /// <summary>
        /// Initializes a new instance of the PhotoSlideShowControl class.
        /// </summary>
        public PhotoSlideShowControl()
        {
            _currentChild = new FacebookImageControl
            {
                Style = (Style)Application.Current.Resources["SimpleSlideShowImageControlStyle"]
            };
            _oldChild = new FacebookImageControl
            {
                Style = (Style)Application.Current.Resources["SimpleSlideShowImageControlStyle"]
            };

            _transitionTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(totalAnimationPeriod), DispatcherPriority.Input, this.OnTransitionTimerTick, Dispatcher);
            _transitionTimer.Stop();

            _mousePointerTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(totalAnimationPeriod), DispatcherPriority.Input, this.OnMousePointerTimerTick, Dispatcher);
            _mousePointerTimer.Stop();

            currentAnimationType = AnimationType.None;

            transformGroup = new TransformGroup();
            translateTransform = new TranslateTransform(transAnimFrom, transAnimFrom);
            scaleTransform = new ScaleTransform(scaleAnimFrom, scaleAnimFrom);
            transformGroup.Children.Add(this.translateTransform);
            transformGroup.Children.Add(this.scaleTransform);

            this.Loaded += this.OnLoaded;
            this.Unloaded += this.OnUnloaded;

            this.InputBindings.Add(new InputBinding(MediaCommands.Stop, new KeyGesture(Key.Escape)));
            this.InputBindings.Add(new InputBinding(MediaCommands.NextTrack, new KeyGesture(Key.Right)));
            this.InputBindings.Add(new InputBinding(MediaCommands.PreviousTrack, new KeyGesture(Key.Left)));
            this.CommandBindings.Add(new CommandBinding(System.Windows.Input.MediaCommands.TogglePlayPause, new ExecutedRoutedEventHandler(OnPlayPauseCommandExecuted), new CanExecuteRoutedEventHandler(OnPlayPauseCommandCanExecute)));
            this.CommandBindings.Add(new CommandBinding(System.Windows.Input.MediaCommands.Pause, new ExecutedRoutedEventHandler(OnPauseCommandExecuted), new CanExecuteRoutedEventHandler(OnPauseCommandCanExecute)));
            this.CommandBindings.Add(new CommandBinding(System.Windows.Input.MediaCommands.Play, new ExecutedRoutedEventHandler(OnResumeCommandExecuted), new CanExecuteRoutedEventHandler(OnResumeCommandCanExecute)));
            this.CommandBindings.Add(new CommandBinding(System.Windows.Input.MediaCommands.NextTrack, new ExecutedRoutedEventHandler(OnNextSlideCommandExecuted), new CanExecuteRoutedEventHandler(OnNextSlideCommandCanExecute)));
            this.CommandBindings.Add(new CommandBinding(System.Windows.Input.MediaCommands.PreviousTrack, new ExecutedRoutedEventHandler(OnPreviousSlideCommandExecuted), new CanExecuteRoutedEventHandler(OnPreviousSlideCommandCanExecute)));
            this.CommandBindings.Add(new CommandBinding(System.Windows.Input.MediaCommands.Stop, new ExecutedRoutedEventHandler(OnStopCommandExecuted), new CanExecuteRoutedEventHandler(OnStopCommandCanExecute)));
            this.CommandBindings.Add(new CommandBinding(ToggleShuffleCommand, _OnToggleShuffleCommandExecuted, _OnToggleShuffleCommandCanExecute));
        }

        /// <summary>
        /// Gets a value indicating whether slide show is in paused mode or not.
        /// </summary>
        public bool Paused
        {
            get { return (bool)GetValue(PausedProperty); }
        }

        /// <summary>
        /// OnApplyTemplate override
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            this.menuBorder = this.Template.FindName("PART_MenuBorder", this) as Border;
            this._photoHost = this.Template.FindName("PART_PhotoHost", this) as Decorator;

            if (this._photoHost != null)
            {
                this._photoHost.Child = this._currentChild;
                this._photoHost.RenderTransform = this.transformGroup;
                this._photoHost.RenderTransformOrigin = new Point(0.5, 0.5);

                if (_photos.Count > 0)
                {
                    this.StartTimer();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="e">Arguments describing the event.</param>
        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);

            if (lastMousePosition == null)
            {
                lastMousePosition = e.GetPosition(this);

                // Display the slide show controls
                if (this.menuBorder != null)
                {
                    this.menuBorder.BeginAnimation(Border.OpacityProperty, null);
                    this.menuBorder.Opacity = 1.0;
                }

                // Restart the timer that would take away the mouse pointer & slide show controls after a while
                this._mousePointerTimer.Stop();
                this._mousePointerTimer.Start();
            }
            else if (e.GetPosition(this) != lastMousePosition.Value)
            {
                lastMousePosition = e.GetPosition(this);
                this.Cursor = Cursors.Arrow;

                // Display the slide show controls
                if (this.menuBorder != null)
                {
                    this.menuBorder.BeginAnimation(Border.OpacityProperty, null);
                    this.menuBorder.Opacity = 1.0;
                }

                // Restart the timer that would take away the mouse pointer & slide show controls after a while
                this._mousePointerTimer.Stop();
                this._mousePointerTimer.Start();
            }
        }

        /// <summary>
        /// Can execute handler for TogglePlayPause command
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnPlayPauseCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow.Paused)
                {
                    OnResumeCommandCanExecute(sender, e);
                }
                else
                {
                    OnPauseCommandCanExecute(sender, e);
                }

                e.Handled = true;
            }
        }

        /// <summary>
        /// Executed event handler for TogglePlayPause command
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnPlayPauseCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow.Paused)
                {
                    OnResumeCommandExecuted(sender, e);
                }
                else
                {
                    OnPauseCommandExecuted(sender, e);
                }

                e.Handled = true;
            }
        }

        /// <summary>
        /// Can execute handler for Pause command
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnPauseCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow != null)
                {
                    e.CanExecute = !slideShow.Paused;
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Executed event handler for pause command
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnPauseCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow != null)
                {
                    slideShow.StopTimer();
                    slideShow.ClearValue(FrameworkElement.CursorProperty);
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Can execute handler for resume command
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnResumeCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow != null)
                {
                    e.CanExecute = slideShow.Paused;
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Executed event handler for resume command
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnResumeCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow != null)
                {
                    slideShow.StartTimer();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Can execute handler for next slide command
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnNextSlideCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow != null)
                {
                    // Since slide show wraps around, this can always execute
                    e.CanExecute = true;
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Executed event handler for next slide command
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnNextSlideCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow != null)
                {
                    // Stop the timer, change the photo, move to the next photo and restart timer
                    slideShow._MoveNext();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Can execute handler for previous slide command
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnPreviousSlideCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow != null)
                {
                    // Since slide show wraps around, this can always execute
                    e.CanExecute = true;
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Executed event handler for previous slide command
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnPreviousSlideCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow != null)
                {
                    // Stop the timer, change the photo, move to the next photo and restart timer
                    slideShow.MovePrevious();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Can execute handler for stop command
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnStopCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow != null)
                {
                    // Slide show can always stop and navigate to current photo
                    e.CanExecute = true;
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Executed event handler for stop command
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnStopCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow != null)
                {
                    // Stop the timer, change the photo, move to the next photo and restart timer
                    slideShow.NavigateToPhoto();
                    e.Handled = true;

                    slideShow.IsStopped = true;
                }
            }
        }

        private static void _OnToggleShuffleCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow != null)
                {
                    e.CanExecute = true;
                    e.Handled = true;
                }
            }
        }

        private static void _OnToggleShuffleCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoSlideShowControl slideShow = sender as PhotoSlideShowControl;
                if (slideShow != null)
                {
                    // Stop the timer, change the photo, move to the next photo and restart timer
                    slideShow.IsShuffled = !slideShow.IsShuffled;
                    e.Handled = true;
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (this.StartingPhoto != null)
            {
                int startIndex = _photos.FindIndex(photo => photo == StartingPhoto);
                if (startIndex != -1)
                {
                    if (IsShuffled)
                    {
                        startIndex = _shuffledPhotoIndices.FindIndex(i => i == startIndex);
                    }
                    _realCurrentIndex = startIndex;

                    if (IsShuffled)
                    {
                        CurrentIndex = _shuffledPhotoIndices[_realCurrentIndex.Value];
                    }
                    else
                    {
                        CurrentIndex = _realCurrentIndex.Value;
                    }

                    _currentChild.FacebookImage = _CurrentPhoto.Image;
                    _oldChild.FacebookImage = _NextPhoto.Image;
                }
            }
            this.Focus();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            this._currentChild.Effect = null;
            this._transitionTimer.Stop();
            this._mousePointerTimer.Stop();
            this.SetValue(PausedPropertyKey, false);
        }

        /// <summary>
        /// Swaps control displaying current photo with the control for the next photo, enabling transition.
        /// </summary>
        private void SwapChildren()
        {
            FacebookImageControl temp = this._currentChild;
            this._currentChild = this._oldChild;
            this._oldChild = temp;
            this._currentChild.Width = double.NaN;
            this._currentChild.Height = double.NaN;
            if (this._photoHost != null)
            {
                this._photoHost.Child = this._currentChild;
            }

            this._oldChild.Effect = null;
        }

        /// <summary>
        /// Starts timer and resets Paused property
        /// </summary>
        private void StartTimer()
        {
            this._transitionTimer.Start();
            this._mousePointerTimer.Start();
            this.ResumeSlideShowAnimation();
            this.SetValue(PausedPropertyKey, false);
        }

        /// <summary>
        /// Stops timer and sets Paused property
        /// </summary>
        private void StopTimer()
        {
            this._transitionTimer.Stop();
            this._mousePointerTimer.Stop();
            this.PauseSlideShowAnimation();
            this.SetValue(PausedPropertyKey, true);
            this.Cursor = Cursors.Arrow;
        }

        /// <summary>
        /// Applies a random transition effect between current and next slide show images
        /// </summary>
        private void ApplyTransitionEffect()
        {
            DoubleAnimation da = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(totalAnimationPeriod / 5)), FillBehavior.HoldEnd);
            da.AccelerationRatio = 0.5;
            da.DecelerationRatio = 0.5;
            da.Completed += new EventHandler(this.TransitionCompleted);
            // Force the frame rate to animFrameRate instead of WPF's default value of 60fps.
            // this will reduce the CPU load on low-end machines, and will conserve battery for portable devices.
            Timeline.SetDesiredFrameRate(da, animFrameRate);

            VisualBrush vb = new VisualBrush(this._oldChild);
            vb.Viewbox = new Rect(0, 0, this._oldChild.ActualWidth, this._oldChild.ActualHeight);
            vb.ViewboxUnits = BrushMappingMode.Absolute;
            this._oldChild.Width = this._oldChild.ActualWidth;
            this._oldChild.Height = this._oldChild.ActualHeight;
            this._oldChild.Measure(new Size(this._oldChild.ActualWidth, this._oldChild.ActualHeight));
            this._oldChild.Arrange(new Rect(0, 0, this._oldChild.ActualWidth, this._oldChild.ActualHeight));

            TransitionEffect transitionEffect = GetRandomTransitionEffect();
            transitionEffect.OldImage = vb;
            this._currentChild.Effect = transitionEffect;

            transitionEffect.BeginAnimation(TransitionEffect.ProgressProperty, da, HandoffBehavior.SnapshotAndReplace);
        }

        /// <summary>
        /// Randomely picks a transition effect among the ones that are implemented.
        /// </summary>
        /// <returns>A transition effect</returns>
        private TransitionEffect GetRandomTransitionEffect()
        {
            TransitionEffect transitionEffect = null;

            try
            {
                // randomely pick a transition effect that is instantiable
                int idx = 0;
                do
                {
                    idx = _rand.Next(_transitionEffectTypes.Length);
                } while (_transitionEffectTypes[idx].IsAbstract == true);

                transitionEffect = Activator.CreateInstance(_transitionEffectTypes[idx]) as TransitionEffect;
            }
            catch (Exception)
            {
                // in case of any problems, default to Fade transition effect
                transitionEffect = new FadeTransitionEffect();
            }

            return transitionEffect;
        }

        /// <summary>
        /// Advances to next photo. This action stops the timer and puts the slide show in paused mode, slide changes now only take place
        /// through user-initiated action.
        /// </summary>
        private void _MoveNext()
        {
            if (!this.Paused)
            {
                this.StopTimer();
            }

            _IncrementRealCurrentIndex();
            this.ChangePhoto(false);
        }

        private void _IncrementRealCurrentIndex()
        {
            if (_photos.Count > 0)
            {
                _realCurrentIndex = (_realCurrentIndex + 1) % _photos.Count;
                if (IsShuffled)
                {
                    CurrentIndex = _shuffledPhotoIndices[_realCurrentIndex.Value];
                }
                else
                {
                    CurrentIndex = _realCurrentIndex.Value;
                }
            }
        }

        private void _DecrementRealCurrentIndex()
        {
            if (_photos.Count > 0)
            {
                _realCurrentIndex = (_realCurrentIndex + _photos.Count - 1) % _photos.Count;
                if (IsShuffled)
                {
                    CurrentIndex = _shuffledPhotoIndices[_realCurrentIndex.Value];
                }
                else
                {
                    CurrentIndex = _realCurrentIndex.Value;
                }
            }
        }

        /// <summary>
        /// Goes back to previous photo. This action stops the timer and puts the slide show in paused mode, slide changes now only take place
        /// through user-initiated action.
        /// </summary>
        private void MovePrevious()
        {
            if (!this.Paused)
            {
                this.StopTimer();
            }

            _DecrementRealCurrentIndex();
            this.ChangePhoto(false);
        }

        /// <summary>
        /// Stops slide show and navigates to the currently displayed photo.
        /// </summary>
        private void NavigateToPhoto()
        {
            this._transitionTimer.Stop();
            this.SetValue(PausedPropertyKey, false);
            if (_photos.Count > 0)
            {
                FacebookPhoto photo = _photos[CurrentIndex];
                if (ServiceProvider.ViewManager.NavigationCommands.NavigateToContentCommand.CanExecute(photo))
                {
                    ServiceProvider.ViewManager.NavigationCommands.NavigateToContentCommand.Execute(photo);
                }
            }
        }

        /// <summary>
        /// Handler for timer tick - initiates transition to next photo.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Event args describing the event.</param>
        private void OnTransitionTimerTick(object sender, EventArgs e)
        {
            this.ChangePhoto(true);
            _IncrementRealCurrentIndex();

            // If resuming from a paused state, then reset the time interval to its maximum
            if (this._transitionTimer.Interval.Milliseconds != totalAnimationPeriod)
            {
                this._transitionTimer.Interval = TimeSpan.FromMilliseconds(totalAnimationPeriod);
            }
        }

        /// <summary>
        /// Handler for timer tick - takes away the mouse pointer and the slide show controls.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Event args describing the event.</param>
        private void OnMousePointerTimerTick(object sender, EventArgs e)
        {
            this.Cursor = Cursors.None;
            this._mousePointerTimer.Stop();

            if (this.menuBorder != null)
            {
                DoubleAnimation da = new DoubleAnimation(0.0, TimeSpan.FromSeconds(1));
                // Force the frame rate to animFrameRate instead of WPF's default value of 60fps.
                // this will reduce the CPU load on low-end machines, and will conserve battery for portable devices.
                Timeline.SetDesiredFrameRate(da, animFrameRate);
                this.menuBorder.BeginAnimation(Border.OpacityProperty, da, HandoffBehavior.SnapshotAndReplace);
            }
        }

        /// <summary>
        /// If applyTransitionEffect is true, initiates transition animation to next photo. If false, assumes that next photo has been
        /// selected by manually advancing the slide show, and just displays the current photo.
        /// </summary>
        /// <param name="applyTransitionEffect">If true, transition animation and effects are initiated.</param>
        private void ChangePhoto(bool applyTransitionEffect)
        {
            if (_photos.Count > 0 && !this._oldChild.IsImageUpdating)
            {
                if (applyTransitionEffect)
                {
                    this.SwapChildren();
                    this.ApplyTransitionEffect();
                    this.ResumeSlideShowAnimation();
                }
                else
                {
                    // Apply the current slide show content. 
                    // Load the old child with the next photo so it will advance to the next photo if the user resumes play.
                    this._currentChild.FacebookImage = _CurrentPhoto.Image;
                    this._oldChild.FacebookImage = _NextPhoto.Image;
                }
            }
        }

        /// <summary>
        /// Handler for slide transition completed.
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">Event args describing the event.</param>
        private void TransitionCompleted(object sender, EventArgs e)
        {
            _currentChild.Effect = null;
            _oldChild.FacebookImage = _NextPhoto.Image;
        }

        /// <summary>
        /// Pauses the animation of the slide show
        /// </summary>
        private void PauseSlideShowAnimation()
        {
            double scaleValue = this.scaleTransform.ScaleX;
            double transValue = this.translateTransform.X;

            switch (currentAnimationType)
            {
                case AnimationType.ZoomIn:
                case AnimationType.ZoomOut:
                    elapsedAnimationPeriod = (scaleValue - scaleAnimFrom) / (scaleAnimTo - scaleAnimFrom) * totalAnimationPeriod;
                    break;

                case AnimationType.PanLeft:
                case AnimationType.PanRight:
                    elapsedAnimationPeriod = (Math.Abs(transValue) - transAnimFrom) / (transAnimTo - transAnimFrom) * totalAnimationPeriod;
                    break;
            }

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            translateTransform.BeginAnimation(TranslateTransform.XProperty, null);

            scaleTransform.ScaleX = scaleValue;
            scaleTransform.ScaleY = scaleValue;
            translateTransform.X = transValue;

            _transitionTimer.Interval = TimeSpan.FromMilliseconds(totalAnimationPeriod - elapsedAnimationPeriod);
        }

        /// <summary>
        /// Resumes/Starts the animation of the slide show
        /// </summary>
        private void ResumeSlideShowAnimation()
        {
            if (this.Paused == false)
            {
                // The slide show animation was not paused so proceed normally

                AnimationType nextAnimationType = AnimationType.None;

                switch (currentAnimationType)
                {
                    case AnimationType.ZoomIn:
                        nextAnimationType = AnimationType.ZoomOut;
                        break;

                    case AnimationType.ZoomOut:
                        nextAnimationType = AnimationType.PanLeft;
                        break;

                    case AnimationType.PanLeft:
                        nextAnimationType = AnimationType.PanRight;
                        break;

                    case AnimationType.PanRight:
                        nextAnimationType = AnimationType.ZoomIn;
                        break;

                    default:
                        nextAnimationType = AnimationType.ZoomIn;
                        break;
                }

                AnimateSlideShow(nextAnimationType);
                currentAnimationType = nextAnimationType;
            }
            else
            {
                // The previous slide show animation was paused so resume it
                AnimateSlideShow(currentAnimationType);
            }
        }

        /// <summary>
        /// Given a type of animation, it will animate the slide show.
        /// </summary>
        /// <param name="animType">The type of animation to perform (ZoomIn, ZoomOut, PanLeft, PanRight)</param>
        private void AnimateSlideShow(AnimationType animType)
        {
            DoubleAnimation da = new DoubleAnimation();
            da.Duration = this._transitionTimer.Interval;
            // Force the frame rate to animFrameRate instead of WPF's default value of 60fps.
            // this will reduce the CPU load on low-end machines, and will conserve battery for portable devices.
            Timeline.SetDesiredFrameRate(da, animFrameRate);

            switch (animType)
            {
                case AnimationType.ZoomIn:
                    da.To = scaleAnimTo;
                    this.scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, da, HandoffBehavior.SnapshotAndReplace);
                    this.scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, da, HandoffBehavior.SnapshotAndReplace);
                    break;

                case AnimationType.ZoomOut:
                    da.To = scaleAnimFrom;
                    this.scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, da, HandoffBehavior.SnapshotAndReplace);
                    this.scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, da, HandoffBehavior.SnapshotAndReplace);
                    break;

                case AnimationType.PanLeft:
                    da.To = -1 * transAnimTo;
                    this.translateTransform.BeginAnimation(TranslateTransform.XProperty, da, HandoffBehavior.SnapshotAndReplace);
                    break;

                case AnimationType.PanRight:
                    da.To = transAnimFrom;
                    this.translateTransform.BeginAnimation(TranslateTransform.XProperty, da, HandoffBehavior.SnapshotAndReplace);
                    break;
            }
        }
    }
}
