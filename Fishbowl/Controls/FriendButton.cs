namespace FacebookClient
{
    using System.Windows;
    using System.Windows.Controls;
    using Contigo;

    public class FriendButton : Button
    {
        public static readonly DependencyProperty FriendProperty = DependencyProperty.Register(
            "Friend",
            typeof(FacebookContact),
            typeof(FriendButton),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public FacebookContact Friend
        {
            get { return (FacebookContact)GetValue(FriendProperty); }
            set { SetValue(FriendProperty, value); }
        }
    }
}
