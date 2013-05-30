namespace FacebookClient
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Input;
    using ClientManager;
    using ClientManager.Controls;
    using Contigo;

    public class PhotoAlbumControl : SizeTemplateControl
    {
        public static readonly DependencyProperty PhotoAlbumProperty = DependencyProperty.Register(
            "PhotoAlbum", 
            typeof(FacebookPhotoAlbum), 
            typeof(PhotoAlbumControl),
            new FrameworkPropertyMetadata(null));

        public FacebookPhotoAlbum PhotoAlbum
        {
            get { return (FacebookPhotoAlbum)GetValue(PhotoAlbumProperty); }
            set { SetValue(PhotoAlbumProperty, value); }
        }

        public static RoutedCommand StartSlideShowCommand { get; private set; }

        static PhotoAlbumControl()
        {
            StartSlideShowCommand = new RoutedCommand("StartSlideShow", typeof(PhotoAlbumControl));
        }

        public PhotoAlbumControl()
        {
            CommandBindings.Add(new CommandBinding(StartSlideShowCommand, (sender, e) => ((PhotoAlbumControl)sender)._StartSlideShow(), (sender, e) => ((PhotoAlbumControl)sender)._CanExecuteStartSlideShow(e)));
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            Focus();
            Loaded += (sender, e2) => ServiceProvider.ViewManager.PropertyChanged += _OnViewManagerPropertyChanged;
            Unloaded += (sender, e2) => ServiceProvider.ViewManager.PropertyChanged -= _OnViewManagerPropertyChanged;
        }

        private bool _CanStartSlideShow { get { return PhotoAlbum != null && PhotoAlbum.Photos.Count > 0; } }

        private void _CanExecuteStartSlideShow(CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _CanStartSlideShow;
            e.Handled = true;
        }

        private void _StartSlideShow()
        {
            if (_CanStartSlideShow)
            {
                ((FacebookClientApplication)Application.Current).SwitchToSlideShow(PhotoAlbum.Photos, PhotoAlbum.Photos[0]);
            }
        }

        /// <summary>
        /// Focuses the PhotoAlbumControl when the current photo album changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Arguments describing the event.</param>
        private void _OnViewManagerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ActivePhotoAlbum")
            {
                this.Focus();
            }
        }
    }
}