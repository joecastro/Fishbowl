
namespace ClientManager.View
{
    using Contigo;
    using Standard;
    
    public class PhotoPlayer
    {
        public PhotoPlayer(FacebookPhotoAlbum album, FacebookPhoto startingPhoto)
        {
            Verify.IsNotNull(album, "album");

            StartingPhoto = startingPhoto;
            FacebookPhotoAlbum = album;
        }

        public FacebookPhotoAlbum FacebookPhotoAlbum { get; private set; }
        public FacebookPhoto StartingPhoto { get; private set; }

        public Navigator GetNavigator() { return new _PhotoPlayerNavigator(this); }

        private class _PhotoPlayerNavigator : Navigator
        {
            public _PhotoPlayerNavigator(PhotoPlayer photoPlayer)
                : base(photoPlayer, "[PhotoPlayer]", null)
            {}

            public override bool IncludeInJournal { get { return false; } }
        }
    }
}
