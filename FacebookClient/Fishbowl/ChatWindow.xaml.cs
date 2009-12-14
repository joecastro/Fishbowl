
namespace FacebookClient
{
    using System;
    using System.Windows;
    using Standard;

    /// <summary>
    /// Interaction logic for ChatWindow.xaml
    /// </summary>
    public partial class ChatWindow : Window
    {
        private WebBrowserEvents _eventHook;

        public ChatWindow()
        {
            InitializeComponent();

            this.Closed += (sender, e) => Utility.SafeDispose(ref _eventHook);
        }

        private void _OnFirstNavigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            Browser.Navigated -= _OnFirstNavigated;

            Assert.IsNotNull(Browser.Document);

            Utility.SuppressJavaScriptErrors(Browser);
            _eventHook = new WebBrowserEvents(Browser);
            _eventHook.WindowClosing += (sender2, e2) => Close();
        }
    }
}
