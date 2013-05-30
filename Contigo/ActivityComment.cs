
namespace Contigo
{
    using System;
    using System.ComponentModel;
    using Standard;

    public class ActivityComment : INotifyPropertyChanged, IFacebookObject, IFBMergeable<ActivityComment>, IComparable<ActivityComment>
    {
        internal enum Type : byte
        {
            Unknown,
            ActivityPost,
            Photo
        }

        private FacebookContact _fromUser;
        private SmallString _text;
        private DateTime _timestamp;

        internal ActivityComment(FacebookService service)
        {
            Verify.IsNotNull(service, "service");
            SourceService = service;
        }

        internal global::Contigo.ActivityComment.Type CommentType { get; set; }

        internal FacebookObjectId FromUserId { get; set; }

        public DateTime Time
        {
            get { return _timestamp; }
            internal set
            {
                if (_timestamp != value)
                {
                    _timestamp = value;
                    _NotifyPropertyChanged("Time");
                }
            }
        }

        public string Text
        {
            get { return _text.GetString(); }
            internal set
            {
                var newValue = new SmallString(value);
                if (_text != newValue)
                {
                    _text = new SmallString(value);
                    _NotifyPropertyChanged("Text");
                }
            }
        }

        internal FacebookObjectId CommentId { get; set; }

        internal ActivityPost Post { get; set; }

        public FacebookContact FromUser
        {
            get
            {
                if (_fromUser == null && FacebookObjectId.IsValid(FromUserId))
                {
                    _fromUser = SourceService.GetUser(FromUserId);
                }

                return _fromUser;
            }
        }

        public bool CanRemove
        {
            get
            {
                return IsMine && FacebookObjectId.IsValid(CommentId) && CommentType == Type.ActivityPost;
            }
        }

        public bool IsMine
        {
            get
            {
                return FromUserId == SourceService.UserId;
            }
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

        #region IFacebookObject Members

        FacebookService IFacebookObject.SourceService { get; set; }

        private FacebookService SourceService
        {
            get { return ((IFacebookObject)this).SourceService; }
            set { ((IFacebookObject)this).SourceService = value; }
        }

        #endregion

        #region IFBMergeable<ActivityComment> Members

        FacebookObjectId IMergeable<FacebookObjectId, ActivityComment>.FKID { get { return CommentId; } }

        void IMergeable<FacebookObjectId, ActivityComment>.Merge(ActivityComment other)
        {}

        #endregion

        #region IEquatable<ActivityComment> Members

        public bool Equals(ActivityComment other)
        {
            if (other == null)
            {
                return false;
            }

            return this.CommentId == other.CommentId;
        }

        #endregion

        #region IComparable<ActivityComment> Members

        public int CompareTo(ActivityComment other)
        {
            // sort oldest first.  This is opposite many other Facebook types.
            if (other == null)
            {
                return -1;
            }

            return this.Time.CompareTo(other.Time);
        }

        #endregion
    }
}
