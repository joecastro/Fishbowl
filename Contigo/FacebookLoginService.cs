
namespace Contigo
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using Microsoft.Json.Serialization;
    using Standard;

    /// <summary>
    /// Extended permissions that the app can request beyond what Facebook normally allows.
    /// </summary>
    /// <remarks>
    /// According to facebook some of these are supersets of of others.  This relationship isn't reflected in the enum values.
    /// Last major update 5/27/2010 from http://developers.facebook.com/docs/authentication/permissions
    /// Minor update 3/19/2011 because despite SMS still being listed as a permission on the above page, the APIs are returning that it is invalid.
    /// Minor update 8/29/2011 because Facebook deprecated friend_photo_video_tags.  I haven't scrubbed the list to verify that there haven't been other changes.
    /// </remarks>
    // There are actually so many nuanced flags now that they aren't flaggable with an Int32!
    // Possible that it will be augmented to have too many for an Int64, so not even bothering with making it flaggable.
    // I think this privacy model is broken.  It's unlikely to stay this way so I don't want to build too much infrastructure
    // directly on top of it nor too much abstraction to insulate it.  This will need to be updated if/when Facebook changes this again.
    //[Flags]
    public enum Permissions
    {
        //None = 0,
        PublishStream,
        CreateEvent,
        RsvpEvent,
        OfflineAccess,

        Email,
        ReadInsights,
        ReadStream,

        UserAboutMe,
        FriendsAboutMe,
        UserActivities,
        FriendsActivities,
        UserBirthday,
        FriendsBirthday,
        UserEducationHistory,
        FriendsEducationHistory,
        UserEvents,
        FriendsEvents,
        UserGroups,
        FriendsGroups,
        UserHometown,
        FriendsHometown,
        UserInterests,
        FriendsInterests,
        UserLikes,
        FriendsLikes,
        UserLocation,
        FriendsLocation,
        UserNotes,
        FriendsNotes,
        UserOnlinePresence,
        FriendsOnlinePresence,
        UserPhotos,
        FriendsPhotos,
        UserRelationships,
        FriendsRelationships,
        UserReligionPolitics,
        FriendsReligionPolitics,
        UserStatus,
        FriendsStatus,
        UserVideos,
        FriendsVideos,
        UserWebsite,
        FriendsWebsite,
        UserWorkHistory,
        FriendsWorkHistory,
        ReadFriendLists,
        ReadRequests,

        // Old permissions, no longer documented, but still required by apps.
        ReadMailbox,
        PhotoUpload,
    }

    public class FacebookLoginService
    {
        private readonly ServiceSettings _settings;

        private FacebookWebApi _facebookApi;

        public string ApplicationId { get; private set; }
        public string ApplicationKey { get; private set; }

        public string SessionKey { get { return _settings.SessionKey; } }
        public string SessionSecret { get { return _settings.SessionSecret; } }
        public FacebookObjectId UserId { get { return _settings.UserId; } }

        public bool HasCachedSessionInfo { get; private set; }

        public FacebookLoginService(string applicationKey, string applicationId)
        {
            // The difference between these two strings is mostly arbitrary.
            // Callers need to be careful to ensure that they're using the correct one in the appropriate place.
            Verify.IsNeitherNullNorEmpty(applicationKey, "applicationKey");
            Verify.IsNeitherNullNorEmpty(applicationId, "applicationId");
            ApplicationKey = applicationKey;
            ApplicationId = applicationId;

            string settingPath = 
                Path.Combine(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Fishbowl"),
                    ApplicationId);

            _settings = ServiceSettings.Load(settingPath);
            _TryGetCachedSession();
        }

        public void ClearCachedCredentials()
        {
            _DeleteUserLoginCookie();
            _settings.ClearSessionInfo();
            _settings.Save();
        }

        private bool _TryGetCachedSession()
        {
            if (!_settings.HasSessionInfo)
            {
                HasCachedSessionInfo = false;
                return false;
            }

            // Properties are gotten from _settings.
            _facebookApi = new FacebookWebApi(ApplicationKey, SessionKey, UserId, SessionSecret);

            HasCachedSessionInfo = true;
            return true;
        }

        /// <summary>
        /// Start a new session by connecting to Facebook.
        /// </summary>
        /// <param name="authenticationToken">The authentication token to use.</param>
        public void InitiateNewSession(Uri sessionResponse)
        {
            string sessionPrefix = "session=";
            var badSessionInfoException = new ArgumentException("The session response does not contain connection information.", "sessionResponse");

            Verify.IsNotNull(sessionResponse, "sessionResponse");

            if (!string.IsNullOrEmpty(SessionKey))
            {
                throw new InvalidOperationException("This object already has a session.");
            }

            string jsonSessionInfo = sessionResponse.Query;
            jsonSessionInfo = Utility.UrlDecode(jsonSessionInfo);
            int startIndex = jsonSessionInfo.IndexOf(sessionPrefix);
            if (-1 == startIndex)
            {
                throw badSessionInfoException;
            }

            jsonSessionInfo = jsonSessionInfo.Substring(startIndex + sessionPrefix.Length);
            if (jsonSessionInfo.Length == 0 || jsonSessionInfo[0] != '{')
            {
                throw badSessionInfoException;
            }

            int curlyCount = 1;
            for (int i = 1; i < jsonSessionInfo.Length; ++i)
            {
                if (jsonSessionInfo[i] == '{')
                {
                    ++curlyCount;
                }
                if (jsonSessionInfo[i] == '}')
                {
                    --curlyCount;
                }

                if (curlyCount == 0)
                {
                    jsonSessionInfo = jsonSessionInfo.Substring(0, i + 1);
                    break;
                }
            }

            if (curlyCount != 0)
            {
                throw badSessionInfoException;
            }

            var serializer = new JsonSerializer(typeof(object));
            var sessionMap = (IDictionary<string, object>)serializer.Deserialize(jsonSessionInfo);

            object sessionKey;
            object userId;
            object secret;

            if (!sessionMap.TryGetValue("session_key", out sessionKey)
                || !sessionMap.TryGetValue("uid", out userId)
                || !sessionMap.TryGetValue("secret", out secret)
                || string.IsNullOrEmpty(sessionKey.ToString())
                || string.IsNullOrEmpty(userId.ToString())
                || string.IsNullOrEmpty(secret.ToString()))
            {
                throw badSessionInfoException;
            }

            _settings.SetSessionInfo(sessionKey.ToString(), secret.ToString(), new FacebookObjectId(userId.ToString()));
            _settings.Save();

            _facebookApi = new FacebookWebApi(ApplicationKey, SessionKey, UserId, SessionSecret);
            HasCachedSessionInfo = true;
        }

        /// <summary>Get the URI to host in a WebBrowser to allow a Facebook user to log into this application.</summary>
        /// <param name="authenticationToken"></param>
        /// <returns></returns>
        public Uri GetLoginUri(string successUri, string deniedUri, IEnumerable<Permissions> requiredPermissions)
        {
            return FacebookWebApi.GetLoginUri(ApplicationKey, successUri, deniedUri, requiredPermissions);
        }

        public void GetMissingPermissionsAsync(IEnumerable<Permissions> permissions, AsyncCompletedEventHandler callback)
        {
            Verify.IsNotNull(permissions, "permisions");
            Verify.IsNotNull(callback, "callback");

            Assert.IsNotNull(_facebookApi);
            Assert.IsNotNull(callback);

            Task.Factory.StartNew(() =>
            {
                Exception ex = null;
                Permissions[] missingPermissions = null;
                try
                {
                    missingPermissions = _facebookApi.GetMissingPermissions(permissions).ToArray();
                }
                catch (Exception e)
                {
                    ex = e;
                }
                callback(this, new AsyncCompletedEventArgs(ex, false, missingPermissions));
            });
        }

        // For signout, we need to delete all cookies for these Urls.
        // (Based on empirical observation; there may be more later we need to clean to ensure logout)
        private static readonly Uri FaceBookLoginUrl1 = new Uri("https://ssl.facebook.com/desktopapp.php");
        private static readonly Uri FaceBookLoginUrl2 = new Uri("https://login.facebook.com/login.php");

        private static void _DeleteUserLoginCookie()
        {
            _DeleteEveryCookie(FaceBookLoginUrl1);
            _DeleteEveryCookie(FaceBookLoginUrl2);
        }

        private static void _DeleteEveryCookie(Uri url)
        {
            string cookie = string.Empty;
            try
            {
                // Get every cookie (Expiration will not be in this response)
                cookie = Application.GetCookie(url);
            }
            catch (Win32Exception)
            {
                // "no more data is available" ... happens randomly so ignore it.
            }
            if (!string.IsNullOrEmpty(cookie))
            {
                // This may change eventually, but seems quite consistent for Facebook.com.
                // ... they split all values w/ ';' and put everything in foo=bar format.
                string[] values = cookie.Split(';');

                foreach (string s in values)
                {
                    if (s.IndexOf('=') > 0)
                    {
                        // Sets value to null with expiration date of yesterday.
                        _DeleteSingleCookie(s.Substring(0, s.IndexOf('=')).Trim(), url);
                    }
                }
            }
        }

        private static void _DeleteSingleCookie(string name, Uri url)
        {
            try
            {
                // Calculate "one day ago"
                DateTime expiration = DateTime.UtcNow - TimeSpan.FromDays(1);
                // Format the cookie as seen on FB.com.  Path and domain name are important factors here.
                string cookie = String.Format("{0}=; expires={1}; path=/; domain=.facebook.com", name, expiration.ToString("R"));
                // Set a single value from this cookie (doesnt work if you try to do all at once, for some reason)
                Application.SetCookie(url, cookie);
            }
            catch (Exception exc)
            {
                Assert.Fail(exc + " seen deleting a cookie.  If this is reasonable, add it to the list.");
            }
        }

    }
}
