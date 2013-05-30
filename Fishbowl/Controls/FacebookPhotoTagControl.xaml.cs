namespace FacebookClient
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using Contigo;

    public partial class FacebookPhotoTagControl : UserControl
    {
        public FacebookPhotoTagControl()
        {
            InitializeComponent();
        }

        public void OnNavigateToContentButtonClicked(object sender, RoutedEventArgs args)
        {
            ClientManager.ServiceProvider.ViewManager.NavigateToContent(sender);
        }

        private void PhotoTag_MouseEnter(object sender, MouseEventArgs e)
        {
            FacebookPhotoTag tag = this.DataContext as FacebookPhotoTag;

            if (tag != null)
            {
                FacebookClient.PhotoViewerControl.IsMouseOverTagCommand.Execute(tag.Offset, (IInputElement)this);
            }
        }

        private void PhotoTag_MouseLeave(object sender, MouseEventArgs e)
        {
            FacebookClient.PhotoViewerControl.IsMouseOverTagCommand.Execute(null, (IInputElement)this);
        }
    }
}
