
namespace Contigo
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using Standard;

    public class FacebookPhoto : IFacebookObject, INotifyPropertyChanged, IFBMergeable<FacebookPhoto>, IComparable<FacebookPhoto>
    {
        private SmallString _caption;
        private SmallUri _link;
        private FacebookPhotoTagCollection _tags;
        private ActivityCommentCollection _comments;
        private ActivityComment _firstComment;
        private bool _canComment = false;
        private DateTime _lastCommentSync;
        internal readonly FBMergeableCollection<FacebookPhotoTag> RawTags;
        internal readonly FBMergeableCollection<ActivityComment> RawComments;
        
        // Light constructor for Attachment provided photos
        internal FacebookPhoto(FacebookService service, FacebookObjectId albumId, FacebookObjectId photoId, Uri source)
        {
            SourceService = service;
            AlbumId = albumId;
            PhotoId = photoId;
            Created = default(DateTime);
            Link = null;
            Image = new FacebookImage(service, source);
            RawComments = new FBMergeableCollection<ActivityComment>();
            RawTags = new FBMergeableCollection<FacebookPhotoTag>();
        }

        internal FacebookPhoto(FacebookService service)
        {
            Assert.IsNotNull(service);
            SourceService = service;
            RawComments = new FBMergeableCollection<ActivityComment>();
            RawTags = new FBMergeableCollection<FacebookPhotoTag>();
        }

        public FacebookObjectId PhotoId { get; internal set; }

        public Uri Link
        {
            get { return _link.GetUri(); }
            internal set
            {
                var newValue = new SmallUri(value);
                if (newValue != _link)
                {
                    _link = newValue;
                    _NotifyPropertyChanged("Link");
                }
            }
        }

        public FacebookObjectId AlbumId { get; internal set; }

        public string Caption
        {
            get { return _caption.GetString(); }
            internal set { _caption = new SmallString(value); }
        }

        /// <summary>
        /// Gets or sets when the photo was uploaded.
        /// </summary>
        public DateTime Created { get; internal set; }

        public FacebookImage Image { get; internal set; }

        internal FacebookObjectId OwnerId { get; set; }

        private FacebookPhotoAlbum _album;

        public FacebookPhotoAlbum Album
        {
            get
            {
                if (_album == null)
                {
                    _album = (from album in SourceService.RawPhotoAlbums
                              where album.AlbumId == AlbumId
                              select album).FirstOrDefault();
                }

                return _album;
            }
        }

        public FacebookPhotoTagCollection Tags
        {
            get
            {
                if (_tags == null)
                {
                    if (RawTags == null)
                    {
                        return null;
                    }

                    _tags = new FacebookPhotoTagCollection(RawTags, SourceService);
                }

                return _tags; 
            }
        }

        public ActivityCommentCollection Comments
        {
            get
            {
                if (_comments == null)
                {
                    _comments = new ActivityCommentCollection(RawComments, SourceService);
                }

                if (DateTime.Now - _lastCommentSync > TimeSpan.FromMinutes(5))
                {
                    _lastCommentSync = DateTime.Now;
                    _comments = new ActivityCommentCollection(RawComments, SourceService);
                    SourceService.GetCommentsForPhotoAsync(this, _OnGetCommentsCompleted);
                }
                return _comments;
            }
        }

        private void _OnGetCommentsCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error != null)
            {
                return;
            }

            var response = (GetCommentsForPhotoResponse)e.UserState;

            SourceService.Dispatcher.BeginInvoke(
            (Action)delegate
            {
                CanComment = response.CanComment;
                RawComments.Merge(response.Comments, false);
                if (RawComments.Count > 0)
                {
                    FirstComment = RawComments[0];
                }
                else
                {
                    FirstComment = null;
                }
            }, null);
        }

        internal void RequeryComments()
        {
            _lastCommentSync = DateTime.MinValue;
            // Requesting the property is enough to cause it to resync.
            var c = Comments;
        }

        public bool CanTag
        {
            get { return OwnerId == SourceService.UserId; }
        }

        public bool CanComment
        { 
            get { return _canComment; }
            private set
            {
                if (value != _canComment)
                {
                    _canComment = value;
                    _NotifyPropertyChanged("CanComment");
                }
            }
        }

        public ActivityComment FirstComment
        {
            get { return _firstComment; }
            private set
            {
                if (_firstComment != value)
                {
                    _firstComment = value;
                    _NotifyPropertyChanged("FirstComment");
                }
            }
        }

        #region System.Object overrides

        public override bool Equals(object obj)
        {
            return this.Equals(obj as FacebookPhoto);
        }

        public override int GetHashCode()
        {
            return PhotoId.GetHashCode();
        }

        #endregion

        #region INotifyPropertyChanged Members

        private void _NotifyPropertyChanged(string propertyName)
        {
            Assert.IsNeitherNullNorEmpty(propertyName);

            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region IEquatable<FacebookPhoto> Members

        public bool Equals(FacebookPhoto other)
        {
            if (other == null)
            {
                return false;
            }

            return other.PhotoId == this.PhotoId;
        }

        #endregion

        #region IFacebookObject Members

        FacebookService IFacebookObject.SourceService { get; set; }

        private FacebookService SourceService
        {
            get { return ((IFacebookObject)this).SourceService; }
            set { ((IFacebookObject)this).SourceService = value; }
        }

        #endregion

        #region IFBMergeable<FacebookPhoto> Members

        FacebookObjectId IMergeable<FacebookObjectId, FacebookPhoto>.FKID
        { 
            get 
            {
                Assert.IsTrue(FacebookObjectId.IsValid(PhotoId));
                return PhotoId;
            }
        }

        void IMergeable<FacebookObjectId, FacebookPhoto>.Merge(FacebookPhoto other)
        {
        }

        #endregion

        #region IComparable<FacebookPhoto> Members

        public int CompareTo(FacebookPhoto other)
        {
            if (other == null)
            {
                return 1;
            }

            // Photos should sort newest first.
            return -Created.CompareTo(other.Created);
        }

        #endregion
    }
}