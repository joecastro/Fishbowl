
namespace Contigo
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using Standard;

    internal class ServiceSettings
    {
        private static readonly object s_lock = new object();
        private readonly object _lock = new object();

        private static readonly Dictionary<string, ServiceSettings> s_settingMap = new Dictionary<string,ServiceSettings>();

        // This object is physically split into two files.  One is at the root and contains session information
        // and the UserId associated with it.
        private const string _SessionSettingsFileName = "SessionSettings.xml";
        // The second file is specific to each user and contained in a subfolder based on the UserId.
        private const string _UserSettingsFileName = "UserSettings.xml";

        private readonly string _settingsRootPath;
        private readonly Dictionary<string, double> _userLookupInterestLevels = new Dictionary<string, double>();
        private readonly HashSet<string> _ignoredFriendRequests = new HashSet<string>();

        public string SessionKey { get; private set; }
        public string SessionSecret { get; private set; }
        public string UserId { get; private set; }

        private bool _hasUserInfo = false;

        private void _VerifyHasUserInformation()
        {
            if (!_hasUserInfo)
            {
                throw new InvalidOperationException("There is no user information yet associated with this settings object.");
            }
        }

        public static ServiceSettings Load(string rootFolderPath)
        {
            Verify.IsNeitherNullNorEmpty(rootFolderPath, "rootFolderPath");

            rootFolderPath = Path.GetFullPath(rootFolderPath);
            Utility.EnsureDirectory(rootFolderPath);
            
            lock (s_lock)
            {
                ServiceSettings settings = null;
                if (!s_settingMap.TryGetValue(rootFolderPath, out settings))
                {
                    settings = new ServiceSettings(rootFolderPath);
                    s_settingMap.Add(rootFolderPath, settings);
                }

                return settings;
            }
        }

        private ServiceSettings(string rootFolderPath)
        {
            _settingsRootPath = rootFolderPath;
            if (_TryGetSessionInfo())
            {
                _GetUserInfo();
            }
        }

        private void _GetUserInfo()
        {
            // Even if this fails we want to purge partial data.
            ClearUserInfo();
            try
            {
                string userPath = Path.Combine(_settingsRootPath, UserId);
                Utility.EnsureDirectory(userPath);

                string userSettingPath = Path.Combine(userPath, _UserSettingsFileName);
                if (!File.Exists(userSettingPath))
                {
                    return;
                }

                XDocument xdoc = XDocument.Load(userSettingPath);

                if (1 != (int)xdoc.Root.Attribute("v"))
                {
                    return;
                }

                XElement contactsElement = xdoc.Root.Element("contacts");
                if (contactsElement != null)
                {
                    foreach (var interestInfo in
                        from contactNode in contactsElement.Elements("contact")
                        select new
                        {
                            UserId = (string)contactNode.Attribute("uid"),
                            InterestLevel = (double)contactNode.Attribute("interestLevel")
                        })
                    {
                        _userLookupInterestLevels.Add(interestInfo.UserId, interestInfo.InterestLevel);
                    }
                }

                XElement knownFriendRequestsElement = xdoc.Root.Element("knownFriendRequests");
                if (knownFriendRequestsElement != null)
                {
                    foreach (var requestInfo in
                        from contactNode in knownFriendRequestsElement.Elements("contact")
                        select (string)contactNode.Attribute("uid"))
                    {
                        _ignoredFriendRequests.Add(requestInfo);
                    }
                }

                _hasUserInfo = true;
            }
            catch
            {
                ClearUserInfo();
                throw;
            }
        }

        private bool _TryGetSessionInfo()
        {
            string sessionPath = Path.Combine(_settingsRootPath, _SessionSettingsFileName);
            if (!File.Exists(sessionPath))
            {
                return false;
            }

            XDocument xdoc = XDocument.Load(sessionPath);

            if (1 != (int)xdoc.Root.Attribute("v"))
            {
                return false;
            }

            XElement sessionInfoElement = xdoc.Root.Element("sessionInfo");
            if (sessionInfoElement != null)
            {
                SessionKey = (string)sessionInfoElement.Element("sessionKey");
                SessionSecret = (string)sessionInfoElement.Element("sessionSecret");
                UserId = (string)sessionInfoElement.Element("userId");
            }

            return HasSessionInfo;
        }

        public bool IsFriendRequestKnown(string uid)
        {
            lock (_lock)
            {
                return _ignoredFriendRequests.Contains(uid);
            }
        }

        public void MarkFriendRequestAsRead(string userId)
        {
            lock (_lock)
            {
                if (!_ignoredFriendRequests.Contains(userId))
                {
                    _ignoredFriendRequests.Add(userId);
                }
            }
        }

        // We don't want to keep a list of people who have requested friend status
        // but who have been either friended or really ignored from the website.
        // FacebookService should call this periodically to keep the list trimmed.
        public void RemoveKnownFriendRequestsExcept(List<string> uids)
        {
            lock (_lock)
            {
                _ignoredFriendRequests.RemoveWhere(uid => !uids.Contains(uid));
            }
        }

        public void SetInterestLevel(string userId, double value)
        {
            lock (_lock)
            {
                Assert.IsNeitherNullNorEmpty(userId);
                _userLookupInterestLevels[userId] = value;
            }
        }

        public double? GetInterestLevel(string userId)
        {
            lock (_lock)
            {
                double i;
                if (_userLookupInterestLevels.TryGetValue(userId, out i))
                {
                    return i;
                }
                return null;
            }
        }

        public void ClearSessionInfo()
        {
            // Don't clear the user id just because we're clearing session info.
            SetSessionInfo(null, null, UserId);
        }

        public void ClearUserInfo()
        {
            lock (_lock)
            {
                _ignoredFriendRequests.Clear();
                _userLookupInterestLevels.Clear();

                _hasUserInfo = false;
            }
        }

        public void SetSessionInfo(string sessionKey, string sessionSecret, string userId)
        {
            lock (_lock)
            {
                SessionKey = sessionKey;
                SessionSecret = sessionSecret;
                UserId = userId;

                if (HasSessionInfo)
                {
                    _GetUserInfo();
                }
            }
        }

        public bool HasSessionInfo
        {
            get
            {
                return !string.IsNullOrEmpty(SessionKey) 
                    && !string.IsNullOrEmpty(SessionSecret) 
                    && !string.IsNullOrEmpty(UserId);
            }
        }

        public void Save()
        {
            lock (_lock)
            {
                XElement sessionXml = new XElement("sessionSettings",
                    new XAttribute("v", 1),
                    new XElement("sessionInfo",
                        new XElement("sessionKey", SessionKey),
                        new XElement("sessionSecret", SessionSecret),
                        new XElement("userId", UserId)));
                sessionXml.Save(Path.Combine(_settingsRootPath, _SessionSettingsFileName));

                if (!string.IsNullOrEmpty(UserId))
                {
                    XElement userXml = new XElement("userSettings",
                        new XAttribute("v", 1),
                        new XElement("contacts",
                            from pair in _userLookupInterestLevels
                            where pair.Value != FacebookContact.DefaultInterestLevel
                            select new XElement("contact",
                                new XAttribute("interestLevel", pair.Value),
                                new XAttribute("uid", pair.Key))),
                        new XElement("knownFriendRequests",
                            from uid in _ignoredFriendRequests
                            select new XElement("contact",
                                new XAttribute("uid", uid))));
                    userXml.Save(Path.Combine(Path.Combine(_settingsRootPath, UserId), _UserSettingsFileName));
                }
            }
        }
    }
}
