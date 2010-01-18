namespace FacebookClient
{
    using System;
    using System.Reflection;
    using Standard;

    public static class FishBowl
    {
        [STAThread]
        public static void Main()
        {
            if (SingleInstance.InitializeAsFirstInstance("Fishbowl"))
            {
                var splash = new SplashScreen
                {
                    ResourceAssembly = Assembly.GetEntryAssembly(),
                    ResourceName =  "resources/images/splash.png",
                    CloseOnMainWindowCreation = true,
                };
                
                splash.Show();

                var application = new FacebookClientApplication();

                application.InitializeComponent();
                application.Run();

                // Allow single instance code to perform cleanup operations
                SingleInstance.Cleanup();
            }
        }
    }
}