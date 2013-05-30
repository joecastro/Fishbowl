namespace Contigo
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using Standard;

    public class ActivityPost : IFacebookObject, INotifyPropertyChanged, IFBMergeable<ActivityPost>, IComparable<ActivityPost>
    {
        private FacebookContact _actor;
        private FacebookContact _target;
        private bool _gettingMoreComments;
        private ActivityCommentCollection _comments;
        private FacebookContactCollection _likers;
        private SmallString _message;
        private SmallUri _likeUri;
        private FBMergeableCollection<FacebookContact> _mergeableLikers;
        private ActivityPostAttachment _attachment;
        private DateTime _created;
        private DateTime _updated;
        private bool _canLike;
        private bool _hasLiked;
        private int _likedCount;
        private bool _canComment;
        private bool _canRemoveComments;
        private int _commentCount;

        // When merging, make sure that we don't drop comments if all were requested.
        private bool _hasGottenMoreComments;

        // We don't actually populate the data in the constructor.  Instead letting the service do it.
        internal ActivityPost(FacebookService service)
        {
            Verify.IsNotNull(service, "service");
            SourceService = service;
        }

        public ActivityPostAttachment Attachment
        {
            get { return _attachment; }
            internal set
            {
                if (_attachment != value)
                {
                    _attachment = value;
                    _NotifyPropertyChanged("Attachment");
                }
            }
        }

        public DateTime Created
        {
            get { return _created; }
            internal set
            {
                if (_created != value)
                {
                    _created = value;
                    _NotifyPropertyChanged("Created");
                }
            }
        }

        public DateTime Updated
        {
            get { return _updated; }
            internal set
            {
                if (_updated != value)
                {
                    _updated = value;
                    _NotifyPropertyChanged("Updated");
                }
            }
        }
        
        public string Message
        {
            get { return _message.GetString(); }
            internal set
            {
                var newValue = new SmallString(value);
                if (_message != newValue)
                {
                    _message = newValue;
                    _NotifyPropertyChanged("Message");
                }
            }
        }

        internal FacebookObjectId ActorUserId { get; set; }
        
        internal FacebookObjectId TargetUserId { get; set; }

        public bool CanLike
        {
            get { return _canLike; }
            internal set
            {
                if (_canLike != value)
                {
                    _canLike = value;
                    _NotifyPropertyChanged("CanLike");
                }
            }
        }

        public bool HasLiked
        {
            get { return _hasLiked; }
            internal set
            {
                if (_hasLiked != value)
                {
                    _hasLiked = value;
                    _NotifyPropertyChanged("HasLiked");
                }
            }
        }

        public int LikedCount
        {
            get { return _likedCount; }
            internal set
            {
                if (value < 0)
                {
                    value = 0;
                }

                if (_likedCount != value)
                {
                    _likedCount = value;
                    _NotifyPropertyChanged("LikedCount");
                }
            }
        }

        internal void SetPeopleWhoLikeThisIds(IEnumerable<FacebookObjectId> likerIds)
        {
            // Should only be set during initialization
            Assert.IsNull(_mergeableLikers);

            _mergeableLikers = new FBMergeableCollection<FacebookContact>(from uid in likerIds select SourceService.GetUser(uid), false);
        }

        public FacebookContactCollection PeopleWhoLikeThis
        {
            get
            {
                if (_mergeableLikers == null)
                {
                    _mergeableLikers = new FBMergeableCollection<FacebookContact>(false);
                }

                if (_likers == null)
                {
                    _likers = new FacebookContactCollection(_mergeableLikers, SourceService, false);
                }
                return _likers;
            }
        }

        public bool CanComment
        {
            get { return _canComment; }
            internal set
            {
                if (_canComment != value)
                {
                    _canComment = value;
                    _NotifyPropertyChanged("CanComment");
                }
            }
        }

        public bool CanRemoveComments
        {
            get { return _canRemoveComments; }
            internal set
            {
                if (_canRemoveComments != value)
                {
                    _canRemoveComments = value;
                    _NotifyPropertyChanged("CanRemoveComments");
                }
            }
        }

        public int CommentCount
        {
            get { return _commentCount; }
            internal set
            {
                if (_commentCount != value)
                {
                    _commentCount = value;
                    _NotifyPropertyChanged("CommentCount");
                }
            }
        }

        public bool HasMoreComments { get { return CommentCount > Comments.Count; } }

        internal FBMergeableCollection<ActivityComment> RawComments { get; set; }

        public ActivityCommentCollection Comments
        {
            get
            {
                if (_comments == null)
                {
                    Assert.IsNotNull(RawComments);
                    _comments = new ActivityCommentCollection(RawComments, SourceService);
                }

                return _comments;
            }
        }

        internal FacebookObjectId PostId { get; set; }

        public Uri LikeUri
        {
            get { return _likeUri.GetUri(); }
            internal set
            {
                var newValue = new SmallUri(value);
                if (_likeUri != newValue)
                {
                    _likeUri = newValue;
                    _NotifyPropertyChanged("LikeUri");
                }
            }
        }

        public FacebookContact Actor
        {
            get
            {
                if (_actor == null && FacebookObjectId.IsValid(ActorUserId))
                {
                    _actor = SourceService.GetUser(ActorUserId);
                }

                return _actor;
            }
        }

        public FacebookContact Target
        {
            get
            {
                if (_target == null && FacebookObjectId.IsValid(TargetUserId))
                {
                    _target = SourceService.GetUser(TargetUserId);
                }

                return _target;
            }
        }

        internal void GetMoreComments()
        {
            _hasGottenMoreComments = true;
            if (HasMoreComments && !_gettingMoreComments)
            {
                _gettingMoreComments = true;
                SourceService.GetCommentsForPostAsync(this, _OnGetCommentsCompleted);
            }
        }

        private void _OnGetCommentsCompleted(object sender, AsyncCompletedEventArgs args)
        {
            var comments = (IEnumerable<ActivityComment>)args.UserState;
            RawComments.Merge(comments, false);
            CommentCount = RawComments.Count;
            _NotifyPropertyChanged("CommentCount");
            _NotifyPropertyChanged("HasMoreComments");
            _gettingMoreComments = false;
        }

        private void _NotifyPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #region IFacebookObject Members

        FacebookService IFacebookObject.SourceService { get; set; }

        private FacebookService SourceService
        {
            get { return ((IFacebookObject)this).SourceService; }
            set { ((IFacebookObject)this).SourceService = value; }
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        public override string ToString()
        {
            return this.Message + " @" + Created + ", Updated @" + Updated;
        }

        #region IFBMergeable<ActivityPost> Members

        FacebookObjectId IMergeable<FacebookObjectId, ActivityPost>.FKID
        {
            get { return PostId; }
        }

        void IMergeable<FacebookObjectId, ActivityPost>.Merge(ActivityPost other)
        {
            Verify.IsNotNull(other, "other");
            Verify.AreEqual(PostId, other.PostId, "other", "Can't merge two ActivityPosts with different Ids.");

            if (object.ReferenceEquals(this, other))
            {
                return;
            }

            Assert.AreEqual(ActorUserId, other.ActorUserId);
            //ActorUserId = other.ActorUserId;
            Assert.AreEqual(TargetUserId, other.TargetUserId);
            //TargetUserId = other.TargetUserId;

            Attachment = other.Attachment;
            CanComment = other.CanComment;
            CanLike = other.CanLike;
            CanRemoveComments = other.CanRemoveComments;
            CommentCount = other.CommentCount;
            Created = other.Created;
            HasLiked = other.HasLiked;
            LikedCount = other.LikedCount;
            LikeUri = other.LikeUri;
            Message = other.Message;
            RawComments.Merge(other.RawComments, false);
            if (_hasGottenMoreComments)
            {
                GetMoreComments();
            }
            else
            {
                _NotifyPropertyChanged("HasMoreComments");
            }

            if (other._mergeableLikers != null && other._mergeableLikers.Count != 0)
            {
                if (this._mergeableLikers == null)
                {
                    _mergeableLikers = new FBMergeableCollection<FacebookContact>(false);
                }
                _mergeableLikers.Merge(other._mergeableLikers, false);
            }
            else if (_mergeableLikers != null)
            {
                _mergeableLikers.Clear();
            }

            Updated = other.Updated;
        }

        #endregion

        #region IEquatable<ActivityPost> Members

        public bool Equals(ActivityPost other)
        {
            if (other == null)
            {
                return false;
            }

            return other.PostId == this.PostId;
        }

        #endregion

        #region IComparable<ActivityPost> Members

        public int CompareTo(ActivityPost other)
        {
            if (other == null)
            {
                return 1;
            }

            // Sort ActivityPosts newest first.
            return -Created.CompareTo(other.Created);
        }

        #endregion
    }
}
