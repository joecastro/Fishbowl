
namespace FacebookClient
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Documents;
    using System.Windows.Navigation;
    using System.Windows.Threading;
    using ClientManager;
    using ClientManager.View;
    using Contigo;
    using Standard;

    /// <summary>
    /// The Facebook login page as a ContentControl.
    /// </summary>
    public partial class LoginPage
    {
        private class _Navigator : Navigator
        {
            public _Navigator(LoginPage page, Dispatcher dispatcher)
                : base(page, FacebookObjectId.Create("[Login Page]"), null)
            { }

            public override bool IncludeInJournal { get { return false; } }
        }

        private static readonly IList<Permissions> _RequiredPermissions = new List<Permissions>
        {
            //Permissions.CreateEvent,
            //Permissions.Email,
            Permissions.FriendsAboutMe,
            Permissions.FriendsActivities, 
            Permissions.FriendsBirthday,
            Permissions.FriendsEducationHistory,
            Permissions.FriendsEvents,
            Permissions.FriendsGroups,
            Permissions.FriendsHometown,
            Permissions.FriendsInterests,
            Permissions.FriendsLikes,
            Permissions.FriendsLocation,
            Permissions.FriendsNotes,
            Permissions.FriendsOnlinePresence,
            Permissions.FriendsPhotos,
            Permissions.FriendsRelationships,
            Permissions.FriendsReligionPolitics,
            Permissions.FriendsStatus,
            Permissions.FriendsVideos,
            Permissions.FriendsWebsite,
            Permissions.FriendsWorkHistory,
            Permissions.OfflineAccess,
            Permissions.PhotoUpload,
            Permissions.PublishStream,
            Permissions.ReadFriendLists,
            Permissions.ReadInsights,
            Permissions.ReadMailbox,
            Permissions.ReadRequests,
            Permissions.ReadStream,
            //Permissions.RsvpEvent,
            //Permissions.Sms,
            Permissions.UserAboutMe,
            Permissions.UserActivities,
            Permissions.UserBirthday,
            Permissions.UserEducationHistory,
            Permissions.UserEvents,
            Permissions.UserGroups,
            Permissions.UserHometown,
            Permissions.UserInterests,
            Permissions.UserLikes,
            Permissions.UserLocation,
            Permissions.UserNotes,
            Permissions.UserOnlinePresence,
            Permissions.UserPhotos,
            Permissions.UserRelationships,
            Permissions.UserReligionPolitics,
            Permissions.UserStatus,
            Permissions.UserVideos,
            Permissions.UserWebsite,
            Permissions.UserWorkHistory,
        }.AsReadOnly();

        private readonly string _appKey;
        private readonly string _appId;

        private FacebookLoginService _service;
        private Navigator _nextPage;
        private bool _isLoggedIn;

        public Navigator Navigator { get; private set; }

        // Dummy URLs where we'll direct the browser in response to requesting extended permissions.
        // Facebook's APIs require that these be either in the Facebook domain or a subdomain of the connect URL.
        // If trying to use FBConnect, the connect URL must be specified through the application settings
        // or Facebook will redirect the user to an error page.
        private const string _GrantedPermissionUri = "http://www.facebook.com/connect/login_success.html";
        private const string _GrantedPermissionSearchString = "login_success.html";
        private const string _DeniedPermissionUri = "http://www.facebook.com/connect/login_failure.html";
        private const string _DeniedPermissionSearchString = "login_failure.html";

        public LoginPage(string appId, string appKey, Navigator next)
        {
            InitializeComponent();

            Verify.IsNeitherNullNorWhitespace(appKey, "appKey");
            Verify.IsNeitherNullNorWhitespace(appId, "appId");

            Loaded += (sender, e) => _OnLoaded();

            _nextPage = next;
            _appId = appId;
            _appKey = appKey;

            Navigator = new _Navigator(this, this.Dispatcher);
        }

        private void _LoginBrowserNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            _SwitchToErrorPage(e.Exception, true);
        }

        private void _SwitchToInformationPage(string text)
        {
            LoginBorder.Visibility = Visibility.Collapsed;
            ErrorBorder.Visibility = Visibility.Collapsed;
            InformationBorder.Visibility = Visibility.Visible;

            InformationText.Text = text;
        }

        private void _SwitchToErrorPage(Exception e, bool canTryAgain)
        {
            _SwitchToErrorPage(e.Message, true, canTryAgain);
        }

        private void _SwitchToErrorPage(string text, bool fromException, bool canTryAgain)
        {
            LoginBorder.Visibility = Visibility.Collapsed;
            InformationBorder.Visibility = Visibility.Collapsed;

            ErrorBorder.Visibility = Visibility.Visible;

            ErrorText.Inlines.Clear();

            if (fromException)
            {
                ErrorText.Inlines.AddRange(
                    new Inline[] 
                    {
                        new Run { Text = "(" + text + ")" },
                        new LineBreak(),
                        new Run { Text = "Fishbowl was unable to connect to Facebook.  Please check your internet connection and ensure that your firewall is configured to allow internet access to Fishbowl." },
                    });
            }
            else
            {
                ErrorText.Inlines.Add(new Run(text));
            }

            if (canTryAgain)
            {
                TryAgainButton.Visibility = Visibility.Visible;
                TryAgainButton.IsEnabled = true;
            }
            else
            {
                TryAgainButton.Visibility = Visibility.Collapsed;
                TryAgainButton.IsEnabled = false;
            }
        }

        private void _OnLoaded()
        {
            FacebookLoginService service = null;
            try
            {
                service = new FacebookLoginService(_appKey, _appId);
                if (!FacebookClientApplication.Current2.KeepMeLoggedIn)
                {
                    service.ClearCachedCredentials();
                }

                _service = service;
                service = null;

                if (_service.HasCachedSessionInfo)
                {
                    try
                    {
                        _OnUserLoggedIn();
                        return;
                    }
                    catch (Exception)
                    {
                        _service.ClearCachedCredentials();
                    }
                }

                LoginBrowser.Navigate(_service.GetLoginUri(_GrantedPermissionUri, _DeniedPermissionUri, _RequiredPermissions));
            }
            catch (Exception ex)
            {
                _SwitchToErrorPage(ex, _service != null);
            }
        }

        private void _OnUserLoggedIn()
        {
            _SwitchToInformationPage("Verifying permissions from Facebook.");
            _service.GetMissingPermissionsAsync(_RequiredPermissions, _OnGetMissingPermissionsCallback);
        }

        private void _OnGetMissingPermissionsCallback(object sender, AsyncCompletedEventArgs e)
        {
            Action<IList<Permissions>> callback = null;
            IList<Permissions> missingPermissions = null;

            if (e.Error != null)
            {
                callback = (p) => _SwitchToErrorPage(e.Error, true);
            }
            else
            {
                callback = _OnMissingPermissionsVerified;
                missingPermissions = (IList<Permissions>)e.UserState;
            }

            this.Dispatcher.Invoke(DispatcherPriority.Normal, callback, missingPermissions);
        }

        private void _OnMissingPermissionsVerified(IList<Permissions> missingPermissions)
        {
            Assert.IsTrue(Dispatcher.CheckAccess());
            if (missingPermissions.Count != 0)
            {
                _SwitchToErrorPage("Fishbowl requires additional permissions to work properly.", false, true);
            }
            else
            {
                _isLoggedIn = true;
                _GoOnline();
            }
        }

        private void _GoOnline()
        {
            ServiceProvider.GoOnline(_service.SessionKey, _service.SessionSecret, _service.UserId);

            ServiceProvider.ViewManager.NavigateByCommand(_nextPage);

            // After we've navigated away, dispose the fields.
            Utility.SafeDispose(ref LoginBrowser);
            _nextPage = null;
        }

        private void _OnBrowserNavigated(object sender, NavigationEventArgs e)
        {
            // Stop the browser from initiating actions if we displayed the error dialog...
            if (ErrorBorder.Visibility != Visibility.Collapsed)
            {
                return;
            }

            Utility.SuppressJavaScriptErrors(LoginBrowser);
            if (!_isLoggedIn)
            {
                // This will be contained in the page once the user has accepted the app.
                if (e.Uri.ToString().Contains(_GrantedPermissionSearchString))
                {
                    _service.InitiateNewSession(e.Uri);
                    try
                    {
                        _OnUserLoggedIn();
                    }
                    catch (Exception ex)
                    {
                        _SwitchToErrorPage(ex, true);
                    }
                    return;
                }
                else if (e.Uri.ToString().Contains(_DeniedPermissionSearchString))
                {
                    _SwitchToErrorPage("You didn't authorize the application.", true, true);
                    return;
                }
            }

            // Facebook has been changing things recently so this list is no longer reliable.
            // I'm simply going to stop attempting to catch bad navigations.

            /*
            // This list is getting unruly and has potential to need to be added to without actually pushing an app update.
            // Consider making this list augmentable through an external file.
            if (e.Uri.PathAndQuery.Contains(_service.ApplicationKey)
                || e.Uri.PathAndQuery.Contains(_service.ApplicationId)
                || e.Uri.PathAndQuery.Contains("tos.php")
                || e.Uri.PathAndQuery.Contains("uiserver.php")
                || e.Uri.PathAndQuery.Contains("login_attempt"))
            {
                // Keep in this browser as long as it appears that we're in the context of this app.
                return;
            }
            else
            {
                // User did something other than log into the application.
                // Spawn a new webpage with the navigated URI and close this browser session.
                Process.Start(e.Uri.ToString());
                _service.ClearCachedCredentials();

                // User canceled (or something).  I can't reliably get the browser back to home in this case.
                _SwitchToErrorPage("An attempt was made to navigate to an unexpected URL.  Please restart Fishbowl to login", false, false);
                return;
            }
            */

            return;
        }

        private void _OnTryAgain(object sender, RoutedEventArgs e)
        {
            if (_service != null)
            {
                LoginBorder.Visibility = Visibility.Visible;
                ErrorBorder.Visibility = Visibility.Collapsed;
                _service.ClearCachedCredentials();
                LoginBrowser.Navigate(_service.GetLoginUri(_GrantedPermissionUri, _DeniedPermissionUri, _RequiredPermissions));
            }
        }
    }
}
