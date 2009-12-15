namespace FacebookClient
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using ClientManager;
    using ClientManager.View;
    using System.Diagnostics;

    public class FooterControl : Control
    {
        public static readonly DependencyProperty NotificationControlProperty = DependencyProperty.Register(
            "NotificationControl",
            typeof(NotificationCountControl),
            typeof(FooterControl));

        public NotificationCountControl NotificationControl
        {
            get { return (NotificationCountControl)GetValue(NotificationControlProperty); }
            set { SetValue(NotificationControlProperty, value); }
        }

        public static readonly DependencyProperty AreNotificationsToggledProperty = DependencyProperty.Register(
            "AreNotificationsToggled",
            typeof(bool),
            typeof(FooterControl),
            new PropertyMetadata(
                false,
                (d, e) => ((FooterControl)d)._OnAreNotificationsToggledChanged()));

        public bool AreNotificationsToggled
        {
            get { return (bool)GetValue(AreNotificationsToggledProperty); }
            set { SetValue(AreNotificationsToggledProperty, value); }
        }

        private void _OnAreNotificationsToggledChanged()
        {
            // Can't have both of these on at the same time.
            if (AreNotificationsToggled)
            {
                IsInboxToggled = false;
            }
        }

        /// <summary>
        /// InboxCountControl Dependency Property
        /// </summary>
        public static readonly DependencyProperty InboxCountControlProperty = DependencyProperty.Register(
            "InboxCountControl",
            typeof(NotificationCountControl),
            typeof(FooterControl));

        public NotificationCountControl InboxCountControl
        {
            get { return (NotificationCountControl)GetValue(InboxCountControlProperty); }
            set { SetValue(InboxCountControlProperty, value); }
        }

        /// <summary>
        /// IsInboxToggled Dependency Property
        /// </summary>
        public static readonly DependencyProperty IsInboxToggledProperty = DependencyProperty.Register(
            "IsInboxToggled",
            typeof(bool),
            typeof(FooterControl),
            new PropertyMetadata(
                false,
                (d, e) => ((FooterControl)d)._OnIsInboxToggledChanged()));

        public bool IsInboxToggled
        {
            get { return (bool)GetValue(IsInboxToggledProperty); }
            set { SetValue(IsInboxToggledProperty, value); }
        }

        private void _OnIsInboxToggledChanged()
        {
            // Can't have both of these on at the same time.
            if (IsInboxToggled)
            {
                AreNotificationsToggled = false;
            }
        }

        public static RoutedCommand ShowChatWindowCommand = new RoutedCommand("ShowChatWindow", typeof(FooterControl));
        public static RoutedCommand ShowSettingsCommand = new RoutedCommand("ShowSettings", typeof(FooterControl));
        public static RoutedCommand SignOutCommand = new RoutedCommand("SignOut", typeof(FooterControl));
        public static RoutedCommand RefreshCommand = new RoutedCommand("Refresh", typeof(FooterControl));

        public FooterControl()
        {
            CommandBindings.Add(new CommandBinding(ShowChatWindowCommand, OnShowChatWindowCommand));
            CommandBindings.Add(new CommandBinding(ShowSettingsCommand, OnShowSettingsCommand));
            CommandBindings.Add(new CommandBinding(SignOutCommand, OnSignOutCommand));
            CommandBindings.Add(new CommandBinding(RefreshCommand, OnRefreshCommand));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            NotificationControl = Template.FindName("NotificationControl", this) as NotificationCountControl;
            InboxCountControl = Template.FindName("InboxCountControl", this) as NotificationCountControl;
        }

        private void OnShowChatWindowCommand(object sender, ExecutedRoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).ApplicationCommands.ShowChatWindowCommand.Execute(Application.Current.MainWindow);
        }

        private void OnShowSettingsCommand(object sender, ExecutedRoutedEventArgs e)
        {
            ServiceProvider.ViewManager.ShowDialog(new SettingsDialog());
        }

        private void OnSignOutCommand(object sender, ExecutedRoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).SignOut();
        }

        private void OnRefreshCommand(object sender, ExecutedRoutedEventArgs e)
        {
            ServiceProvider.ViewManager.ActionCommands.StartSyncCommand.Execute(null);
        }

    }
}
