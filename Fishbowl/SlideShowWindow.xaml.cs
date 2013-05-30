
namespace FacebookClient
{
    using System;
    using System.ComponentModel;
    using Contigo;
    using Standard;

    public partial class SlideShowWindow
    {
        private void _OnSlideShowControlIsStoppedChanged(object sender, EventArgs e)
        {
            if (SlideShowControl == null || SlideShowControl.IsStopped)
            {
                this.Close();
            }
        }
        
        public SlideShowWindow(FacebookPhotoCollection photos, FacebookPhoto startPhoto)
        {
            Verify.IsNotNull(photos, "photos");

            InitializeComponent();

            SlideShowControl.FacebookPhotoCollection = photos;
            SlideShowControl.StartingPhoto = startPhoto;
            
            DependencyPropertyDescriptor desc = DependencyPropertyDescriptor.FromProperty(PhotoSlideShowControl.IsStoppedProperty, typeof(PhotoSlideShowControl));
            desc.AddValueChanged(SlideShowControl, _OnSlideShowControlIsStoppedChanged);
        }
    }
}
