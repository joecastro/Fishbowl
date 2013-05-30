#define USE_STANDARD_DRAGDROP

namespace FacebookClient
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Windows;
    using System.Windows.Automation.Peers;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Navigation;
    using System.Windows.Shell;
    using ClientManager;
    using ClientManager.Controls;
    using ClientManager.View;
    using Contigo;
    using Standard;
    using Microsoft.Windows.Shell;

    /// <summary>
    /// The ScePhoto view mode; regular or full-screen with options.
    /// </summary>
    public enum ViewingMode
    {
        /// <summary>
        /// Full-screen viewing with the navigation UI.
        /// </summary>
        FullScreenNavigationUI,

        /// <summary>
        /// Normal viewing with the navigation UI.
        /// </summary>
        NormalScreenNavigationUI
    }

    public partial class MainWindow
    {
        const double SmallModeWidth = 780;

        public static readonly DependencyProperty GoldBarBackgroundBrushProperty = DependencyProperty.Register(
            "GoldBarBackgroundBrush",
            typeof(Brush), 
            typeof(MainWindow),
            new FrameworkPropertyMetadata(Brushes.Gold));

        public Brush GoldBarBackgroundBrush
        {
            get { return (Brush)GetValue(GoldBarBackgroundBrushProperty); }
            set { SetValue(GoldBarBackgroundBrushProperty, value); }
        }

        public static readonly DependencyProperty GoldBarBorderBrushProperty = DependencyProperty.Register(
            "GoldBarBorderBrush",
            typeof(Brush), 
            typeof(MainWindow),
            new FrameworkPropertyMetadata(Brushes.Goldenrod));

        public Brush GoldBarBorderBrush
        {
            get { return (Brush)GetValue(GoldBarBorderBrushProperty); }
            set { SetValue(GoldBarBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty FullScreenModeProperty = DependencyProperty.Register(
            "FullScreenMode",
            typeof(bool),
            typeof(MainWindow),
            new UIPropertyMetadata(false));
        
        public bool FullScreenMode
        {
            get { return (bool)GetValue(FullScreenModeProperty); }
            protected set { SetValue(FullScreenModeProperty, value); }
        }

        public static readonly DependencyProperty IsOnlineProperty = DependencyProperty.Register(
            "IsOnline",
            typeof(bool),
            typeof(MainWindow),
            new PropertyMetadata(false));

        public bool IsOnline
        {
            get { return (bool)GetValue(IsOnlineProperty); }
            private set { SetValue(IsOnlineProperty, value); }
        }

        public static readonly DependencyProperty IsInSmallModeProperty = DependencyProperty.Register("IsInSmallMode", typeof(bool), typeof(MainWindow));

        public bool IsInSmallMode
        {
            get { return (bool)GetValue(IsInSmallModeProperty); }
            set { SetValue(IsInSmallModeProperty, value); }
        }

        /// <summary>
        /// Viewing mode for the next view.
        /// </summary>
        private ViewingMode _viewingMode = ViewingMode.NormalScreenNavigationUI;

        internal MainWindowCommands ApplicationCommands { get; private set; }

        private readonly RoutedCommand _SwitchFullScreenModeCommand;

        private WindowStyle _windowStyle = WindowStyle.None;
        private WindowState _windowState = WindowState.Normal;
        private ResizeMode _resizeMode = ResizeMode.NoResize;

        private readonly PhotoUploadWizard _photoUploadWizard;
        private readonly PhotoUploadInformationPage _photoUploadWizardInfoPage;
        private readonly EmbeddedBrowserControl _embeddedBrowserControl;

        public MainWindow()
        {
            ServiceProvider.Initialize(FacebookClientApplication.FacebookApiId, FacebookClientApplication.FacebookApiKey, FacebookClientApplication.BingApiKey, Environment.GetCommandLineArgs(), Dispatcher);
            ServiceProvider.GoneOnline += (sender, e) =>
            {
                IsOnline = true;
                ServiceProvider.ViewManager.Friends.CollectionChanged += _OnFriendsHasCount;
            };
            
            ServiceProvider.ViewManager.PropertyChanged += _OnViewManagerPropertyChanged;
            ServiceProvider.ViewManager.ExternalNavigationRequested += _OnExternalNavigationRequested;

            Loaded += (sender, e) =>
            {
                ServiceProvider.ViewManager.NavigationCommands.NavigateLoginCommand.Execute(null);

                if (FacebookClientApplication.Current2.IsFirstRun)
                {
                    FacebookClientApplication.Current2.IsFirstRun = false;
                }
            };

            SourceInitialized += (sender, e) =>
            {
                // Defer starting the update timer until the Window is up, but not until the application is online.  
                // We want to make sure that the user is able to update the app if there's a fix available for an issue that
                // was preventing them from connecting to the service.
                DeploymentManager.ApplicationUpdated += _OnApplicationUpdated;
                DeploymentManager.ApplicationUpdateFailed += _OnApplicationUpdateFailed;
                DeploymentManager.StartMonitor();
            };

            // When the window loses focus take it as an opportunity to trim our workingset.
            Deactivated += (sender, e) => FacebookClientApplication.PerformAggressiveCleanup();

            ApplicationCommands = new MainWindowCommands(ServiceProvider.ViewManager);

            _SwitchFullScreenModeCommand = new RoutedCommand("SwitchFullScreenMode", typeof(MainWindow));

            InitializeComponent();

            _photoUploadWizard = new PhotoUploadWizard();
            _photoUploadWizardInfoPage = new PhotoUploadInformationPage(_photoUploadWizard);
            _embeddedBrowserControl = new EmbeddedBrowserControl();

            Rect settingsBounds = Properties.Settings.Default.MainWindowBounds;
            if (!settingsBounds.IsEmpty)
            {
                if (settingsBounds.Left == 0 && settingsBounds.Top == 0)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                else
                {
                    this.Left = settingsBounds.Left;
                    this.Top = settingsBounds.Top;
                }
                this.Width = settingsBounds.Width;
                this.Height = settingsBounds.Height;
            }
            
            CommandBindings.Add(new CommandBinding(System.Windows.Input.NavigationCommands.BrowseBack, (sender, e) => _SafeBrowseBack(), (sender, e) => e.CanExecute = CanGoBack));
            CommandBindings.Add(new CommandBinding(System.Windows.Input.NavigationCommands.Refresh, (sender, e) => ServiceProvider.ViewManager.ActionCommands.StartSyncCommand.Execute(null)));
            CommandBindings.Add(new CommandBinding(_SwitchFullScreenModeCommand, OnSwitchFullScreenCommand));

            RoutedCommand backNavigationKeyOverrideCommand = new RoutedCommand();
            CommandBindings.Add(
                new CommandBinding(
                    backNavigationKeyOverrideCommand,
                    (sender, e) => ((MainWindow)sender)._SafeBrowseBack()));

            InputBindings.Add(new InputBinding(backNavigationKeyOverrideCommand, new KeyGesture(Key.Back)));
            
            this.PreviewStylusSystemGesture += new StylusSystemGestureEventHandler(OnPreviewStylusSystemGesture);
            this.PreviewStylusMove += new StylusEventHandler(OnPreviewStylusMove);

            this.SizeChanged += _OnSizeChanged;
        }

        private void _OnFriendsHasCount(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (ServiceProvider.ViewManager.Friends.Count > 0)
            {
                ServiceProvider.ViewManager.Friends.CollectionChanged -= _OnFriendsHasCount;
                SplashScreenOverlay.GenerateCustomSplashScreen(ServiceProvider.ViewManager.Friends);
            }
        }

        private void _OnNotificationNavigationRequested(object sender, RequestNavigateEventArgs e)
        {
            // If there's no handler then send it as a navigation command.
            Hyperlink hyperlink = sender as Hyperlink;
            ServiceProvider.ViewManager.NavigationCommands.NavigateToContentCommand.Execute(hyperlink.NavigateUri);
            e.Handled = true;
        }

        private void _OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            IsInSmallMode = e.NewSize.Width < SmallModeWidth;

            if (ServiceProvider.ViewManager.Dialog != null)
            {
                _UpdateClipMargin();
            }
        }

        private void _OnMessageNavigationRequested(object sender, RequestNavigateEventArgs e)
        {
            // If there's no handler then send it as a navigation command.
            Hyperlink hyperlink = sender as Hyperlink;
            ServiceProvider.ViewManager.NavigationCommands.NavigateToContentCommand.Execute(hyperlink.NavigateUri);
            e.Handled = true;
        }

        private void _OnExternalNavigationRequested(object sender, RequestNavigateEventArgs e)
        {
            if (FacebookClientApplication.Current2.OpenWebContentInExternalBrowser)
            {
                if (e.Uri != null)
                {
                    Process.Start(new ProcessStartInfo(e.Uri.OriginalString));
                }
            }
            else
            {
                _embeddedBrowserControl.Source = e.Uri;
            }
        }

        private void _OnViewManagerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "CurrentRootNavigator":
                    _OnRootNavigatorChanged();
                    break;
                case "Dialog":
                    _OnDialogChanged();
                    break;
            }
        }

        private void _OnDialogChanged()
        {
            _UpdateClipMargin();
        }

        private void _UpdateClipMargin()
        {
            //TaskbarItemInfo tbii = TaskbarItemInfo.GetTaskbarItemInfo(this);
            //if (ServiceProvider.ViewManager.Dialog == null)
            //{
            //    Thickness margin = new Thickness(0, Header.ActualHeight, 0, Footer.ActualHeight);
            //    tbii.ThumbnailClipMargin = margin;
            //}
            //else
            //{
            //    tbii.ThumbnailClipMargin = default(Thickness);
            //}
        }

        private Thickness _SubtractSizesToGetThickness(Size outerSize, Size innerSize)
        {
            double width = outerSize.Width - innerSize.Width;
            double height = outerSize.Height - innerSize.Height;

            double x = width / 2;
            double y = height / 2;

            return new Thickness(x, y, x, y);
        }

        private void _OnRootNavigatorChanged()
        {
            Navigator rootNavigator = ServiceProvider.ViewManager.CurrentRootNavigator;
            this.Header.OnRootNavigatorChanged(rootNavigator);

            this.ToggleOpacityOfAnimatedSwooshes(rootNavigator);
        }

        private void ToggleOpacityOfAnimatedSwooshes(Navigator navigator)
        {
            if (navigator.Content.GetType() == typeof(LoadingPage))
            {
                var timeline = new DoubleAnimation(1, new Duration(TimeSpan.FromMilliseconds(2000)));
                this.animatedSwooshes.BeginAnimation(FrameworkElement.OpacityProperty, timeline);
            }
            else
            {
                this.animatedSwooshes.Pause();

                if (this.animatedSwooshes.Opacity == 1)
                {
                    var timeline = new DoubleAnimation(.7, new Duration(TimeSpan.FromMilliseconds(500)));
                    this.animatedSwooshes.BeginAnimation(FrameworkElement.OpacityProperty, timeline);
                }
            }
        }

        private void _OnApplicationUpdated(object sender, EventArgs e)
        {
            var restartLink = new Hyperlink(new Run("Click here"));
            restartLink.Click += (sender2, e2) =>
            {
                FacebookClientApplication.ClearUserState();
                ApplicationCommands.InitiateRestartCommand.Execute(this);
            };

            _ShowGoldBar(true, new Inline[]
            { 
                new Run("Fishbowl has been updated. "),
                restartLink,
                new Run(" to restart.")
            });
        }

        private void _OnApplicationUpdateFailed(object sender, ApplicationUpdateFailedEventArgs e)
        {
            Assert.IsNotNull(e);
            
            var websiteLink = new Hyperlink(new Run("website"));
            websiteLink.Click += (sender2, e2) => Process.Start(new ProcessStartInfo(FacebookClientApplication.Current2.SupportWebsite.OriginalString));

            if (e.WasUpdateDetected)
            {
                _ShowGoldBar(false, new Inline[]
                {
                    new Run("An update to Fishbowl was unable to install successfully.  To complete the update please visit the "),
                    websiteLink,
                    new Run(" and run setup manually."),
                });
            }
            else
            {
                _ShowGoldBar(false, new Inline[]
                {
                    new Run("Fishbowl was unable to check for updates.  To check on whether there's a new version please visit the "),
                    websiteLink,
                    new Run("."),
                });
            }
        }

        protected override void OnClosed(EventArgs args)
        {
            if (this.WindowState == WindowState.Normal)
            {
                Properties.Settings.Default.MainWindowBounds = new Rect(Left, Top, Width, Height);
            }
            else
            {
                Properties.Settings.Default.MainWindowBounds = RestoreBounds;
            }

            Action<string> deleteCallback = null;
            if (FacebookClientApplication.Current2.DeleteCacheOnShutdown)
            {
                deleteCallback = dir =>
                {
                    // I'd rather use SHFileOperation to get the dialog, but I haven't been able to get it to work.
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch
                    { }
                };
            }

            ServiceProvider.Shutdown(deleteCallback);

            base.OnClosed(args);
        }

        public void ShowUploadWizard(string albumName)
        {
            _photoUploadWizardInfoPage.SetDefaultAlbum(albumName);
            ServiceProvider.ViewManager.ShowDialog(_photoUploadWizardInfoPage);
        }

        /// <summary>
        /// Switches the viewing mode between full screen and windowed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Arguments describing the routed event.</param>
        private void OnSwitchFullScreenCommand(object sender, ExecutedRoutedEventArgs e)
        {
            UpdateViewingModeFullScreen();
            ContentPane.Focus();
        }

        private void OnPreviewStylusSystemGesture(object sender, StylusSystemGestureEventArgs e)
        {
            if (!e.Handled)
                ApplicationInputHandler.OnPreviewStylusSystemGesture(e);
        }

        private void OnPreviewStylusMove(object sender, StylusEventArgs e)
        {
            if (!e.Handled)
                ApplicationInputHandler.OnPreviewStylusMove(e);

        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            // Custom logic for MainPage's MouseWheel should be put before calling global handler

            // Next, call global handler
            if (!e.Handled)
            {
                ApplicationInputHandler.OnMouseWheel(e);
            }

            // Finally, call base if not handled
            if (!e.Handled)
            {
                base.OnMouseWheel(e);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Custom key handling for main page
            if (!e.Handled)
            {
                if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
                {
                    switch (e.Key)
                    {
                        case Key.F9:
                            OnF9KeyPress(e);
                            break;
                        case Key.F11:
                            OnF11KeyPress(e);
                            break;
                        case Key.Escape:
                            OnEscapeKeyPress(e);
                            break;
                        default:
                            break;
                    }
                }
                else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                {
                    switch (e.Key)
                    {
                        case Key.T:
                            ApplicationCommands.SwitchThemeCommand.Execute(null);
                            break;
                    }
                }
            }

            // Next, call application-wide input handler.
            if (!e.Handled)
            {
                ApplicationInputHandler.OnKeyDown(e);
            }

            // Finally, call base if not handled.
            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        #region Private Methods

        /// <summary>
        /// Turn off full screen
        /// </summary>
        private void RestoreViewingMode()
        {
            _SwitchFullScreenMode(false);
        }

        /// <summary>
        /// Get the next viewing mode for the given value.
        /// </summary>
        /// <param name="viewingMode">Current viewing mode.</param>
        /// <returns>Next viewing mode.</returns>
        private static ViewingMode _GetNextViewingMode(ViewingMode viewingMode)
        {
            switch (viewingMode)
            {
                case ViewingMode.FullScreenNavigationUI: return ViewingMode.NormalScreenNavigationUI;
                case ViewingMode.NormalScreenNavigationUI: return ViewingMode.FullScreenNavigationUI;
                default:
                    Assert.Fail();
                    return ViewingMode.FullScreenNavigationUI;
            }
        }

        /// <summary>
        /// Switches full screen mode on or off.
        /// </summary>
        /// <param name="fullScreen">If true, full screen mode is on, otherwise, it's turned off.</param>
        private void _SwitchFullScreenMode(bool fullScreen)
        {
            // If viewing mode is already in a full screen state, not changes are necessary
            if (fullScreen && !FullScreenMode)
            {
                // Hide the window while we make these changes.
                this.Hide();

                _resizeMode = ResizeMode;
                ResizeMode = ResizeMode.NoResize;

                _windowStyle = WindowStyle;
                WindowStyle = WindowStyle.None;

                // Be consistent with IE, don't set Topmost.
                // It also has weird interactions with WebOC popup windows.

                _windowState = WindowState;

                // If the window was already maximized, restore it before making this switch
                // or else the window will slightly extend the workarea of the monitor.
                if (_windowStyle != WindowStyle.None && WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Normal;
                }

                WindowState = WindowState.Maximized;

                FullScreenMode = true;

                this.Show();
            }
            else if (!fullScreen && FullScreenMode)
            {
                Assert.AreEqual(WindowState, WindowState.Maximized);

                WindowStyle = _windowStyle;
                ResizeMode = _resizeMode;

                if (_windowState == WindowState.Normal)
                {
                    WindowState = WindowState.Normal;
                }

                FullScreenMode = false;
            }

            this.Focus();
        }

        /// <summary>
        /// On escape, go back to normal screen and navigation ui.
        /// </summary>
        /// <param name="e">Event args describing the event.</param>
        private void OnEscapeKeyPress(KeyEventArgs e)
        {
            // Restore viewing mode
            RestoreViewingMode();
            ContentPane.Focus();
            e.Handled = true;
        }

        /// <summary>
        /// Cycle viewing mode on F9 key press.
        /// </summary>
        /// <param name="e">EventArgs describing the event.</param>
        private void OnF9KeyPress(KeyEventArgs e)
        {
            CycleViewingMode();
            e.Handled = true;
        }

        /// <summary>
        /// On F11 key press, switch full screen mode.
        /// </summary>
        /// <param name="e">EventArgs describing the event.</param>
        private void OnF11KeyPress(KeyEventArgs e)
        {
            UpdateViewingModeFullScreen();
            e.Handled = true;
        }

        /// <summary>
        /// Switches viewing mode based on full screen setting, and toggles the visibilty of navigation UI.
        /// </summary>
        private void UpdateViewingModeFullScreen()
        {
            switch (_viewingMode)
            {
                case ViewingMode.FullScreenNavigationUI:
                    _SwitchFullScreenMode(false);
                    _viewingMode = ViewingMode.NormalScreenNavigationUI;
                    break;
                case ViewingMode.NormalScreenNavigationUI:
                    _SwitchFullScreenMode(true);
                    _viewingMode = ViewingMode.FullScreenNavigationUI;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Cycles the viewing mode to the next value.
        /// </summary>
        private void CycleViewingMode()
        {
            ViewingMode nextViewingMode = _GetNextViewingMode(_viewingMode);
            switch (nextViewingMode)
            {
                case ViewingMode.FullScreenNavigationUI:
                    _SwitchFullScreenMode(true);
                    break;
                case ViewingMode.NormalScreenNavigationUI:
                    _SwitchFullScreenMode(false);
                    break;
                default:
                    break;
            }

            _viewingMode = nextViewingMode;
        }

        private void _SafeBrowseBack()
        {
            if (CanGoBack)
            {
                GoBack();
            }
        }

        #endregion

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);

            e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) && !(ServiceProvider.ViewManager.Dialog is EmbeddedBrowserControl))
                ? DragDropEffects.Copy
                : DragDropEffects.None;

            e.Handled = true;

#if !USE_STANDARD_DRAGDROP
            DropTargetHelper.DragEnter(this, e.Data, e.GetPosition(this), e.Effects);
#endif
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            base.OnDragLeave(e);
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && !(ServiceProvider.ViewManager.Dialog is EmbeddedBrowserControl) 
                ? DragDropEffects.Copy 
                : DragDropEffects.None;

            e.Handled = true;

#if !USE_STANDARD_DRAGDROP
            DropTargetHelper.DragLeave(e.Data);
#endif
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);

            e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) && !(ServiceProvider.ViewManager.Dialog is EmbeddedBrowserControl))
                ? DragDropEffects.Copy
                : DragDropEffects.None;

            e.Handled = true;

#if !USE_STANDARD_DRAGDROP
            DropTargetHelper.DragOver(e.GetPosition(this), e.Effects);
#endif
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);

            if (!DragContainer.IsInDrag && !(ServiceProvider.ViewManager.Dialog is EmbeddedBrowserControl))
            {
                string[] fileNames = e.Data.GetData(DataFormats.FileDrop) as string[];
                DoDrop(fileNames);
            }

#if !USE_STANDARD_DRAGDROP
            DropTargetHelper.Drop(e.Data, e.GetPosition(this), DragDropEffects.Copy);
#endif
        }

        public void DoDrop(string[] fileNames)
        {
            List<string> imageFiles = PhotoUploadWizard.FindImageFiles(fileNames);
            if (imageFiles.Count != 0)
            {
                if (ServiceProvider.ViewManager.EndDialog(ServiceProvider.ViewManager.Dialog))
                {
                    // This is a bit hacky.  Don't clear the selected album on drop
                    // if a dialog (e.g. an existing upload wizard) is up.
                    // This will leave it if the embedded browser or settings dialog is up,
                    // but whatever.
                    _photoUploadWizard.SetDefaultAlbum(null);
                }
                _photoUploadWizard.Show(imageFiles);
            }
        }

        public void SignOut()
        {
            FacebookClientApplication.ClearUserState();
            ApplicationCommands.InitiateRestartCommand.Execute(this);
        }

        private void _CloseGoldBar(object sender, RoutedEventArgs e)
        {
            GoldBarBorder.Visibility = Visibility.Collapsed;
        }

        private void _ShowGoldBar(bool informational, Inline[] text)
        {
            if (informational)
            {
                GoldBarBackgroundBrush = Brushes.LightBlue;
                GoldBarBorderBrush = (Brush)Application.Current.Resources["FacebookBlueBrush"];
            }
            else
            {
                GoldBarBackgroundBrush = Brushes.RosyBrown;
                GoldBarBorderBrush = Brushes.Maroon;
            }

            GoldBarTextBlock.Inlines.Clear();
            GoldBarTextBlock.Inlines.AddRange(text);

            GoldBarBorder.Visibility = Visibility.Visible;
        }

        internal bool ProcessCommandLineArgs(IList<string> commandLineArgs)
        {
            // This only gets signaled to process a second instance sending commands to this window.
            Assert.IsNotDefault(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            Assert.IsNotNull(ApplicationCommands);

            bool ret = false;

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            if (commandLineArgs != null && commandLineArgs.Count > 0)
            {
                int argIndex = 0;
                while (argIndex < commandLineArgs.Count)
                {
                    string commandSwitch = commandLineArgs[argIndex].ToLowerInvariant();
                    switch (commandSwitch)
                    {
                        case "-minimode":
                        case "/minimode":
                            ApplicationCommands.SwitchToMiniModeCommand.Execute(this);
                            ret = true;
                            break;
                        case "-chat":
                        case "/chat":
                            ApplicationCommands.ShowChatWindowCommand.Execute(this);
                            ret = true;
                            break;
                    }
                    ++argIndex;
                }
            }

            if (!ret)
            {
                this.Activate();
            }

            return ret;
        }

        internal void ShowInbox()
        {
            _OnExternalNavigationRequested(this, new RequestNavigateEventArgs(new Uri("http://www.facebook.com/inbox"), null));
        }

        internal void SetTaskbarProgress(float percent)
        {
            TaskbarItemInfo tbi = this.TaskbarItemInfo;
            tbi.ProgressState = TaskbarItemProgressState.Normal;
            tbi.ProgressValue = percent;
        }

        internal void ClearTaskbarProgress()
        {
            TaskbarItemInfo tbi = this.TaskbarItemInfo;
            tbi.ProgressState = TaskbarItemProgressState.None;
        }
    }
}
