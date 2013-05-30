namespace FacebookClient
{
    using System.Windows;
    using System.Windows.Controls;
    using Contigo;

    public class AlbumThumbnailButton : Button
    {
        public static readonly DependencyProperty FacebookPhotoAlbumProperty = DependencyProperty.Register(
            "FacebookPhotoAlbum",
            typeof(FacebookPhotoAlbum),
            typeof(AlbumThumbnailButton));

        public FacebookPhotoAlbum FacebookPhotoAlbum
        {
            get { return (FacebookPhotoAlbum)GetValue(FacebookPhotoAlbumProperty); }
            set { SetValue(FacebookPhotoAlbumProperty, value); }
        }

        public static readonly DependencyProperty ShowOwnerOverlayProperty = DependencyProperty.Register(
            "ShowOwnerOverlay",
            typeof(bool), 
            typeof(AlbumThumbnailButton),
            new FrameworkPropertyMetadata(true));

        public bool ShowOwnerOverlay
        {
            get { return (bool)GetValue(ShowOwnerOverlayProperty); }
            set { SetValue(ShowOwnerOverlayProperty, value); }
        }
    }
}
