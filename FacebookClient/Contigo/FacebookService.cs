
namespace Contigo
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Threading;
    using Standard;

    internal class GetCommentsForPhotoResponse
    {
        public bool CanComment;
        public IEnumerable<ActivityComment> Comments;
    }

    /// <summary>
    /// Provides access to Facebook data for a given user account.
    /// </summary>
    public class FacebookService : INotifyPropertyChanged
    {
        #region Fields

        private static readonly TimeSpan _RefreshThreshold = new TimeSpan(0, 3, 0);

        private readonly object _localLock = new object();

        private FacebookWebApi _facebookApi;

        private readonly Dictionary<string, FacebookContact> _userLookup = new Dictionary<string, FacebookContact>();
        private readonly Dictionary<string, FacebookPhoto> _photoLookup = new Dictionary<string, FacebookPhoto>();
        private readonly List<FacebookContact> _interestingPeople = new List<FacebookContact>();

        private DispatcherPool _userInteractionDispatcher;
        // These should have two threads, but using one for now to avoid the need for locking until I start using the update thread.
        private DispatcherPool _friendInfoDispatcher;
        private DispatcherPool _photoInfoDispatcher;
        private DispatcherPool _newsFeedDispatcher;

        private DispatcherPool _collectionUpdateDispatcher;

        private DispatcherTimer _refreshTimer;

        private ActivityFilter _newsFeedFilter;

        private PhotoAlbumSortOrder _albumSortValue;
        private ContactSortOrder _contactSortValue;

        private readonly ServiceSettings _settings;
        private readonly string _settingsPath;

        #endregion

        /// <summary>The Application's ID key provided by Facebook.</summary>
        public string ApplicationId { get; private set; }
        public Dispatcher Dispatcher { get; private set; }
        /// <summary>Once logged in, the key provided by the Facebook servers that identifies this session.</summary>
        public string SessionKey { get; private set; }
        /// <summary>The ID of the currently logged in user.  Requests to the server are made with this context.</summary>
        public string UserId { get; private set; }
        public bool IsOnline { get { return !string.IsNullOrEmpty(SessionKey); } }

        internal AsyncWebGetter WebGetter { get; private set; }

        internal MergeableCollection<Notification> RawNotifications { get; private set; }
        internal MergeableCollection<ActivityPost> RawNewsFeed { get; private set; }
        internal MergeableCollection<FacebookContact> RawFriends { get; private set; }
        internal MergeableCollection<FacebookPhotoAlbum> RawPhotoAlbums { get; private set; }
        internal MergeableCollection<ActivityFilter> RawFilters { get; private set; }

        public FacebookCollection<ActivityFilter> ActivityFilters { get; private set; }
        public FacebookCollection<Notification> Notifications { get; private set; }
        public FacebookContact MeContact { get; private set; }
        public ActivityPostCollection NewsFeed { get; private set; }
        public FacebookContactCollection Friends { get; private set; }
        public FacebookPhotoAlbumCollection PhotoAlbums { get; private set; }

        public SearchIndex SearchIndex { get; private set; }

        /// <summary>Utility for methods that require a valid session key.</summary>
        private void _VerifyOnline()
        {
            if (!IsOnline)
            {
                throw new InvalidOperationException("The session must be initiated before this method can be called.");
            }
        }

        /// <summary>Utility for methods that expect this object to not be connected.</summary>
        private void _VerifyNotOnline()
        {
            if (IsOnline)
            {
                throw new InvalidOperationException("A session is already associated with this object.");
            }
        }

        /// <summary>
        /// Create a new instance of the FacebookService.
        /// </summary>
        /// <param name="appId">The application id to use when communicating with Facebook.</param>
        /// <param name="dispatcher">The Dispatcher which thread is to be used for updating collections retrieved from this object.</param>
        public FacebookService(string appId, Dispatcher dispatcher)
        {
            Verify.IsNeitherNullNorEmpty(appId, "appId");
            Verify.IsNotNull(dispatcher, "dispatcher");

            _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft") + "\\" + this.GetType().Namespace + "\\" + appId + @"\";
            _settings = new ServiceSettings(_settingsPath);

            Dispatcher = dispatcher;
            ApplicationId = appId;

            MeContact = new FacebookContact(this)
            {
                HasData = true,
            };

            SearchIndex = new SearchIndex(this);

            RawNewsFeed = new MergeableCollection<ActivityPost>();
            RawFriends = new MergeableCollection<FacebookContact>();
            RawPhotoAlbums = new MergeableCollection<FacebookPhotoAlbum>();
            RawNotifications = new MergeableCollection<Notification>();
            RawFilters = new MergeableCollection<ActivityFilter>();

            NewsFeed = new ActivityPostCollection(RawNewsFeed, this, true);
            Friends = new FacebookContactCollection(RawFriends, this);
            PhotoAlbums = new FacebookPhotoAlbumCollection(RawPhotoAlbums, this, null);
            Notifications = new FacebookCollection<Notification>(RawNotifications, this);
            ActivityFilters = new FacebookCollection<ActivityFilter>(RawFilters, this);

            // Default sort orders
            ContactSortOrder = ContactSortOrder.AscendingByLastName;
            PhotoAlbumSortOrder = PhotoAlbumSortOrder.AscendingByFriend;
        }

        /// <summary>
        /// Associates an existing session from Facebook with this object.
        /// </summary>
        /// <param name="sessionKey"></param>
        /// <param name="userId"></param>
        public void RecoverSession(string sessionKey, string sessionSecret, string userId)
        {
            Dispatcher.VerifyAccess();
            _VerifyNotOnline();
            Verify.IsNeitherNullNorEmpty(sessionKey, "sessionKey");
            Verify.IsNeitherNullNorEmpty(userId, "userId");
            Verify.IsNeitherNullNorEmpty(sessionSecret, "sessionSecret");

            SessionKey = sessionKey;
            UserId = userId;

            _facebookApi = new FacebookWebApi(this, sessionSecret);

            WebGetter = new AsyncWebGetter(this.Dispatcher, _settingsPath);

            _userInteractionDispatcher = new DispatcherPool("FacebookServer: User Interactions", 1);
            _friendInfoDispatcher = new DispatcherPool("FacebookServer: Friend Info", 1);
            _photoInfoDispatcher = new DispatcherPool("FacebookServer: Photo Info", 3);
            _newsFeedDispatcher = new DispatcherPool("FacebookServer: NewsFeed", 1);

            _collectionUpdateDispatcher = new DispatcherPool("FacebookServer: Update Thread", 1);

            GetUserAsync(UserId, _PerformInitialSync);
        }

        private void _PerformInitialSync(object sender, AsyncCompletedEventArgs e)
        {
            Assert.IsFalse(Dispatcher.CheckAccess());

            if (e.Cancelled || e.Error != null)
            {
                // TODO: Something is really messed up.  We need a way to surface this error to the user.
                return; // ...
            }

            var newMeContact = (FacebookContact)e.UserState;
            newMeContact.HasData = true;
            newMeContact.InterestLevel = _settings.GetInterestLevel(UserId) ?? 1f;

            MeContact.Merge(newMeContact);

            _userLookup[UserId] = MeContact;
            _NotifyPropertyChanged("MeContact");

            Refresh();

            _refreshTimer = new DispatcherTimer(_RefreshThreshold, DispatcherPriority.Background, (sender2, e2) => Refresh(), Dispatcher);
            _refreshTimer.Start();
        }

        /// <summary>
        /// Disconnects this object from a Facebook session.
        /// Calls that require an active session are no longer usable.
        /// </summary>
        public void DisconnectSession(Action<string> deleteCacheCallback)
        {
            if (!IsOnline)
            {
                return;
            }

            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer = null;
            }

            Utility.SafeDispose(ref _facebookApi);
            Utility.SafeDispose(ref _userInteractionDispatcher);
            Utility.SafeDispose(ref _friendInfoDispatcher);
            Utility.SafeDispose(ref _photoInfoDispatcher);
            Utility.SafeDispose(ref _newsFeedDispatcher);
            Utility.SafeDispose(ref _collectionUpdateDispatcher);

            WebGetter.Shutdown(deleteCacheCallback);

            // Don't actually expire the session because it makes Facebook
            // think that we don't want offline access anymore.  The next time
            // the user logs in it will prompt them for it again.
            /*
            var expireMap = new METHOD_MAP
            {
                { "method", "auth.expireSession" },
            };

            _SendRequest(expireMap, true);
            */

            _settings.SaveSessionInfo(SessionKey, UserId);

            SessionKey = null;
            UserId = null;

            RawNewsFeed.Clear();
            RawFriends.Clear();
            RawPhotoAlbums.Clear();
            RawNotifications.Clear();
            RawFilters.Clear();

            _settings.Save();
        }

        public ActivityFilter NewsFeedFilter
        {
            get { return _newsFeedFilter; }
            set 
            {
                _newsFeedFilter = value;
                _NotifyPropertyChanged("NewsFeedFilter");
                RawNewsFeed.Clear();
                _UpdateNewsFeedAsync();                
            }
        }

        public PhotoAlbumSortOrder PhotoAlbumSortOrder
        {
            get { return _albumSortValue; }
            set
            {
                if (value != _albumSortValue)
                {
                    _albumSortValue = value;
                    RawPhotoAlbums.CustomComparison = FacebookPhotoAlbum.GetComparison(value);
                    _NotifyPropertyChanged("PhotoAlbumSortOrder");
                }
            }
        }

        public ContactSortOrder ContactSortOrder
        {
            get { return _contactSortValue; }
            set
            {
                if (value != _contactSortValue)
                {
                    _contactSortValue = value;
                    RawFriends.CustomComparison = FacebookContact.GetComparison(value);
                    _NotifyPropertyChanged("ContactSortOrder");
                }
            }
        }

        public void GetUserAsync(string userId, AsyncCompletedEventHandler callback)
        {
            Verify.IsNeitherNullNorEmpty(userId, "userId");
            Verify.IsNotNull(callback, "callback");

            FacebookContact fastContact;
            if (_userLookup.TryGetValue(userId, out fastContact))
            {
                callback(this, new AsyncCompletedEventArgs(null, false, fastContact));
                return;
            }

            _friendInfoDispatcher.QueueRequest((o) => 
            {
                // Treat this case as though it was canceled.
                if (!IsOnline)
                {
                    Dispatcher.BeginInvoke(callback, this, new AsyncCompletedEventArgs(null, true, null));
                    return;
                }

                // Maybe this contact was found while the request was in the queue.
                FacebookContact contact = null;

                if (_userLookup.TryGetValue(userId, out contact))
                {
                    Dispatcher.BeginInvoke(callback, this, new AsyncCompletedEventArgs(null, false, contact));
                    return;
                }

                try
                {
                    contact = _facebookApi.GetUser(userId);
                    Assert.IsNotNull(contact);

                    lock (_userLookup)
                    {
                        if (_userLookup.ContainsKey(userId))
                        {
                            contact = _userLookup[userId];
                        }
                        else
                        {
                            // If we have a persisted interest level for this contact then apply it before returning.
                            double? interestLevel = _settings.GetInterestLevel(userId);
                            if (interestLevel.HasValue)
                            {
                                contact.InterestLevel = interestLevel.Value;
                            }

                            _userLookup.Add(userId, contact);
                        }
                    }
                }
                catch (Exception e)
                {
                    Dispatcher.BeginInvoke(callback, this, new AsyncCompletedEventArgs(e, false, null));
                    return;
                }
                callback(this, new AsyncCompletedEventArgs(null, false, contact));
                return;
            }, null);
        }

        /* Unused
        internal MergeableCollection<FacebookPhotoTag> GetPhotoTags(FacebookPhoto photo)
        {
            Verify.IsNotNull(photo, "photo");

            var tagCollection = new MergeableCollection<FacebookPhotoTag>();
            _photoInfoDispatcher.QueueRequest(
                delegate
                {
                    List<FacebookPhotoTag> tagList = _facebookApi.GetPhotoTags(photo.PhotoId);
                    tagCollection.Merge(tagList, false);
                }, null);

            return tagCollection;
        }
        */

        public void AddPhotoTag(FacebookPhoto photo, FacebookContact contact, Point offset)
        {
            Verify.IsNotNull(photo, "photo");
            Verify.IsNotNull(contact, "contact");

            if (photo.OwnerId != UserId)
            {
                throw new InvalidOperationException("Unable to tag photos not owned by the user.");
            }

            _VerifyOnline();

            float x = (float)Math.Max(0, Math.Min(offset.X, 1));
            float y = (float)Math.Max(0, Math.Min(offset.Y, 1));

            _userInteractionDispatcher.QueueRequest((o) =>
            {
                List<FacebookPhotoTag> newTags = null;
                try
                {
                    newTags = _facebookApi.AddPhotoTag(photo.PhotoId, contact.UserId, x, y);
                }
                catch (Exception)
                { 
                    // Consider making this an explicit async and handling the failure.
                }
                Action act = () => photo.RawTags.Merge(newTags, false);
                Dispatcher.BeginInvoke(act, DispatcherPriority.Normal);
            }, null);
        }

        public FacebookPhotoAlbum CreatePhotoAlbum(string name, string description, string location)
        {
            Verify.IsNeitherNullNorEmpty(name, "name");

            _VerifyOnline();

            FacebookPhotoAlbum album = _facebookApi.CreateAlbum(name, description, location);
            RawPhotoAlbums.Add(album);

            _UpdatePhotoAlbumsAsync();

            return album;
        }

        public FacebookPhoto AddPhotoToAlbum(FacebookPhotoAlbum album, string caption, string imageFile)
        {
            Verify.IsNotNull(album, "album");

            _VerifyOnline();

            FacebookPhoto photo = _facebookApi.AddPhotoToAlbum(album.AlbumId, caption, imageFile);

            album.RawPhotos.Add(photo);
            return photo;
        }

        public FacebookPhoto AddPhotoToApplicationAlbum(string caption, string imageFile)
        {
            _VerifyOnline();
            FacebookPhoto photo = _facebookApi.AddPhotoToAlbum(null, caption, imageFile);

            _photoInfoDispatcher.QueueRequest(delegate
            {
                // Check if we already knew about this album.  If not, then add it to the app.
                if (!(from album in RawPhotoAlbums where album.AlbumId == photo.AlbumId select album).Any())
                {
                    FacebookPhotoAlbum appAlbum = _facebookApi.GetAlbum(photo.AlbumId);
                    if (appAlbum == null)
                    {
                        throw new FacebookException("The application album could not be created.", null);
                    }
                    appAlbum.RawPhotos = new MergeableCollection<FacebookPhoto>(new[] { photo });
                    RawPhotoAlbums.Add(appAlbum);
                }
            }, null);

            _userInteractionDispatcher.QueueRequest(delegate
             {
                 // If we sync immediately the feed data won't include this update, so try in 5 seconds.
                 System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));
                 _UpdateNewsFeedAsync();
             }, null);

            return photo;
        }

        public void WriteOnWall(FacebookContact contact, string comment)
        {
            _VerifyOnline();
            Verify.IsNotNull(contact, "contact");
            Verify.IsNeitherNullNorWhitespace(comment, "comment");

            if (contact.UserId == UserId)
            {
                // Shouldn't be misusing the APIs like this.
                Assert.Fail();
                UpdateStatus(comment);
            }

            _userInteractionDispatcher.QueueRequest(
            delegate
            {
                ActivityPost updatedStatus = _facebookApi.PublishStream(contact.UserId, comment);
                contact.RawRecentActivity.Add(updatedStatus);
                RawNewsFeed.Add(updatedStatus);
                // If we sync immediately the feed data won't include this update, so try in 5 seconds.
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));
                _UpdateNewsFeedAsync();
                contact.UpdateRecentActivity();
            }, null);
        }

        public void UpdateStatus(string newStatus)
        {
            _VerifyOnline();

            Verify.IsNeitherNullNorWhitespace(newStatus, "newStatus");

            _userInteractionDispatcher.QueueRequest(
                delegate
                {
                    ActivityPost updatedStatus = _facebookApi.UpdateStatus(newStatus);
                    RawNewsFeed.Add(updatedStatus);
                    MeContact.StatusMessage = updatedStatus;
                    // If we sync immediately the feed data won't include this update, so try in 5 seconds.
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));
                    _UpdateNewsFeedAsync();
                }, null);
        }

        public void UpdateStatus(string newStatus, string uri)
        {
            _VerifyOnline();

            // Should do some validation here before trying to post an invalid URL...
            if (string.IsNullOrEmpty(uri) || "" == uri.Trim())
            {
                UpdateStatus(newStatus);
            }

            Verify.IsNeitherNullNorWhitespace(newStatus, "newStatus");

            _userInteractionDispatcher.QueueRequest(
                delegate
                {
                    _facebookApi.PostLink(newStatus, uri);

                    // If we sync immediately the feed data won't include this update, so try in 5 seconds.
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));
                    _UpdateNewsFeedAsync();
                }, null);
        }

        public void AddComment(ActivityPost post, string comment)
        {
            _VerifyOnline();

            Verify.IsNotNull(post, "post");
            Verify.IsNeitherNullNorEmpty(comment, "comment");

            if (post.CanComment)
            {
                _userInteractionDispatcher.QueueRequest(
                delegate
                {
                    string commentId = _facebookApi.AddComment(post, comment);
                    if (!string.IsNullOrEmpty(commentId))
                    {
                        var activityComment = new ActivityComment(this)
                        {
                            CommentType = ActivityComment.Type.ActivityPost,
                            CommentId = commentId,
                            FromUserId = UserId,
                            Text = comment,
                            Time = DateTime.Now,
                        };

                        post.RawComments.Add(activityComment);
                    }

                    // Start up a background sync of the NewsFeed.
                    _UpdateNewsFeedAsync();
                }, null);
            }
        }

        public void AddComment(FacebookPhoto photo, string comment)
        {
            _VerifyOnline();

            Verify.IsNotNull(photo, "photo");
            Verify.IsNeitherNullNorEmpty(comment, "comment");

            if (photo.CanComment)
            {
                _userInteractionDispatcher.QueueRequest(
                delegate
                {
                    string commentId = _facebookApi.AddPhotoComment(photo.PhotoId, comment);
                    if (!string.IsNullOrEmpty(commentId))
                    {
                        var activityComment = new ActivityComment(this)
                        {
                            CommentType = ActivityComment.Type.Photo,
                            CommentId = commentId,
                            FromUserId = UserId,
                            Text = comment,
                            Time = DateTime.Now,
                        };

                        photo.RawComments.Add(activityComment);
                    }

                    // Tell the photo to resync its comments.
                    photo.RequeryComments();
                }, null);
            }
        }

        private List<ActivityComment> _GetCommentsWorker(ActivityPost post)
        {
            Verify.IsNotNull(post, "post");

            _VerifyOnline();

            try
            {
                return _facebookApi.GetComments(post);
            }
            catch (Exception)
            {
                return new List<ActivityComment>();
            }
        }

        public void GetMoreComments(ActivityPost post)
        {
            Verify.IsNotNull(post, "post");

            post.GetMoreComments();
        }

        public void RemoveComment(ActivityComment comment)
        {
            Verify.IsNotNull(comment, "comment");

            _VerifyOnline();

            if (comment.CommentType != ActivityComment.Type.ActivityPost)
            {
                throw new InvalidOperationException("This comment cannot be removed");
            }

            _facebookApi.RemoveComment(comment.CommentId);

            ActivityPost post = comment.Post;
            if (post != null)
            {
                post.RawComments.Remove(comment);
            }

            // Start up a background sync of the NewsFeed.
            _UpdateNewsFeedAsync();
        }

        public void AddLike(ActivityPost post)
        {
            _VerifyOnline();

            Verify.IsNotNull(post, "post");

            if (post.CanLike)
            {
                _userInteractionDispatcher.QueueRequest(
                    delegate
                    {
                        // Do this optimistically;
                        post.HasLiked = true;
                        ++post.LikedCount;

                        try
                        {
                            _facebookApi.AddLike(post.PostId);
                        }
                        catch
                        {
                            post.HasLiked = false;
                            --post.LikedCount;
                            throw;
                        }

                        // Start up a background sync of the NewsFeed.
                        _UpdateNewsFeedAsync();
                    }, null);
            }
        }

        public void RemoveLike(ActivityPost post)
        {
            _VerifyOnline();

            Verify.IsNotNull(post, "post");

            if (post.HasLiked)
            {
                _userInteractionDispatcher.QueueRequest(
                    delegate
                    {
                        // Do this optimistically
                        post.HasLiked = false;
                        --post.LikedCount;

                        try
                        {
                            _facebookApi.RemoveLike(post.PostId);
                        }
                        catch
                        {
                            post.HasLiked = true;
                            ++post.LikedCount;
                            throw;
                        }

                        // Start up a background sync of the NewsFeed.
                        _UpdateNewsFeedAsync();
                    }, null);
            }
        }

        public void ReadNotification(Notification notification)
        {
            _VerifyOnline();
            Verify.IsNotNull(notification, "notification");

            if (notification.IsUnread)
            {
                notification.IsUnread = false;
                RawNotifications.Remove(notification);

                _userInteractionDispatcher.QueueRequest(
                    delegate
                    {
                        if (!string.IsNullOrEmpty(notification.NotificationId))
                        {
                            _facebookApi.MarkNotificationsAsRead(notification.NotificationId);
                        }
                    }, null);
            }
        }

        public void Refresh()
        {
            if (!IsOnline)
            {
                return;
            }

            _UpdateNewsFeedAsync();
            _UpdateFriendsAsync();
            _UpdatePhotoAlbumsAsync();
            _UpdateNotificationsAsync();
            _UpdateFiltersAsync();

            //TODO: this is incomplete.
        }

        private void _NotifyPropertyChanged(string propertyName)
        {
            Assert.IsNeitherNullNorEmpty(propertyName);

            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Asynchronous Queries

        private void _UpdateFiltersAsync()
        {
            _newsFeedDispatcher.QueueRequest(_UpdateFiltersWorker, null);
        }

        private void _UpdateNewsFeedAsync()
        {
            _newsFeedDispatcher.QueueRequest(_UpdateNewsFeedWorker, null);
        }

        private void _UpdateNotificationsAsync()
        {
            _newsFeedDispatcher.QueueRequest(_UpdateNotificationsWorker, null);
        }


        private void _UpdateFriendsAsync()
        {
            _friendInfoDispatcher.QueueRequest(_UpdateFriendsWorker, null);
        }

        private void _UpdatePhotoAlbumsAsync()
        {
            _photoInfoDispatcher.QueueRequest(_UpdateInterestingPhotoAlbumsWorker, null);
            _photoInfoDispatcher.QueueRequest(_UpdateFriendsPhotoAlbumsWorker, null);
        }

        internal void GetActivityPostsByUserAsync(string userId, AsyncCompletedEventHandler callback)
        {
            Assert.IsNeitherNullNorEmpty(userId);
            Assert.IsNotNull(callback);

            _friendInfoDispatcher.QueueRequest(unused =>
            {
                if (IsOnline)
                {
                    List<ActivityPost> posts;
                    try
                    {
                        // This has been timing out from Facebook.
                        posts = _facebookApi.GetStreamPosts(userId);
                    }
                    catch
                    {
                        // No update this time.
                        return;
                    }

                    callback(this, new AsyncCompletedEventArgs(null, false, posts));
                }
            }, null);
        }

        internal void GetCommentsForPostAsync(ActivityPost post, AsyncCompletedEventHandler callback)
        {
            _newsFeedDispatcher.QueueRequest(unused =>
            {
                if (IsOnline)
                {
                    callback(this, new AsyncCompletedEventArgs(null, false, _GetCommentsWorker(post)));
                }
            }, null);
        }

        internal void GetCommentsForPhotoAsync(FacebookPhoto photo, AsyncCompletedEventHandler callback)
        {
            Assert.IsNotNull(photo);
            Assert.IsNotNull(callback);

            _photoInfoDispatcher.QueueRequest(unused =>
            {
                IEnumerable<ActivityComment> comments = null;
                bool canComment = false;
                
                if (!IsOnline)
                {
                    return;
                }

                try
                {
                    comments = _facebookApi.GetPhotoComments(photo.PhotoId);
                    canComment = _facebookApi.GetPhotoCanComment(photo.PhotoId);
                }
                catch (Exception e)
                {
                    if (!IsOnline)
                    {
                        Dispatcher.BeginInvoke(callback, this, new AsyncCompletedEventArgs(null, true, null));
                        return;
                    }

                    Dispatcher.BeginInvoke(callback, this, new AsyncCompletedEventArgs(e, false, null));
                    return;
                }

                Dispatcher.BeginInvoke(callback, this, new AsyncCompletedEventArgs(null, false, new GetCommentsForPhotoResponse { CanComment = canComment, Comments = comments }));
            }, null);
        }

        private void _UpdateFiltersWorker(object parameter)
        {
            if (!IsOnline)
            {
                return;
            }

            List<ActivityFilter> filters = _facebookApi.GetActivityFilters();
            RawFilters.Merge(filters, false);
        }

        private void _UpdateNewsFeedWorker(object parameter)
        {
            if (!IsOnline)
            {
                return;
            }

            List<ActivityPost> posts;
            List<FacebookContact> users;

            string filterKey = null;
            if (_newsFeedFilter != null)
            {
                filterKey = _newsFeedFilter.Key;
            }

            try
            {
                // This has been timing out from Facebook.
                _facebookApi.GetStream(filterKey, out posts, out users);
            }
            catch
            {
                // No update this time.
                return;
            }

            lock (_userLookup)
            {
                foreach (FacebookContact streamUser in users)
                {
                    FacebookContact foundContact;
                    if (!_userLookup.TryGetValue(streamUser.UserId, out foundContact))
                    {
                        // If we have a persisted interest level for this contact then apply it before returning.
                        double? interestLevel = _settings.GetInterestLevel(streamUser.UserId);
                        if (interestLevel.HasValue)
                        {
                            streamUser.InterestLevel = interestLevel.Value;
                        }
                        _userLookup.Add(streamUser.UserId, streamUser);
                    }
                    else
                    {
                        // For the most part the profile data won't be better info than we can get through the User info,
                        // Except sometimes the User's images are blank but the stream's isn't.
                        foundContact.MergeImage(streamUser.Image);
                    }
                }
            }

            // Check that we still have the same filter applied...
            if (_newsFeedFilter != null)
            {
                if (_newsFeedFilter.Key != filterKey)
                {
                    return;
                }
            }
            else
            {
                if (filterKey != null)
                {
                    return;
                }
            }

            if (IsOnline)
            {
                RawNewsFeed.Merge(posts, false);
            }

            if (_facebookApi != null)
            {
                FacebookContact meContact = _facebookApi.GetUser(UserId);
                meContact.HasData = true;
                MeContact.Merge(meContact);
            }
        }

        private void _UpdateNotificationsWorker(object sender)
        {
            List<Notification> notifications = null;
            if (!IsOnline)
            {
                return;
            }

            try
            {
                notifications = _facebookApi.GetNotifications(false);
            }
            catch (FacebookException)
            {
                // Bad facebook.  No notifications for us right now.
                return;
            }

            if (!IsOnline)
            {
                return;
            }
            notifications.AddRange(_facebookApi.GetRequests());
            RawNotifications.Merge(notifications, false);
        }

        private void _UpdateFriendsWorker(object parameter)
        {
            if (!IsOnline)
            {
                return;
            }

            List<FacebookContact> friendsList = _facebookApi.GetFriends();
                
            // These contacts are known to be friends of UserId.
            foreach (var friend in friendsList)
            {
                friend.HasData = true;
            }

            lock (_userLookup)
            {
                if (IsOnline)
                {
                    RawFriends.Merge(friendsList, false);
                }

                if (IsOnline)
                {
                    foreach (FacebookContact friend in friendsList)
                    {
                        FacebookContact lookedUpFriend;
                        if (!_userLookup.TryGetValue(friend.UserId, out lookedUpFriend))
                        {
                            // If we have a persisted interest level for this contact then apply it before returning.
                            double? interestLevel = _settings.GetInterestLevel(friend.UserId);
                            if (interestLevel.HasValue)
                            {
                                friend.InterestLevel = interestLevel.Value;
                            }
                            _userLookup.Add(friend.UserId, friend);
                        }
                        else
                        {
                            lookedUpFriend.Merge(friend);
                        }
                    }
                }
            }
        }

        private void _UpdateFriendsPhotoAlbumsWorker(object parameter)
        {
            if (!IsOnline)
            {
                return;
            }

            List<FacebookPhotoAlbum> albums = _facebookApi.GetFriendsPhotoAlbums();

            _GetPhotosForAlbumsWorker(albums);
        }

        private void _UpdateInterestingPhotoAlbumsWorker(object parameter)
        {
            if (!IsOnline)
            {
                return;
            }

            if (parameter == null)
            {
                FacebookContact[] interestingCopy;
                lock (_localLock)
                {
                    interestingCopy = new FacebookContact[_interestingPeople.Count];
                    _interestingPeople.CopyTo(interestingCopy); 
                }

                foreach (var contact in interestingCopy)
                {
                    if (!IsOnline)
                    {
                        return;
                    }

                    List<FacebookPhotoAlbum> albums = _facebookApi.GetUserAlbums(contact.UserId);
                    _GetPhotosForAlbumsWorker(albums);
                }
            }
            else
            {
                var userId = (string)parameter;
                List<FacebookPhotoAlbum> albums = _facebookApi.GetUserAlbums(userId);
                _GetPhotosForAlbumsWorker(albums);
            }
        }

        private void _GetPhotosForAlbumsWorker(List<FacebookPhotoAlbum> albums)
        {
            int batchCount = 5;
            for (int i = 0; i < albums.Count; i += batchCount)
            {
                var batchAlbums = (from album in albums.Sublist(i, i + batchCount) select album).ToArray();
                List<FacebookPhoto>[] photoCollections;
                try
                {
                    if (!IsOnline)
                    {
                        return;
                    }
                    photoCollections = _facebookApi.GetPhotosWithTags(from album in batchAlbums select album.AlbumId);
                }
                catch
                {
                    if (!IsOnline)
                    {
                        return;
                    }
                    continue;
                }

                for (int currentAlbum = 0; currentAlbum < batchAlbums.Length; ++currentAlbum)
                {
                    batchAlbums[currentAlbum].RawPhotos = new MergeableCollection<FacebookPhoto>(photoCollections[currentAlbum]);
                }

                if (!IsOnline)
                {
                    return;
                }
                Dispatcher.Invoke((Action)(() => RawPhotoAlbums.Merge(batchAlbums, true)));
            }
        }

        #endregion Asynchronous Queries

        public void Dispose()
        {
            Shutdown(null);
        }

        public void Shutdown(Action<string> deleteCallback)
        {
            DisconnectSession(deleteCallback);
        }

        // Interesting people we want to get all the available information for.
        internal void TagAsInteresting(FacebookContact contact)
        {
            Verify.IsNotNull(contact, "contact");

            _settings.AddInterestLevel(contact.UserId, contact.InterestLevel);

            if (contact.InterestLevel >= .75)
            {
                lock (_localLock)
                {
                    _interestingPeople.Add(contact);
                }
                if (_photoInfoDispatcher != null)
                {
                    _photoInfoDispatcher.QueueRequest(_UpdateInterestingPhotoAlbumsWorker, contact.UserId);
                }
            }
            else
            {
                lock (_localLock)
                {
                    _interestingPeople.Remove(contact);
                }
            }

            if (_albumSortValue == PhotoAlbumSortOrder.AscendingByInterestLevel || _albumSortValue == PhotoAlbumSortOrder.DescendingByInterestLevel)
            {
                RawPhotoAlbums.RefreshSort();
            }
            if (_contactSortValue == ContactSortOrder.AscendingByInterestLevel || _contactSortValue == ContactSortOrder.DescendingByInterestLevel)
            {
                RawFriends.RefreshSort();
            }
        }

        internal double GetInterestLevelForUserId(string userId)
        {
            lock (_userLookup)
            {
                FacebookContact contact;
                if (_userLookup.TryGetValue(userId, out contact))
                {
                    return contact.InterestLevel;
                }
            }

            return _settings.GetInterestLevel(userId) ?? FacebookContact.DefaultInterestLevel;
        }
    }
}
