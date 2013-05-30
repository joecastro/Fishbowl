//-----------------------------------------------------------------------
// <copyright file="ServiceProvider.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
//     The ServiceProvider class hosts and provides access to all 
//     ScePhoto services.
// </summary>
//-----------------------------------------------------------------------

namespace ClientManager
{
    using System;
    using System.Windows.Threading;
    using ClientManager.View;
    using Contigo;
    using Standard;

    /// <summary>
    /// Hosts and provides access to all ClientManager services.
    /// </summary>
    public static class ServiceProvider
    {
        internal static FacebookService FacebookService { get; private set; }

        /// <summary>
        /// Gets the ViewManager that provides services to the UI layer.
        /// </summary>
        public static ViewManager ViewManager { get; private set; }

        /// <summary>
        /// Shuts down the service provider.
        /// </summary>
        public static void Shutdown(Action<string> deleteCallback)
        {
            if (FacebookService != null)
            {
                FacebookService.Shutdown(deleteCallback);
                FacebookService = null;
            }
            ViewManager = null;
        }

        public static void Initialize(string facebookAppId, string facebookAppKey, string bingAppId, string[] parameters, Dispatcher dispatcher)
        {
            try
            {
                var facebook = new FacebookService(facebookAppId, facebookAppKey, dispatcher);
                var view = new ViewManager(facebook, parameters);
                FacebookService = facebook;
                ViewManager = view;
            }
            catch
            {
                Shutdown(null);
                throw;
            }
        }

        public static void GoOnline(string sessionKey, string sessionSecret, FacebookObjectId userId)
        {
            Verify.IsNeitherNullNorEmpty(sessionKey, "sessionKey");
            Verify.IsNeitherNullNorEmpty(sessionSecret, "sessionSecret");
            Verify.IsTrue(FacebookObjectId.IsValid(userId), "invalid userId");

            if (FacebookService.IsOnline)
            {
                throw new InvalidOperationException();
            }

            FacebookService.RecoverSession(sessionKey, sessionSecret, userId);

            var handler = GoneOnline;
            if (handler != null)
            {
                handler(FacebookService, new EventArgs());
            }
        }

        public static event EventHandler GoneOnline;
    }
}
