
namespace FacebookClient
{
    using System.Windows;
    using System.Windows.Controls;
    using Contigo;

    public class ContactNameDisplayControl : Control
    {
        static ContactNameDisplayControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ContactNameDisplayControl), new FrameworkPropertyMetadata(typeof(ContactNameDisplayControl)));
        }

        public static readonly DependencyProperty FacebookContactProperty = DependencyProperty.Register(
            "FacebookContact",
            typeof(FacebookContact),
            typeof(ContactNameDisplayControl),
            new FrameworkPropertyMetadata(null));

        public FacebookContact FacebookContact
        {
            get { return (FacebookContact)GetValue(FacebookContactProperty); }
            set { SetValue(FacebookContactProperty, value); }
        }

        public static readonly DependencyProperty TargetFacebookContactProperty = DependencyProperty.Register(
            "TargetFacebookContact",
            typeof(FacebookContact),
            typeof(ContactNameDisplayControl),
            new FrameworkPropertyMetadata(null));

        public FacebookContact TargetFacebookContact
        {
            get { return (FacebookContact)GetValue(TargetFacebookContactProperty); }
            set { SetValue(TargetFacebookContactProperty, value); }
        }
    }
}
