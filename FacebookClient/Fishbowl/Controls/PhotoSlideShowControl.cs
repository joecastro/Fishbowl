﻿//-----------------------------------------------------------------------
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
    using System.Reflection;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Threading;
    using ClientManager;
    using TransitionEffects;
    using Contigo;

    /// <summary>
    /// Control used to display a slideshow of photo's with transitions.
    /// </summary>
    [TemplatePart(Name = "PART_PhotoHost", Type = typeof(Decorator))]
    public class PhotoSlideShowControl : Control
    {
        public event Action Stopped;

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
            _photos = null;
            currentChild.FacebookPhoto = null;
            oldChild.FacebookPhoto = null;
            transitionTimer.Stop();
            SetValue(PausedPropertyKey, false);

            var photoCollection = e.NewValue as FacebookPhotoCollection;
            if (photoCollection == null || photoCollection.Count == 0)
            {
                return;
            }

            var photos = new FacebookPhoto[photoCollection.Count];
            photoCollection.CopyTo(photos, 0);

            _photos = photos;

            CoerceValue(CurrentIndexProperty);

            currentChild.FacebookPhoto = _CurrentPhoto;
            oldChild.FacebookPhoto = _NextPhoto;

            if (photoHost != null)
            {
                StartTimer();
            }
        }

        public static readonly DependencyProperty CurrentIndexProperty = DependencyProperty.Register(
            "CurrentIndex",
            typeof(int),
            typeof(PhotoSlideShowControl),
            new PropertyMetadata(
                0,
                (d, e) => ((PhotoSlideShowControl)d)._OnCurrentIndexChanged(e),
                (d, value) => ((PhotoSlideShowControl)d)._CoerceCurrentIndexValue(value)));

        /// <summary>
        /// Gets or sets the CurrentIndex property.  This dependency property 
        /// indicates ....
        /// </summary>
        public int CurrentIndex
        {
            get { return (int)GetValue(CurrentIndexProperty); }
            set { SetValue(CurrentIndexProperty, value); }
        }

        private void _OnCurrentIndexChanged(DependencyPropertyChangedEventArgs e)
        {
            if (photoHost == null)
            {
                currentChild.FacebookPhoto = _CurrentPhoto;
                oldChild.FacebookPhoto = _NextPhoto;
            }
        }

        private object _CoerceCurrentIndexValue(object value)
        {
            if (_photos == null)
            {
                return 0;
            }

            int i = (int)value;

            i = Math.Min(i, _photos.Length - 1);
            i = Math.Max(0, i);

            return i;
        }


        private FacebookPhoto _CurrentPhoto
        {
            get
            {
                if (_photos == null)
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
                if (_photos == null)
                {
                    return null;
                }
                return _photos[(CurrentIndex + 1) % _photos.Length];
            }
        }

        private static readonly DependencyPropertyKey PausedPropertyKey = DependencyProperty.RegisterReadOnly(
                        "Paused",
                        typeof(bool),
                        typeof(PhotoSlideShowControl),
                        new FrameworkPropertyMetadata(false));

        /// <summary>
        /// DependencyProperty for <see cref="Paused"/> property.
        /// </summary>
        public static readonly DependencyProperty PausedProperty = PausedPropertyKey.DependencyProperty;

        private FacebookPhoto[] _photos;

        /// <summary>
        /// Control hosting the current slide show image.
        /// </summary>
        private SimplePhotoViewerControl currentChild;

        /// <summary>
        /// Control that temporarily hosts the old slide show image upon transition to the next image.
        /// </summary>
        private SimplePhotoViewerControl oldChild;

        /// <summary>
        /// Decorator that hosts photo controls.
        /// </summary>
        private Decorator photoHost;

        /// <summary>
        /// Timer to control interval between transitions.
        /// </summary>
        private DispatcherTimer transitionTimer;

        /// <summary>
        /// Timer to hide the mouse pointer and the slide show controls (play, pause, stop, ...).
        /// </summary>
        private DispatcherTimer mousePointerTimer;

        /// <summary>
        /// PRNG used to select the next transition to be applied.
        /// </summary>
        private static Random rand = new Random();

        /// <summary>
        /// The list of all kinds of transition effects that are supported
        /// </summary>
        private static Type[] transitionEffectTypes;

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
            Assembly assembly = Assembly.GetAssembly(typeof(TransitionEffect));
            transitionEffectTypes = assembly.GetTypes();
        }

        /// <summary>
        /// Initializes a new instance of the PhotoSlideShowControl class.
        /// </summary>
        public PhotoSlideShowControl()
        {
            this.currentChild = new SimplePhotoViewerControl();
            this.oldChild = new SimplePhotoViewerControl();

            this.transitionTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(totalAnimationPeriod), DispatcherPriority.Input, this.OnTransitionTimerTick, Dispatcher);
            this.transitionTimer.Stop();

            this.mousePointerTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(totalAnimationPeriod), DispatcherPriority.Input, this.OnMousePointerTimerTick, Dispatcher);
            this.mousePointerTimer.Stop();

            this.currentAnimationType = AnimationType.None;

            transformGroup = new TransformGroup();
            this.translateTransform = new TranslateTransform(transAnimFrom, transAnimFrom);
            this.scaleTransform = new ScaleTransform(scaleAnimFrom, scaleAnimFrom);
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
        }

        #region Public Properties

        /// <summary>
        /// Gets a value indicating whether slide show is in paused mode or not.
        /// </summary>
        public bool Paused
        {
            get { return (bool)GetValue(PausedProperty); }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// OnApplyTemplate override
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            this.menuBorder = this.Template.FindName("PART_MenuBorder", this) as Border;
            this.photoHost = this.Template.FindName("PART_PhotoHost", this) as Decorator;

            if (this.photoHost != null)
            {
                this.photoHost.Child = this.currentChild;
                this.photoHost.RenderTransform = this.transformGroup;
                this.photoHost.RenderTransformOrigin = new Point(0.5, 0.5);

                if (_photos != null)
                {
                    this.StartTimer();
                }
            }
        }

        #endregion

        #region Private Methods

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
                this.mousePointerTimer.Stop();
                this.mousePointerTimer.Start();
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
                this.mousePointerTimer.Stop();
                this.mousePointerTimer.Start();
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
                    slideShow.MoveNext();
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

                    var handler = slideShow.Stopped;
                    if (handler != null)
                    {
                        handler();
                    }
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.Focus();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            this.currentChild.Effect = null;
            this.transitionTimer.Stop();
            this.mousePointerTimer.Stop();
            this.SetValue(PausedPropertyKey, false);
        }

        /// <summary>
        /// Swaps control displaying current photo with the control for the next photo, enabling transition.
        /// </summary>
        private void SwapChildren()
        {
            SimplePhotoViewerControl temp = this.currentChild;
            this.currentChild = this.oldChild;
            this.oldChild = temp;
            this.currentChild.Width = double.NaN;
            this.currentChild.Height = double.NaN;
            if (this.photoHost != null)
            {
                this.photoHost.Child = this.currentChild;
            }

            this.oldChild.Effect = null;
        }

        /// <summary>
        /// Starts timer and resets Paused property
        /// </summary>
        private void StartTimer()
        {
            this.transitionTimer.Start();
            this.mousePointerTimer.Start();
            this.ResumeSlideShowAnimation();
            this.SetValue(PausedPropertyKey, false);
        }

        /// <summary>
        /// Stops timer and sets Paused property
        /// </summary>
        private void StopTimer()
        {
            this.transitionTimer.Stop();
            this.mousePointerTimer.Stop();
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

            VisualBrush vb = new VisualBrush(this.oldChild);
            vb.Viewbox = new Rect(0, 0, this.oldChild.ActualWidth, this.oldChild.ActualHeight);
            vb.ViewboxUnits = BrushMappingMode.Absolute;
            this.oldChild.Width = this.oldChild.ActualWidth;
            this.oldChild.Height = this.oldChild.ActualHeight;
            this.oldChild.Measure(new Size(this.oldChild.ActualWidth, this.oldChild.ActualHeight));
            this.oldChild.Arrange(new Rect(0, 0, this.oldChild.ActualWidth, this.oldChild.ActualHeight));

            TransitionEffect transitionEffect = GetRandomTransitionEffect();
            transitionEffect.OldImage = vb;
            this.currentChild.Effect = transitionEffect;

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
                    idx = rand.Next(transitionEffectTypes.Length);
                } while (transitionEffectTypes[idx].IsAbstract == true);

                transitionEffect = Activator.CreateInstance(transitionEffectTypes[idx]) as TransitionEffect;
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
        private void MoveNext()
        {
            if (!this.Paused)
            {
                this.StopTimer();
            }

            if (_photos != null)
            {
                CurrentIndex = (CurrentIndex + 1) % _photos.Length;
            }

            this.ChangePhoto(false);
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

            if (_photos != null)
            {
                CurrentIndex = (CurrentIndex + _photos.Length - 1) % _photos.Length;
            }

            this.ChangePhoto(false);
        }

        /// <summary>
        /// Stops slide show and navigates to the currently displayed photo.
        /// </summary>
        private void NavigateToPhoto()
        {
            this.transitionTimer.Stop();
            this.SetValue(PausedPropertyKey, false);
            if (_photos != null)
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
            if (_photos != null)
            {
                CurrentIndex = (CurrentIndex + 1) % _photos.Length;
            }

            // If resuming from a paused state, then reset the time interval to its maximum
            if (this.transitionTimer.Interval.Milliseconds != totalAnimationPeriod)
            {
                this.transitionTimer.Interval = TimeSpan.FromMilliseconds(totalAnimationPeriod);
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
            this.mousePointerTimer.Stop();

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
            if (_photos != null && !this.oldChild.ImageDownloadInProgress)
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
                    this.currentChild.FacebookPhoto = _CurrentPhoto;
                    this.oldChild.FacebookPhoto = _NextPhoto;
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
            this.currentChild.Effect = null;
            if (_photos != null)
            {
                this.oldChild.FacebookPhoto = _NextPhoto;
            }
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

            this.scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            this.scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            this.translateTransform.BeginAnimation(TranslateTransform.XProperty, null);

            this.scaleTransform.ScaleX = scaleValue;
            this.scaleTransform.ScaleY = scaleValue;
            this.translateTransform.X = transValue;

            this.transitionTimer.Interval = TimeSpan.FromMilliseconds(totalAnimationPeriod - elapsedAnimationPeriod);
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
            da.Duration = this.transitionTimer.Interval;
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

        #endregion
    }
}
