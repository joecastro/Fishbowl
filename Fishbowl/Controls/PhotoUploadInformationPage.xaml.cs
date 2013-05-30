using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ClientManager;
using Standard;
using System.Collections.Generic;

namespace FacebookClient
{
    /// <summary>
    /// Interaction logic for PhotoUploadInformationPage.xaml
    /// </summary>
    public partial class PhotoUploadInformationPage : UserControl
    {
        public PhotoUploadInformationPage(PhotoUploadWizard wizard)
        {
            Verify.IsNotNull(wizard, "wizard");
            Wizard = wizard;

            InitializeComponent();
        }

        public PhotoUploadWizard Wizard { get; private set; }

        private void BrowseButtonClick(object sender, RoutedEventArgs e)
        {
            var ofd = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.gif;*.png)|*.jpg;*.jpeg;*.gif;*.png",
                Title = "Choose images to upload",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Multiselect = true,
            };
        
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Hide();

                List<string> imageFiles = PhotoUploadWizard.FindImageFiles(ofd.FileNames);
                if (imageFiles.Count != 0)
                {
                    Wizard.Show(imageFiles);
                }
            }
        }

        public void Show()
        {
            if (ServiceProvider.ViewManager.Dialog == null)
            {
                ServiceProvider.ViewManager.ShowDialog(this);
            }
        }

        public void Hide()
        {
            ServiceProvider.ViewManager.EndDialog(this);
        }

        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        public void SetDefaultAlbum(string albumName)
        {
            Wizard.SetDefaultAlbum(albumName);
        }
    }
}
