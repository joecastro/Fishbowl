namespace Contigo
{
    using Standard;
    using System.Collections.Generic;

    public class FacebookPhotoCollection : FacebookCollection<FacebookPhoto>
    {
        internal static FacebookPhotoCollection CreateStaticCollection(IEnumerable<FacebookPhoto> photos)
        {
            return new FacebookPhotoCollection(photos);
        }

        private FacebookPhotoCollection(IEnumerable<FacebookPhoto> photos)
            : base(photos)
        {}

        internal FacebookPhotoCollection(FBMergeableCollection<FacebookPhoto> rawCollection, FacebookService service)
            : base(rawCollection, service)
        {}
    }
}
