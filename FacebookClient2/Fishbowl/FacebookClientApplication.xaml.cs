namespace FacebookClient
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Shell;
    using Contigo;
    using FacebookClient.Properties;
    using Standard;

    /// <summary>
    /// Code behind file for the FacebookClient Application XAML.
    /// </summary>
    public partial class FacebookClientApplication : Application, INotifyPropertyChanged
    {
        private enum _WindowMode
        {
            Normal,
            SlideShow,
            MiniMode,
        }

        public static FacebookClientApplication Current2
        {
            get { return (FacebookClientApplication)Application.Current; }
        }

        internal const string FacebookApiKey = "f6310ebf42d462b20050f62bea75d7d2";
        internal const string BingApiKey = "63F02036684DE7BEA0FDE713C0D1653056727276";

        private static readonly Uri _SupportUri = new Uri("http://www.fishbowlclient.com");
        private static readonly Uri _PrivacyUri = new Uri("http://go.microsoft.com/fwlink/?LinkId=167928");

        private static readonly Dictionary<string, Uri> _ThemeLookup = new Dictionary<string,Uri>
        {
            { "Blue",     new Uri(@"Resources\Themes\Blue\Blue.xaml", UriKind.Relative) },
            { "Dark",     new Uri(@"Resources\Themes\Dark\Dark.xaml", UriKind.Relative) },
            { "Facebook", new Uri(@"Resources\Themes\FBBlue\FBBlue.xaml", UriKind.Relative) },
            { "Blend",    new Uri(@"Resources\Themes\Blend\Expression.xaml", UriKind.Relative) },
#if DEBUG
            // Not a production quality theme.
            // This can be used to find resources that aren't properly styled.
            { "Red",      new Uri(@"Resources\Themes\Red\Red.xaml", UriKind.Relative) },
#endif
        };
        private static readonly List<string> _ThemeNames = new List<string>(_ThemeLookup.Keys);
        private const string _DefaultThemeName = "Dark";

        private MainWindow _mainWindow;
        private MiniModeWindow _minimodeWindow;
        private ChatWindow _chatWindow;
        private SlideShowWindow _slideshowWindow;
        private _WindowMode _viewMode = _WindowMode.Normal;
        private _WindowMode _previousViewMode = _WindowMode.Normal;

        public static IEnumerable<string> AvailableThemes { get { return _ThemeNames.AsReadOnly(); } }

        public Uri SupportWebsite { get { return _SupportUri; } }

        public Uri PrivacyWebsite { get { return _PrivacyUri; } }

        public bool IsFirstRun
        {
            get { return Settings.Default.IsFirstRun; }
            set
            {
                if (Settings.Default.IsFirstRun != value)
                {
                    Settings.Default.IsFirstRun = value;
                    _NotifyPropertyChanged("IsFirstRun");
                }

            }
        }

        public string ThemeName
        {
            get { return Settings.Default.ThemeName; }
            set
            {
                if (value != Settings.Default.ThemeName)
                {
                    Settings.Default.ThemeName = value;
                    ((FacebookClientApplication)Application.Current).SwitchTheme(value);
                    _NotifyPropertyChanged("ThemeName");
                }
            }
        }

        public bool AreUpdatesEnabled
        {
            get { return Settings.Default.AreUpdatesEnabled; }
            set
            {
                if (Settings.Default.AreUpdatesEnabled != value)
                {
                    Settings.Default.AreUpdatesEnabled = value;
                    _NotifyPropertyChanged("AreUpdatesEnabled");
                }
            }
        }

        public bool DeleteCacheOnShutdown { get; set; }

        public bool OpenWebContentInExternalBrowser
        {
            get { return Settings.Default.OpenWebContentExternally; }
            set
            {
                if (Settings.Default.OpenWebContentExternally != value)
                {
                    Settings.Default.OpenWebContentExternally = value;
                    _NotifyPropertyChanged("OpenWebContentInExternalBrowser");
                }
            }
        }

        public bool ShowMoreNewsfeedFilters
        {
            get { return Settings.Default.ShowMoreNewsfeedFilters; }
            set
            {
                if (Settings.Default.ShowMoreNewsfeedFilters != value)
                {
                    Settings.Default.ShowMoreNewsfeedFilters = value;
                    _NotifyPropertyChanged("ShowMoreNewsfeedFilters");
                }
            }
        }

        public bool KeepMeLoggedIn
        {
            get { return Settings.Default.KeepMeLoggedIn; }
            set 
            {
                if (Settings.Default.KeepMeLoggedIn != value)
                {
                    Settings.Default.KeepMeLoggedIn = value;
                    _NotifyPropertyChanged("KeepMeLoggedIn");
                }
            }
        }

        public bool KeepMiniModeWindowOnTop
        {
            get { return Settings.Default.KeepMiniModeWindowOnTop; }
            set
            {
                if (Settings.Default.KeepMiniModeWindowOnTop != value)
                {
                    Settings.Default.KeepMiniModeWindowOnTop = value;
                    Window window = (((FacebookClientApplication)Application.Current)._minimodeWindow);
                    if (null != window)
                    {
                        window.Topmost = value;
                    }
                    _NotifyPropertyChanged("KeepMiniModeWindowOnTop");
                }
            }
        }

        /// <summary>Whether the client is currently Tier 2 capable which is required for hardware-accelerated effects.</summary>
        public static bool IsShaderEffectSupported
        {
            get { return RenderCapability.Tier == 0x00020000 && RenderCapability.IsPixelShaderVersionSupported(2, 0); }
        }

        internal void SwitchTheme(string themeName)
        {
            if (themeName == null)
            {
                int index = _ThemeNames.IndexOf(ThemeName);
                if (index == -1)
                {
                    themeName = _DefaultThemeName;
                }
                else
                {
                    themeName = _ThemeNames[(index + 1) % _ThemeNames.Count];
                }
            }

            Uri resourceUri = null;
            if (!_ThemeLookup.TryGetValue(themeName, out resourceUri))
            {
                themeName = _DefaultThemeName;
                resourceUri = _ThemeLookup[themeName];
            }

            ThemeName = themeName;

            if (_currentThemeDictionary != null)
            {
                this.Resources.MergedDictionaries.Remove(_currentThemeDictionary);
            }
            
            _currentThemeDictionary = LoadComponent(resourceUri) as ResourceDictionary;
            this.Resources.MergedDictionaries.Insert(0, _currentThemeDictionary);
        }

        internal static void PerformAggressiveCleanup()
        {
            GC.Collect(2);
            try
            {
                NativeMethods.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, 40000, 80000);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // This is a way to get the app back to zero.
            //Settings.Default.Reset();

            SwitchTheme(Settings.Default.ThemeName);

            _mainWindow = new MainWindow();
            _minimodeWindow = new MiniModeWindow();

            Point minimodeStartupLocation = FacebookClient.Properties.Settings.Default.MiniModeWindowBounds.TopLeft;
            if (minimodeStartupLocation == default(Point) || !DoubleUtilities.IsFinite(minimodeStartupLocation.X) || !DoubleUtilities.IsFinite(minimodeStartupLocation.Y))
            {
                _minimodeWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            else
            {
                _minimodeWindow.Left = minimodeStartupLocation.X;
                _minimodeWindow.Top = minimodeStartupLocation.Y;
            }

            this.MainWindow = _mainWindow;

            var jumpListResource = (JumpList)Resources["MainModeJumpList"];
            Assert.IsNotNull(jumpListResource);

            var jumpList = new JumpList(jumpListResource.JumpItems, false, false);

            JumpList.SetJumpList(this, jumpList);

            _mainWindow.Show();

            SingleInstance.SingleInstanceActivated += _SignalExternalCommandLineArgs;

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Persist the minimode window's location here, since it never lets itself close on its own.
            FacebookClient.Properties.Settings.Default.MiniModeWindowBounds = new Rect(_minimodeWindow.Left, _minimodeWindow.Top, _minimodeWindow.Width, _minimodeWindow.Height);

            FacebookClient.Properties.Settings.Default.Save();

            if (!KeepMeLoggedIn)
            {
                ClearUserState();
            }
            
            // By explicitly setting this list we'll remove the notifications items.
            var jumpList = (JumpList)Resources["SignedOutJumpList"];
            JumpList.SetJumpList(this, jumpList);

            base.OnExit(e);
        }

        private ResourceDictionary _currentThemeDictionary;

        private void _SignalExternalCommandLineArgs(object sender, SingleInstanceEventArgs e)
        {
            bool handledByWindow = false;
            if (_viewMode == _WindowMode.MiniMode)
            {
                _minimodeWindow.Activate();

                handledByWindow = _minimodeWindow.ProcessCommandLineArgs(e.Args);
            }
            else
            {
                _mainWindow.Activate();
                handledByWindow = _mainWindow.ProcessCommandLineArgs(e.Args);
            }

            if (!handledByWindow)
            {
                ClientManager.ServiceProvider.ViewManager.ProcessCommandLineArgs(e.Args);
            }
        }

        internal void SwitchToMiniMode()
        {
            Dispatcher.VerifyAccess();

            ExitSlideShow();

            if (_viewMode == _WindowMode.MiniMode)
            {
                return;
            }
            _viewMode = _WindowMode.MiniMode;

            _mainWindow.Hide();
            _minimodeWindow.Show();

            if (_minimodeWindow.WindowState == WindowState.Minimized)
            {
                _minimodeWindow.WindowState = WindowState.Normal;
            }
            _minimodeWindow.Activate();

            var miniJumpList = (JumpList)Resources["MiniModeJumpList"];
            JumpList currentJumpList = JumpList.GetJumpList(this);

            // Remove and replace all tasks.
            currentJumpList.JumpItems.RemoveAll(item => item.CustomCategory == null);
            currentJumpList.JumpItems.AddRange(miniJumpList.JumpItems);

            currentJumpList.Apply();
        }

        internal void SwitchToMainMode()
        {
            Dispatcher.VerifyAccess();

            ExitSlideShow();

            if (_viewMode == _WindowMode.Normal)
            {
                return;
            }
            _viewMode = _WindowMode.Normal;

            _minimodeWindow.Hide();
            _mainWindow.Show();

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();

            var mainJumpList = (JumpList)Resources["MainModeJumpList"];
            JumpList currentJumpList = JumpList.GetJumpList(this);

            // Remove and replace all tasks.
            currentJumpList.JumpItems.RemoveAll(item => item.CustomCategory == null);
            currentJumpList.JumpItems.AddRange(mainJumpList.JumpItems);

            currentJumpList.Apply();
        }

        internal void ShowChatWindow()
        {
            if (_chatWindow != null)
            {
                _chatWindow.Activate();
                return;
            }

            _chatWindow = new ChatWindow();
            _chatWindow.Closed += (sender, e) => _chatWindow = null;
            _chatWindow.Show();
        }

        internal void SwitchToSlideShow(FacebookPhotoCollection photos, FacebookPhoto startPhoto)
        {
            if (_viewMode == _WindowMode.SlideShow)
            {
                Assert.IsNotNull(_slideshowWindow);
                _slideshowWindow.Activate();
                return;
            }

            _previousViewMode = _viewMode;
            _viewMode = _WindowMode.SlideShow;

            if (_chatWindow != null)
            {
                _chatWindow.Hide();
            }

            if (_minimodeWindow.IsVisible)
            {
                _minimodeWindow.Hide();
            }

            if (_mainWindow.IsVisible)
            {
                _mainWindow.Hide();
            }

            _slideshowWindow = new SlideShowWindow(photos, startPhoto);
            _slideshowWindow.Show();
            _slideshowWindow.Closing += (sender, e) => ExitSlideShow();
        }

        internal void ExitSlideShow()
        {
            if (_viewMode != _WindowMode.SlideShow)
            {
                return;
            }

            _viewMode = _previousViewMode;
            if (_viewMode == _WindowMode.MiniMode)
            {
                _minimodeWindow.Show();
            }
            else
            {
                _mainWindow.Show();
            }

            if (_chatWindow != null)
            {
                _chatWindow.Show();
            }
        }

        internal static void ClearUserState()
        {
            FacebookLoginService.ClearCachedCredentials(FacebookClientApplication.FacebookApiKey);
            SplashScreenOverlay.DeleteCustomSplashScreen();
        }

        #region INotifyPropertyChanged Members

        private void _NotifyPropertyChanged(string propertyName)
        {
            Assert.IsNeitherNullNorWhitespace(propertyName);

            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
