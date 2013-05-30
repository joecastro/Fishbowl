
namespace Contigo
{
    using System;
    using System.ComponentModel;
    using Standard;

    public class Notification : IFacebookObject, INotifyPropertyChanged, IFBMergeable<Notification>
    {
        internal Notification(FacebookService service)
        {
            Verify.IsNotNull(service, "service");
            SourceService = service;
        }

        private SmallString _title;
        private SmallString _description;
        private SmallUri _href;
        // private int _appId;
        private FacebookImage _iconImage;

        private SmallString _titleText;
        private SmallString _descriptionText;
        private DateTime _created;
        private DateTime _updated;
        private bool _hidden;
        private bool _unread;
        private FacebookContact _sender;

        internal FacebookObjectId NotificationId { get; set; }

        internal FacebookObjectId SenderId { get; set; }

        public FacebookContact Sender
        {
            get
            {
                if (_sender == null)
                {
                    _sender = SourceService.GetUser(SenderId);
                }

                return _sender;
            }
        }

        internal FacebookObjectId RecipientId { get; set; }

        public string TitleText
        {
            get
            {
                if (_titleText == default(SmallString))
                {
                    return _title.GetString();
                }
                return _titleText.GetString();
            }
            set
            {
                SmallString newValue = new SmallString(value);
                if (_titleText != newValue)
                {
                    _titleText = newValue;
                    _NotifyPropertyChanged("TitleText");
                    if (_titleText == default(SmallString) || _title == default(SmallString))
                    {
                        // It didn't necessarily, but it's worth checking.
                        _NotifyPropertyChanged("Title");
                    }
                }
            }
        }

        public string DescriptionText 
        {
            get 
            {
                if (_descriptionText == default(SmallString))
                {
                    return _description.GetString();
                }
                return _descriptionText.GetString(); 
            }
            set
            {
                SmallString newValue = new SmallString(value);
                if (_descriptionText != newValue)
                {
                    _descriptionText = newValue;
                    _NotifyPropertyChanged("DescriptionText");
                    if (_descriptionText == default(SmallString) || _title == default(SmallString))
                    {
                        // It didn't necessarily, but it's worth checking.
                        _NotifyPropertyChanged("Description");
                    }
                }
            }
        }

        public DateTime Created
        {
            get { return _created; }
            internal set
            {
                if (value != _created)
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
                if (value != _updated)
                {
                    _updated = value;
                    _NotifyPropertyChanged("Updated");
                }
            }
        }

        public bool IsUnread
        {
            get { return _unread; }
            internal set
            {
                if (value != _unread)
                {
                    _unread = value;
                    _NotifyPropertyChanged("IsUnread");
                }
            }
        }

        public bool IsHidden
        {
            get { return _hidden; }
            internal set
            {
                if (value != _hidden)
                {
                    _hidden = value;
                    _NotifyPropertyChanged("IsHidden");
                }
            }
        }

        public string Title
        {
            get
            {
                if (_title == default(SmallString))
                {
                    return _titleText.GetString();
                }
                return _title.GetString();
            }
            internal set
            {
                var newValue = new SmallString(value);
                if (newValue != _title)
                {
                    _title = newValue;
                    _NotifyPropertyChanged("Title");
                    if (_titleText == default(SmallString) || _title == default(SmallString))
                    {
                        // It didn't necessarily, but it's worth checking.
                        _NotifyPropertyChanged("TitleText");
                    }

                }
            }
        }

        public string Description
        {
            get
            {
                if (_description == default(SmallString))
                {
                    return _descriptionText.GetString();
                }
                return _description.GetString();
            }
            internal set
            {
                var newValue = new SmallString(value);
                if (newValue != _description)
                {
                    _description = newValue;
                    _NotifyPropertyChanged("Description");
                    if (_descriptionText == default(SmallString) || _title == default(SmallString))
                    {
                        // It didn't necessarily, but it's worth checking.
                        _NotifyPropertyChanged("DescriptionText");
                    }
                }
            }
        }

        public Uri Link
        {
            get { return _href.GetUri(); }
            internal set
            {
                var newValue = new SmallUri(value);
                if (newValue != _href)
                {
                    _href = newValue;
                    _NotifyPropertyChanged("Link");
                }
            }
        }

        public FacebookImage Icon
        {
            get
            {
                if (_iconImage == null)
                {
                    _iconImage = new FacebookImage(SourceService, null);
                }
                return _iconImage;
            }
            set
            {
                _iconImage = value;
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

        #region Object Overrides

        public override bool Equals(object obj)
        {
            return Equals(obj as Notification);
        }

        public override int GetHashCode()
        {
            return NotificationId.GetHashCode();
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(TitleText))
            {
                return "(Empty Notification)"; 
            }

            if (string.IsNullOrEmpty(DescriptionText))
            {
                return TitleText;
            }
            return TitleText + " - " + DescriptionText;
        }

        #endregion

        #region IFBMergeable<Notification> Members

        FacebookObjectId IMergeable<FacebookObjectId, Notification>.FKID
        {
            get
            {
                Assert.IsTrue(FacebookObjectId.IsValid(NotificationId));
                return NotificationId; 
            }
        }

        void IMergeable<FacebookObjectId, Notification>.Merge(Notification other)
        {
            Verify.IsNotNull(other, "other");
            Verify.AreEqual(NotificationId, other.NotificationId, "other", "This can only be merged with a Notification with the same Id.");

            Created = other.Created;
            Description = other.Description;
            DescriptionText = other.DescriptionText;
            IsHidden = other.IsHidden;
            IsUnread = other.IsUnread;
            Link = other.Link;
            RecipientId = other.RecipientId;
            SenderId = other.SenderId;
            Title = other.Title;
            TitleText = other.TitleText;
            Updated = other.Updated;
            Icon.SafeMerge(other.Icon);
        }

        #endregion

        #region IEquatable<Notification> Members

        public bool Equals(Notification other)
        {
            if (other == null)
            {
                return false; 
            }
            return NotificationId == other.NotificationId;
        }

        #endregion

        #region INotifyPropertyChanged Members

        private void _NotifyPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }

    public class FriendRequestNotification : Notification
    {
        private const string _friendRequestFormat = "<div><a href=\"{0}\">{1}</a> wants to be your friend!</div>";
        private const string _friendRequestTextFormat = "{0} wants to be your friend!";

        internal FriendRequestNotification(FacebookService service, FacebookObjectId userId)
            : base(service)
        {
            Created = default(DateTime);
            Updated = default(DateTime);
            IsHidden = false;
            IsUnread = true;
            NotificationId = new FacebookObjectId("FriendRequest_" + userId.ToString());
            RecipientId = service.UserId;
            SenderId = userId;
            Sender.PropertyChanged += _OnSenderPropertyChanged;
            Title = string.Format(_friendRequestFormat, Sender.ProfileUri.ToString(), Sender.Name);
            TitleText = string.Format(_friendRequestTextFormat, Sender.Name);
            Link = Sender.ProfileUri;
            //this.Description = "";
        }

        private void _OnSenderPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Name")
            {
                if (!string.IsNullOrEmpty(Sender.Name))
                {
                    Title = string.Format(_friendRequestFormat, Sender.ProfileUri, Sender.Name);
                    TitleText = string.Format(_friendRequestTextFormat, Sender.Name);
                }
            }
            if (e.PropertyName == "ProfileUri")
            {
                Link = Sender.ProfileUri;
            }
        }

        public override string ToString()
        {
            return TitleText;
        }
    }

    // Defriend or unfriend?  Oxford American Dictionary thinks "Unfriend"...
    public class UnfriendNotification : Notification
    {
        private const string _friendRemovalFormat = "<div><a href=\"{0}\">{1}</a> is no longer your friend...</div>";
        private const string _friendRemovalTextFormat = "{0} is no longer your friend...";

        internal UnfriendNotification(FacebookService service, FacebookObjectId userId)
            : base(service)
        {
            Created = default(DateTime);
            Updated = default(DateTime);
            IsHidden = false;
            IsUnread = true;
            NotificationId = new FacebookObjectId("Unfriended_" + userId.ToString());
            RecipientId = service.UserId;
            SenderId = userId;
            Sender.PropertyChanged += _OnSenderPropertyChanged;
            Title = string.Format(_friendRemovalFormat, Sender.ProfileUri.ToString(), Sender.Name);
            TitleText = string.Format(_friendRemovalTextFormat, Sender.Name);
            Link = Sender.ProfileUri;
        }

        private void _OnSenderPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Name")
            {
                if (!string.IsNullOrEmpty(Sender.Name))
                {
                    Title = string.Format(_friendRemovalFormat, Sender.ProfileUri, Sender.Name);
                    TitleText = string.Format(_friendRemovalTextFormat, Sender.Name);
                }
            }
            if (e.PropertyName == "ProfileUri")
            {
                Link = Sender.ProfileUri;
            }
        }

        public override string ToString()
        {
            return TitleText;
        }
    }

    // public class GroupInviteRequestNotification : Notification {}
    // public class EventInviteRequestNotification : Notification {}

    public class MessageNotification : Notification, IFBMergeable<MessageNotification>
    {
        internal MessageNotification(FacebookService service)
            : base(service)
        {}

        #region IFBMergeable<MessageNotification> Members

        FacebookObjectId IMergeable<FacebookObjectId, MessageNotification>.FKID
        {
            get { return ((IFBMergeable<Notification>)this).FKID; }
        }

        void IMergeable<FacebookObjectId, MessageNotification>.Merge(MessageNotification other)
        {
        }

        #endregion

        #region IEquatable<MessageNotification> Members

        public bool Equals(MessageNotification other)
        {
            return ((IEquatable<Notification>)this).Equals(other);
        }

        #endregion
    }
}
