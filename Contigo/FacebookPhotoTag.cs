
namespace Contigo
{
    using System.ComponentModel;
    using System.Windows;
    using Standard;

    public class FacebookPhotoTag : IFacebookObject, INotifyPropertyChanged
    {
        private SmallString _text;
        private FacebookContact _contact;

        internal FacebookPhotoTag(FacebookService service)
        {
            Verify.IsNotNull(service, "service");
            SourceService = service;
        }

        public FacebookContact Contact
        { 
            get
            {
                if (_contact == null && FacebookObjectId.IsValid(ContactId))
                {
                    _contact = SourceService.GetUser(ContactId);
                }
                return _contact;
            }
        }

        public Point Offset { get; internal set; }

        internal FacebookObjectId PhotoId { get; set; }

        internal FacebookObjectId ContactId { get; set; }

        public string Text
        {
            get { return _text.GetString(); }
            internal set { _text = new SmallString(value); }
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
    }
}
