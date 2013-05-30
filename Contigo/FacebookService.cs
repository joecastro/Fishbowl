namespace Contigo
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
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

        // Lock for accessing _interestingPeople
        private readonly object _localLock = new object();

        private FacebookWebApi _facebookApi;

        private bool _hasFetchedFriendsList = false;
        private readonly ReaderWriterLockSlim _userLookupLock = new ReaderWriterLockSlim();
        private readonly Dictionary<FacebookObjectId, FacebookContact> _userLookup = new Dictionary<FacebookObjectId, FacebookContact>();
        private readonly Dictionary<FacebookObjectId, FacebookPhoto> _photoLookup = new Dictionary<FacebookObjectId, FacebookPhoto>();
        private readonly List<FacebookContact> _interestingPeople = new List<FacebookContact>();

        private DispatcherPool _userInteractionDispatcher;
        // These should have two threads, but using one for now to avoid the need for locking until I start using the update thread.
        private DispatcherPool _friendInfoDispatcher;
        private DispatcherPool _photoInfoDispatcher;
        private DispatcherPool _newsFeedDispatcher;

        private DispatcherPool _collectionUpdateDispatcher;

        // Fudge the numbers a bit to avoid the timers getting hit all at the same time.
        private static readonly TimeSpan _RefreshThresholdDynamic = TimeSpan.FromMinutes(2.9);
        private static readonly TimeSpan _RefreshThresholdModerate = TimeSpan.FromMinutes(15.1);
        private static readonly TimeSpan _RefreshThresholdInfrequent = TimeSpan.FromHours(1.01);

        private readonly DispatcherTimer _refreshTimerDynamic;
        private readonly DispatcherTimer _refreshTimerModerate;
        private readonly DispatcherTimer _refreshTimerInfrequent;

        private ActivityFilter _newsFeedFilter;

        private PhotoAlbumSortOrder _albumSortValue;
        private ContactSortOrder _contactSortValue;

        private readonly ServiceSettings _settings;
        private readonly string _settingsPath;

        private DateTime _mostRecentNewsfeedItem = DateTime.MinValue;

        #endregion

        /// <summary>The Application's ID key provided by Facebook.</summary>
        public string ApplicationId { get; private set; }
        public string ApplicationKey { get; private set; }

        public Dispatcher Dispatcher { get; private set; }

        /// <summary>Once logged in, the key provided by the Facebook servers that identifies this session.</summary>
        public string SessionKey { get; private set; }
        
        /// <summary>The ID of the currently logged in user.  Requests to the server are made with this context.</summary>
        public FacebookObjectId UserId { get; private set; }
        
        public bool IsOnline { get { return !string.IsNullOrEmpty(SessionKey); } }

        internal AsyncWebGetter WebGetter { get; private set; }

        internal FBMergeableCollection<Notification> RawNotifications { get; private set; }

        internal FBMergeableCollection<ActivityPost> RawNewsFeed { get; private set; }
        internal FBMergeableCollection<FacebookContact> RawFriends { get; private set; }
        internal FBMergeableCollection<FacebookPhotoAlbum> RawPhotoAlbums { get; private set; }
        internal FBMergeableCollection<ActivityFilter> RawFilters { get; private set; }

        public FacebookCollection<ActivityFilter> ActivityFilters { get; private set; }
        public FacebookCollection<Notification> Notifications { get; private set; }
        public FacebookContact MeContact { get; private set; }
        public ActivityPostCollection NewsFeed { get; private set; }
        public FacebookContactCollection Friends { get; private set; }
        public FacebookContactCollection OnlineFriends { get; private set; }
        public FacebookPhotoAlbumCollection PhotoAlbums { get; private set; }

        public SearchIndex SearchIndex { get; private set; }

        public FacebookCollection<MessageNotification> InboxNotifications { get; private set; }
        internal FBMergeableCollection<MessageNotification> RawInbox { get; private set; }

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
        public FacebookService(string appId, string appKey, Dispatcher dispatcher)
        {
            Verify.IsNeitherNullNorEmpty(appId, "appId");
            Verify.IsNeitherNullNorEmpty(appKey, "appKey");
            Verify.IsNotNull(dispatcher, "dispatcher");

            ApplicationId = appId;
            ApplicationKey = appKey;
            Dispatcher = dispatcher;

            _settingsPath = 
                Path.Combine(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Fishbowl"),
                    ApplicationId);

            _settings = ServiceSettings.Load(_settingsPath);

            MeContact = new FacebookContact(this);

            SearchIndex = new SearchIndex(this);

            RawNewsFeed = new FBMergeableCollection<ActivityPost>();
            RawFriends = new FBMergeableCollection<FacebookContact>();
            RawPhotoAlbums = new FBMergeableCollection<FacebookPhotoAlbum>();
            RawNotifications = new FBMergeableCollection<Notification>();
            RawFilters = new FBMergeableCollection<ActivityFilter>();

            NewsFeed = new ActivityPostCollection(RawNewsFeed, this, true);
            Friends = new FacebookContactCollection(RawFriends, this, false);
            OnlineFriends = new FacebookContactCollection(RawFriends, this, true);
            PhotoAlbums = new FacebookPhotoAlbumCollection(RawPhotoAlbums, this, null);
            Notifications = new FacebookCollection<Notification>(RawNotifications, this);
            RawInbox = new FBMergeableCollection<MessageNotification>();
            InboxNotifications = new FacebookCollection<MessageNotification>(RawInbox, this);
            ActivityFilters = new FacebookCollection<ActivityFilter>(RawFilters, this);

            // Default sort orders
            ContactSortOrder = ContactSortOrder.AscendingByLastName;
            PhotoAlbumSortOrder = PhotoAlbumSortOrder.DescendingByUpdate;

            _refreshTimerDynamic = new DispatcherTimer(_RefreshThresholdDynamic, DispatcherPriority.Background, (sender2, e2) => _RefreshDynamic(), Dispatcher);
            _refreshTimerModerate = new DispatcherTimer(_RefreshThresholdModerate, DispatcherPriority.Background, (sender2, e2) => _RefreshModerate(), Dispatcher);
            _refreshTimerInfrequent = new DispatcherTimer(_RefreshThresholdInfrequent, DispatcherPriority.Background, (sender2, e2) => _RefreshInfrequent(), Dispatcher);
        }

        /// <summary>
        /// Associates an existing session from Facebook with this object.
        /// </summary>
        /// <param name="sessionKey"></param>
        /// <param name="userId"></param>
        public void RecoverSession(string sessionKey, string sessionSecret, FacebookObjectId userId)
        {
            Dispatcher.VerifyAccess();

            _settings.SetSessionInfo(sessionKey, sessionSecret, userId);

            _VerifyNotOnline();
            Verify.IsNeitherNullNorEmpty(sessionKey, "sessionKey");
            Verify.IsTrue(FacebookObjectId.IsValid(userId), "Invalid userId");
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

            _userLookupLock.EnterWriteLock();
            try
            {
                MeContact.UserId = UserId;
                MeContact.ProfileUri = new Uri("http://facebook.com/profile.php?id=" + UserId);
                MeContact.InterestLevel = _settings.GetInterestLevel(UserId) ?? 1f;
                _userLookup.Add(UserId, MeContact);
            }
            finally 
            {
                _userLookupLock.ExitWriteLock();
            }
            
            GetUser(UserId);

            _friendInfoDispatcher.QueueRequest(_PerformInitialSync, null);
        }

        private void _PerformInitialSync(object unused)
        {
            Assert.IsFalse(Dispatcher.CheckAccess());
            
            _RefreshFirstTime();

            _refreshTimerDynamic.Start();
            _refreshTimerModerate.Start();
            _refreshTimerInfrequent.Start();
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

            _refreshTimerDynamic.Stop();
            _refreshTimerModerate.Stop();
            _refreshTimerInfrequent.Stop();

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

            SessionKey = null;
            UserId = default(FacebookObjectId);

            RawNewsFeed.Clear();
            RawFriends.Clear();
            RawPhotoAlbums.Clear();
            RawNotifications.Clear();
            RawInbox.Clear();
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
                _newsFeedDispatcher.QueueRequest((o) =>
                {
                    RawNewsFeed.Clear();
                    // We want to get all the data again.
                    _mostRecentNewsfeedItem = DateTime.MinValue;
                    _UpdateNewsFeedAsync();                
                }, null);
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

        public FacebookContact GetUser(FacebookObjectId userId)
        {
            Verify.IsTrue(FacebookObjectId.IsValid(userId), "Invalid userId");

            FacebookContact retContact = null;
            _userLookupLock.EnterUpgradeableReadLock();
            try
            {
                if (_userLookup.TryGetValue(userId, out retContact))
                {
                    return retContact;
                }
                
                retContact = new FacebookContact(this)
                {
                    UserId = userId,
                    ProfileUri = new Uri("http://facebook.com/profile.php?id=" + userId),
                    Name = "Someone",
                };
                double? interestLevel = _settings.GetInterestLevel(userId);
                if (interestLevel.HasValue)
                {
                    retContact.InterestLevel = interestLevel.Value;
                }

                _userLookupLock.EnterWriteLock();
                try
                {
                    _userLookup.Add(userId, retContact);
                }
                finally
                {
                    _userLookupLock.ExitWriteLock();
                }
            }
            finally
            {
                _userLookupLock.ExitUpgradeableReadLock();
            }

            _friendInfoDispatcher.QueueRequest((o) =>
            {
                if (!IsOnline)
                {
                    return;
                }

                if (userId != this.UserId && !_hasFetchedFriendsList)
                {
                    _UpdateFriendsWorker(null);
                }

                FacebookContact contact;
                try
                {
                    contact = _facebookApi.TryGetUser(userId, false);
                    if (contact == null)
                    {
                        return;
                    }

                    double? interestLevel = _settings.GetInterestLevel(userId);
                    if (interestLevel.HasValue)
                    {
                        contact.InterestLevel = interestLevel.Value;
                    }
                }
                catch (FacebookException)
                {
                    return;
                }

                _userLookupLock.EnterReadLock();
                try
                {
                    _userLookup[userId].Merge(contact);
                }
                finally
                {
                    _userLookupLock.ExitReadLock();
                }
            }, null);

            return retContact;
        }

        /* Unused
        internal FBMergeableCollection<FacebookPhotoTag> GetPhotoTags(FacebookPhoto photo)
        {
            Verify.IsNotNull(photo, "photo");

            var tagCollection = new FBMergeableCollection<FacebookPhotoTag>();
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
            FacebookPhoto photo = _facebookApi.AddPhotoToAlbum(default(FacebookObjectId), caption, imageFile);

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
                    appAlbum.RawPhotos = new FBMergeableCollection<FacebookPhoto>(new[] { photo });
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
                    
                    // Be careful pushing this into the newsfeed because we don't have a valid PostId.
                    // I'm switching from stream.publish to status.set because posts, even without
                    // attachments, aren't showing up as true statuses...
                    // Since we don't have a PostId with the status.set call, I need to know how to remove it.
                    // This seems like a bug in Facebook's APIs though, so maybe I can change this back soon.
                    Assert.AreEqual(new FacebookObjectId("FakeStatusId"), updatedStatus.PostId);

                    // If there are multiple fake status updates then this will replace the last one.
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
                    FacebookObjectId commentId = _facebookApi.AddComment(post, comment);
                    if (FacebookObjectId.IsValid(commentId))
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
                    FacebookObjectId commentId = _facebookApi.AddPhotoComment(photo.PhotoId, comment);
                    if (FacebookObjectId.IsValid(commentId))
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

            var friendRequest = notification as FriendRequestNotification;
            if (friendRequest != null)
            {
                _ReadFriendRequestNotification(friendRequest);
                return;
            }

            var unfriendNotification = notification as UnfriendNotification;
            if (unfriendNotification != null)
            {
                _ReadUnfriendNotification(unfriendNotification);
                return;
            }

            // Near as I can tell there's currently no way to mark messages as read.  Silently do nothing... :(
            var message = notification as MessageNotification;
            if (message != null)
            {
                return;
            }

            if (notification.IsUnread)
            {
                notification.IsUnread = false;
                RawNotifications.Remove(notification);

                if (FacebookObjectId.IsValid(notification.NotificationId))
                {
                    _userInteractionDispatcher.QueueRequest((obj) => _facebookApi.MarkNotificationsAsRead(notification.NotificationId), null);
                }
            }
        }

        private void _ReadFriendRequestNotification(FriendRequestNotification friendRequest)
        {
            friendRequest.IsUnread = false;
            RawNotifications.Remove(friendRequest);
            _userInteractionDispatcher.QueueRequest((obj) => _settings.MarkFriendRequestAsRead(friendRequest.SenderId), null);
        }

        private void _ReadUnfriendNotification(UnfriendNotification unfriendNotification)
        {
            unfriendNotification.IsUnread = false;
            RawNotifications.Remove(unfriendNotification);
            _userInteractionDispatcher.QueueRequest((obj) => _settings.MarkUnfriendNotificationAsRead(unfriendNotification.SenderId), null);
        }

        // Refresh data that needs to be frequently requeried.
        private void _RefreshDynamic()
        {
            if (!IsOnline)
            {
                return;
            }

            _UpdateNewsFeedAsync();
            _UpdateFriendsOnlineStatusAsync();
            _UpdateNotificationsAsync();
        }

        private void _RefreshModerate()
        {
            if (!IsOnline)
            {
                return;
            }

            _UpdateFriendsAsync();
            _UpdatePhotoAlbumsAsync();
        }

        private void _RefreshInfrequent()
        {
            if (!IsOnline)
            {
                return;
            }

            _UpdateFiltersAsync();
        }
        
        private void _RefreshFirstTime()
        {
            _UpdateFiltersAsync();
            _UpdateNewsFeedAsync();
            _UpdateFriendsAsync();
            _UpdatePhotoAlbumsAsync();
            _UpdateNotificationsAsync();
        }

        public void Refresh()
        {
            _RefreshDynamic();
            _RefreshModerate();
            _RefreshInfrequent();
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
            _newsFeedDispatcher.QueueRequest(_UpdateNewsFeedWorker, false);
        }

        private void _UpdateNotificationsAsync()
        {
            _newsFeedDispatcher.QueueRequest(_UpdateNotificationsWorker, null);
        }

        private void _UpdateFriendsAsync()
        {
            _friendInfoDispatcher.QueueRequest(_UpdateFriendsWorker, null);
            _friendInfoDispatcher.QueueRequest(_UpdatePagesWorker, null);
        }

        private void _UpdateFriendsOnlineStatusAsync()
        {
            _friendInfoDispatcher.QueueRequest(_UpdateFriendsOnlineStatusWorker, null);
        }

        private void _UpdatePhotoAlbumsAsync()
        {
            _photoInfoDispatcher.QueueRequest(_UpdateInterestingPhotoAlbumsWorker, default(FacebookObjectId));
            _photoInfoDispatcher.QueueRequest(_UpdateFriendsPhotoAlbumsWorker, null);
        }

        internal void GetActivityPostsByUserAsync(FacebookObjectId userId, AsyncCompletedEventHandler callback)
        {
            Assert.IsTrue(FacebookObjectId.IsValid(userId));
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

        private void _UpdateNewsFeedWorker(object smallSyncBoolParameter)
        {
            Assert.IsFalse(Dispatcher.CheckAccess());

            var doSmallSync = (bool)smallSyncBoolParameter;

            int maxStreamCount = 100;

            if (!IsOnline)
            {
                return;
            }

            // Don't bother with the extra async updates if this is the first time synching,
            // or if the filter is being changed.
            bool isFirstSync = _mostRecentNewsfeedItem == DateTime.MinValue;

            // If this is the first sync, just pull in a smaller set of items first to improve the first run experience.
            // Do it inline to complete the task with the full set right afterwards.
            if (doSmallSync)
            {
                maxStreamCount = 10;
            }
            else
            {
                if (isFirstSync)
                {
                    _UpdateNewsFeedWorker(true);
                    // Reset the last update time because we want to get the bigger set.
                    _mostRecentNewsfeedItem = DateTime.MinValue;
                }
            }

            FacebookObjectId filterKey = default(FacebookObjectId);
            if (_newsFeedFilter != null)
            {
                filterKey = _newsFeedFilter.Key;
            }

            if (!IsOnline)
            {
                return;
            }

            List<ActivityPost> posts;
            try
            {
                // This has been timing out from Facebook.
                posts = _facebookApi.GetStream(filterKey, maxStreamCount, _mostRecentNewsfeedItem);
            }
            catch
            {
                // No update this time.
                return;
            }

            if (!IsOnline)
            {
                return;
            }

            List<FacebookContact> users = null;
            try
            {
                if (!_hasFetchedFriendsList)
                {
                    users = _facebookApi.GetFriendProfiles();
                }
            }
            catch
            {
                users = null;
            }

            if (!IsOnline)
            {
                return;
            }

            if (users != null)
            {
                _userLookupLock.EnterWriteLock();
                try
                {
                    foreach (FacebookContact friendProfile in users)
                    {
                        FacebookContact foundContact;
                        if (!_userLookup.TryGetValue(friendProfile.UserId, out foundContact))
                        {
                            double? interestLevel = _settings.GetInterestLevel(friendProfile.UserId);
                            if (interestLevel.HasValue)
                            {
                                friendProfile.InterestLevel = interestLevel.Value;
                            }

                            _userLookup.Add(friendProfile.UserId, friendProfile);
                        }
                        else
                        {
                            foundContact.MergeImage(friendProfile.Image);
                        }
                    }
                }
                finally
                {
                    _userLookupLock.ExitWriteLock();
                }
            }

            if (posts.Count > 0)
            {
                bool foundNewPhotos = false;
                foreach (var post in posts)
                {
                    if (post.Updated > _mostRecentNewsfeedItem)
                    {
                        _mostRecentNewsfeedItem = post.Updated;
                    }

                    if (post.Attachment != null && post.Attachment.Type == ActivityPostAttachmentType.Photos)
                    {
                        foundNewPhotos = true;
                    }
                }

                // Don't bother is we're in the small sync case.
                if (!doSmallSync && foundNewPhotos && !isFirstSync)
                {
                    _UpdatePhotoAlbumsAsync();
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
                    if (filterKey != default(FacebookObjectId))
                    {
                        return;
                    }
                }

                if (IsOnline)
                {
                    ActivityPost fakeStatusPost = RawNewsFeed.FindFKID(new FacebookObjectId("FakeStatusId"));
                    if (fakeStatusPost != null)
                    {
                        ActivityPost realStatusPost = posts.FirstOrDefault(post => string.Equals(post.Message, fakeStatusPost.Message, StringComparison.Ordinal));
                        if (realStatusPost != null)
                        {
                            RawNewsFeed.Remove(fakeStatusPost);
                        }
                    }

                    RawNewsFeed.Merge(posts, true, maxStreamCount);
                }
            }

            if (_facebookApi != null)
            {
                FacebookContact meContact = _facebookApi.TryGetUser(UserId, true);
                Assert.IsNotNull(meContact);
                if (meContact != null)
                {
                    MeContact.Merge(meContact);
                }
            }
        }

        private void _UpdateNotificationsWorker(object sender)
        {
            if (!IsOnline)
            {
                return;
            }

            List<Notification> notifications = null;
            try
            {
                notifications = _facebookApi.GetNotifications(false);
            }
            catch (FacebookException)
            {
                // Bad facebook.  No notification changes for us right now.
                return;
            }

            if (!IsOnline)
            {
                return;
            }

            List<Notification> requests;
            int unreadMailMessages;
            _facebookApi.GetRequests(out requests, out unreadMailMessages);

            _settings.RemoveKnownFriendRequestsExcept((from notification in requests where notification is FriendRequestNotification select notification.SenderId).ToList());
            notifications.AddRange(from req in requests where !_settings.IsFriendRequestKnown(req.SenderId) select req);

            List<FacebookObjectId> unfriendIds = _settings.GetUnfriendNotifications();
            notifications.AddRange(from id in unfriendIds select new UnfriendNotification(this, id) as Notification);

            RawNotifications.Merge(notifications, false);

            if (!IsOnline)
            {
                return;
            }

            List<MessageNotification> newMessages = _facebookApi.GetMailNotifications(false);
            RawInbox.Merge(newMessages, false);
        }

        private void _UpdatePagesWorker(object parameter)
        {
            if (!IsOnline)
            {
                return;
            }

            List<FacebookContact> pagesList = _facebookApi.GetPages();

            if (IsOnline)
            {
                _userLookupLock.EnterWriteLock();
                try
                {
                    foreach (FacebookContact page in pagesList)
                    {
                        FacebookContact lookedUpPage;
                        if (!_userLookup.TryGetValue(page.UserId, out lookedUpPage))
                        {
                            _userLookup.Add(page.UserId, page);
                        }
                        else
                        {
                            lookedUpPage.Merge(page);
                        }
                    }
                }
                finally
                {
                    _userLookupLock.ExitWriteLock();
                }
            }
        }

        private void _UpdateFriendsWorker(object parameter)
        {
            if (!IsOnline)
            {
                return;
            }

            List<FacebookContact> friendsList = _facebookApi.GetFriends();

            if (!IsOnline)
            {
                return;
            }

            _settings.UpdateInterestLevels(friendsList);
            _settings.UpdateCurrentFriends(friendsList);

            if (!IsOnline)
            {
                return;
            }

            RawFriends.Merge(friendsList, false);

            if (!IsOnline)
            {
                return;
            }

            _userLookupLock.EnterWriteLock();
            try
            {
                foreach (FacebookContact friend in friendsList)
                {
                    if (!_userLookup.ContainsKey(friend.UserId))
                    {
                        _userLookup.Add(friend.UserId, friend);
                    }
                }
            }
            finally
            {
                _userLookupLock.ExitWriteLock();
            }
            _hasFetchedFriendsList = true;
        }

        private void _UpdateFriendsOnlineStatusWorker(object parameter)
        {
            if (!IsOnline)
            {
                return;
            }

            Dictionary<FacebookObjectId, OnlinePresence> statusMap = _facebookApi.GetFriendsOnlineStatus();

            if (IsOnline)
            {
                _userLookupLock.EnterReadLock();
                try
                {
                    foreach (var friend in RawFriends)
                    {
                        OnlinePresence presence;
                        if (!statusMap.TryGetValue(friend.UserId, out presence))
                        {
                            // Why don't we have data for this friend?
                            // Refresh the list.
                            statusMap.Clear();
                            break;
                        }
                        friend.OnlinePresence = presence;
                    }
                }
                finally
                {
                    _userLookupLock.ExitReadLock();
                }
            }

            // If the friend list has obviously changed then just do a full refresh.
            if (RawFriends.Count != statusMap.Count)
            {
                _UpdateFriendsAsync();
            }
        }

        private void _UpdateFriendsPhotoAlbumsWorker(object unused)
        {
            if (!IsOnline)
            {
                return;
            }

            List<FacebookPhotoAlbum> albums = _facebookApi.GetFriendsPhotoAlbums();

            _GetPhotosForAlbumsWorker(albums);
        }

        private void _UpdateInterestingPhotoAlbumsWorker(object userIdParameter)
        {
            if (!IsOnline)
            {
                return;
            }

            var userId = (FacebookObjectId)userIdParameter;

            if (!FacebookObjectId.IsValid(userId))
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
                List<FacebookPhotoAlbum> albums = _facebookApi.GetUserAlbums(userId);
                _GetPhotosForAlbumsWorker(albums);
            }
        }

        private void _GetPhotosForAlbumsWorker(List<FacebookPhotoAlbum> albums)
        {
            // Only update albums if we don't already know about it, or if it's been updated more recently than our view.

            List<FacebookPhotoAlbum> batchAlbumList = new List<FacebookPhotoAlbum>();
            int batchCount = 5;
            for (int i = 0; i < albums.Count;)
            {
                batchAlbumList.Clear();

                for (; batchAlbumList.Count < batchCount && i < albums.Count; ++i)
                {
                    // Do we already know about this album?
                    FacebookPhotoAlbum sourceAlbum = RawPhotoAlbums.FindFKID(((IFBMergeable<FacebookPhotoAlbum>)albums[i]).FKID);
                    if (sourceAlbum == null || sourceAlbum.LastModified != albums[i].LastModified)
                    {
                        batchAlbumList.Add(albums[i]);
                    }
                }

                List<FacebookPhoto>[] photoCollections;
                try
                {
                    if (!IsOnline)
                    {
                        return;
                    }
                    photoCollections = _facebookApi.GetPhotosWithTags(from album in batchAlbumList select album.AlbumId);
                }
                catch
                {
                    if (!IsOnline)
                    {
                        return;
                    }
                    continue;
                }

                for (int currentAlbum = 0; currentAlbum < batchAlbumList.Count; ++currentAlbum)
                {
                    batchAlbumList[currentAlbum].RawPhotos = new FBMergeableCollection<FacebookPhoto>(photoCollections[currentAlbum]);
                }

                if (!IsOnline)
                {
                    return;
                }
                Dispatcher.Invoke((Action)(() => RawPhotoAlbums.Merge(batchAlbumList, true)));
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

            _settings.SetInterestLevel(contact.UserId, contact.InterestLevel);

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

        internal double GetInterestLevelForUserId(FacebookObjectId userId)
        {
            _userLookupLock.EnterReadLock();
            try
            {
                FacebookContact contact;
                if (_userLookup.TryGetValue(userId, out contact))
                {
                    return contact.InterestLevel;
                }
            }
            finally
            {
                _userLookupLock.ExitReadLock();
            }

            return _settings.GetInterestLevel(userId) ?? FacebookContact.DefaultInterestLevel;
        }
    }
}
