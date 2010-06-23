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
    }
}
