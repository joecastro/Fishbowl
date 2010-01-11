
namespace FacebookClient
{
    using Contigo;
    using Standard;

    public partial class SlideShowWindow
    {
        public SlideShowWindow(FacebookPhotoCollection photos, int startIndex)
        {
            Verify.IsNotNull(photos, "photos");

            InitializeComponent();

            SlideShowControl.FacebookPhotoCollection = photos;
            SlideShowControl.CurrentIndex = startIndex;
            SlideShowControl.Stopped += () => Close();
        }
    }
}
