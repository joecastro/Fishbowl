namespace FacebookClient.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using Contigo;

    /// <summary>
    /// Interaction logic for FacebookPhotoTag.xaml
    /// </summary>
    public partial class FacebookPhotoTagControl : UserControl
    {
        public FacebookPhotoTagControl()
        {
            InitializeComponent();
        }

        #region Private Methods

        public void OnNavigateToContentButtonClicked(object sender, RoutedEventArgs args)
        {
            ClientManager.ServiceProvider.ViewManager.NavigateToContent(sender);
        }

        /// <summary>
        /// Event handler for when mouse enters control. Execute command on PhotoViewerControl with tag offset as parameter.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Event args.</param>
        private void PhotoTag_MouseEnter(object sender, MouseEventArgs e)
        {
            FacebookPhotoTag tag = this.DataContext as FacebookPhotoTag;

            if (tag != null)
            {
                FacebookClient.PhotoViewerControl.IsMouseOverTagCommand.Execute(tag.Offset, (IInputElement)this);
            }
        }

        /// <summary>
        /// Event handler for when mouse leaves control. Execute command on PhotoViewerControl with null parameter.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Event args.</param>
        private void PhotoTag_MouseLeave(object sender, MouseEventArgs e)
        {
            FacebookClient.PhotoViewerControl.IsMouseOverTagCommand.Execute(null, (IInputElement)this);
        }

        #endregion // Private Methods
    }
}
