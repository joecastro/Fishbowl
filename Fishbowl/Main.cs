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
                    ImageFileName = SplashScreenOverlay.CustomSplashPath,
                    ResourceAssembly = Assembly.GetEntryAssembly(),
                    ResourceName =  "resources/images/splash.png",
                    CloseOnMainWindowCreation = true,
                };
                
                splash.Show();

                BlackBox.DoctorWilson.InitializeReporting();
                BlackBox.DoctorWilson.Preamble = @"Something happened to Fishbowl that it wasn't expecting.
What follows is some information that would be helpful to the developer
to fix the issue.  It would be great if you could paste the content of this
file to http://fishbowl.codeplex.com/workitem/list/basic, along with a brief
description of what you were doing.

Thanks!
";

                var application = new FacebookClientApplication();

                application.InitializeComponent();
                application.Run();

                // Allow single instance code to perform cleanup operations
                SingleInstance.Cleanup();
            }
        }
    }
}