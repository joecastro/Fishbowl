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
    using Microsoft.Bing;

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

        public static TranslationService TranslationService { get; private set; }

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
            TranslationService = null;
        }

        public static void Initialize(string facebookAppId, string bingAppId, string[] parameters, Dispatcher dispatcher)
        {
            try
            {
                var facebook = new FacebookService(facebookAppId, dispatcher);
                var bing = new TranslationService(bingAppId);

                var view = new ViewManager(facebook, parameters);

                TranslationService = bing;
                FacebookService = facebook;
                ViewManager = view;
            }
            catch
            {
                Shutdown(null);
                throw;
            }
        }

        public static void GoOnline(string sessionKey, string sessionSecret, string userId)
        {
            Verify.IsNeitherNullNorEmpty(sessionKey, "sessionKey");
            Verify.IsNeitherNullNorEmpty(sessionSecret, "sessionSecret");
            Verify.IsNeitherNullNorEmpty(userId, "userId");

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
