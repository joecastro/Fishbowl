
namespace Contigo
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using Standard;

    // Extension methods to make XAttributes usable.
    internal static class XAttributeExtensions
    {
        public static XElement NewXAttribute(this XElement element, XName name, object value)
        {
            Verify.IsNotNull(element, "element");
            if (value != null)
            {
                element.Add(new XAttribute(name, value));
            }

            return element;
        }
    }

    internal class ServiceSettings
    {
        private class _LiteContact
        {
            public _LiteContact()
            { }

            public _LiteContact(FacebookContact c)
            {
                UserId = c.UserId;
                InterestLevel = c.NullableInterestLevel;
                Name = c.Name;
            }

            public FacebookObjectId UserId { get; set; }
            public string Name { get; set; }
            public double? InterestLevel { get; set; }

            public override string ToString()
            {
                return Name ?? UserId.ToString();
            }
        }

        private static readonly object s_lock = new object();
        private readonly object _lock = new object();

        private static readonly Dictionary<string, ServiceSettings> s_settingMap = new Dictionary<string,ServiceSettings>();

        // This object is physically split into two files.  One is at the root and contains session information
        // and the UserId associated with it.
        private const string _SessionSettingsFileName = "SessionSettings.2.xml";
        // The second file is specific to each user and contained in a subfolder based on the UserId.
        private const string _UserSettingsFileName = "UserSettings.2.xml";

        private readonly string _settingsRootPath;
        private readonly Dictionary<FacebookObjectId, _LiteContact> _friendLookup = new Dictionary<FacebookObjectId, _LiteContact>();
        private readonly HashSet<FacebookObjectId> _ignoredFriendRequests = new HashSet<FacebookObjectId>();
        private readonly HashSet<FacebookObjectId> _readMessages = new HashSet<FacebookObjectId>();
        private readonly HashSet<FacebookObjectId> _unknownFriendRemovals = new HashSet<FacebookObjectId>();
        private readonly _LiteContact _user = new _LiteContact();

        public string SessionKey { get; private set; }
        public string SessionSecret { get; private set; }
        public FacebookObjectId UserId { get { return _user.UserId; } }

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
            _ClearUserInfo();

            try
            {
                string userPath = Path.Combine(_settingsRootPath, UserId.ToString());
                Utility.EnsureDirectory(userPath);

                string userSettingPath = Path.Combine(userPath, _UserSettingsFileName);
                if (!File.Exists(userSettingPath))
                {
                    return;
                }

                XDocument xdoc = XDocument.Load(userSettingPath);

                // Currently not providing a migration path from v1 settings.  
                // Consider doing this in the future, or a path from 2 if we add a v3.
                // It's also worth noting that if an older version gets run, the way these are implemented will tend to stomp over each other.
                if (2 != (int)xdoc.Root.Attribute("v"))
                {
                    return;
                }

                XElement contactsElement = xdoc.Root.Element("friends");
                if (contactsElement != null)
                {
                    double il = 0;
                    foreach (var contact in
                        from contactNode in contactsElement.Elements("contact")
                        let isDouble = double.TryParse((string)contactNode.Attribute("interestLevel"), out il)
                        select new _LiteContact
                        {
                            UserId = new FacebookObjectId((string)contactNode.Attribute("uid")),
                            Name = (string)contactNode.Attribute("name"),
                            InterestLevel = (isDouble ? (double?)il : null)
                        })
                    {
                        _friendLookup.Add(contact.UserId, contact);
                    }
                }

                XElement knownFriendRequestsElement = xdoc.Root.Element("knownFriendRequests");
                if (knownFriendRequestsElement != null)
                {
                    _ignoredFriendRequests.AddRange(from contactNode in knownFriendRequestsElement.Elements("contact") select new FacebookObjectId((string)contactNode.Attribute("uid")));
                }

                XElement unknownFriendRemovalsElement = xdoc.Root.Element("unreadUnfriendings");
                if (unknownFriendRemovalsElement != null)
                {
                    _unknownFriendRemovals.AddRange(from contactNode in knownFriendRequestsElement.Elements("contact") select new FacebookObjectId((string)contactNode.Attribute("uid")));
                }

                XElement readMessagesElement = xdoc.Root.Element("readMessages");
                if (readMessagesElement != null)
                {
                    _readMessages.AddRange(from messageNode in readMessagesElement.Elements("message") select new FacebookObjectId((string)messageNode.Attribute("id")));
                }

                _hasUserInfo = true;
            }
            catch
            {
                _ClearUserInfo();
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

            try
            {
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
                    _user.UserId = new FacebookObjectId((string)sessionInfoElement.Element("userId"));
                }
            }
            // The XML file can be corrupted in various ways.
            // Just treat it as though we don't have settings saved.
            catch
            {
                _ClearUserInfo();
                return false;
            }

            return HasSessionInfo;
        }

        public bool IsFriendRequestKnown(FacebookObjectId uid)
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();

                return _ignoredFriendRequests.Contains(uid);
            }
        }

        public void UpdateInterestLevels(List<FacebookContact> friendsList)
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();
                foreach (FacebookContact friend in friendsList)
                {
                    double? interestLevel = GetInterestLevel(friend.UserId);
                    if (interestLevel != null)
                    {
                        friend.InterestLevel = interestLevel.Value;
                    }
                }
            }
        }

        public void UpdateCurrentFriends(List<FacebookContact> friendsList)
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();

                var friendsLite = new Dictionary<FacebookObjectId, _LiteContact>();
                friendsLite.AddRange(from c in friendsList select new KeyValuePair<FacebookObjectId, _LiteContact>(c.UserId, new _LiteContact(c)));

                List<FacebookObjectId> missingFriends = _friendLookup.Keys.Except(friendsLite.Keys).ToList();
                _friendLookup.Clear();
                _friendLookup.AddRange(friendsLite.AsEnumerable());

                _AddUnfriendNotifications(missingFriends);
            }
        }

        private void _AddUnfriendNotifications(List<FacebookObjectId> unfriends)
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();

                _unknownFriendRemovals.AddRange(unfriends);
            }
        }

        public void MarkMessageAsRead(FacebookObjectId messageId)
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();
                _readMessages.Add(messageId);
            }
        }

        public bool IsMessageRead(FacebookObjectId messageId)
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();
                return _readMessages.Contains(messageId);
            }
        }

        public void RemoveReadMessagesExcept(IEnumerable<FacebookObjectId> messageIds)
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();
                _readMessages.RemoveWhere(id => !messageIds.Contains(id));
            }
        }

        public void MarkFriendRequestAsRead(FacebookObjectId userId)
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();
                if (!_ignoredFriendRequests.Contains(userId))
                {
                    _ignoredFriendRequests.Add(userId);
                }
            }
        }

        public void MarkUnfriendNotificationAsRead(FacebookObjectId userId)
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();
                _unknownFriendRemovals.Remove(userId);
            }
        }

        // We don't want to keep a list of people who have requested friend status
        // but who have been either friended or really ignored from the website.
        // FacebookService should call this periodically to keep the list trimmed.
        public void RemoveKnownFriendRequestsExcept(List<FacebookObjectId> uids)
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();
                _ignoredFriendRequests.RemoveWhere(uid => !uids.Contains(uid));
            }
        }

        public List<FacebookObjectId> GetUnfriendNotifications()
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();
                return _unknownFriendRemovals.ToList();
            }
        }

        public void SetInterestLevel(FacebookObjectId userId, double value)
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();

                Assert.IsNotDefault(userId);

                if (userId == _user.UserId)
                {
                    _user.InterestLevel = value;
                    return;
                }

                _friendLookup[userId].InterestLevel = value;
            }
        }

        public double? GetInterestLevel(FacebookObjectId userId)
        {
            lock (_lock)
            {
                _VerifyHasUserInformation();

                Assert.IsNotDefault(userId);

                if (userId == UserId)
                {
                    return _user.InterestLevel;
                }

                _LiteContact c;
                if (_friendLookup.TryGetValue(userId, out c))
                {
                    return c.InterestLevel;
                }
                return null;
            }
        }

        public void ClearSessionInfo()
        {
            SetSessionInfo(null, null, default(FacebookObjectId));
        }

        private void _ClearUserInfo()
        {
            lock (_lock)
            {
                _ignoredFriendRequests.Clear();
                _unknownFriendRemovals.Clear();
                _friendLookup.Clear();

                _hasUserInfo = false;
            }
        }

        public void SetSessionInfo(string sessionKey, string sessionSecret, FacebookObjectId userId)
        {
            lock (_lock)
            {
                SessionKey = sessionKey;
                SessionSecret = sessionSecret;
                _user.UserId = userId;

                if (HasSessionInfo)
                {
                    _GetUserInfo();
                }
                else
                {
                    _ClearUserInfo();
                }
            }
        }

        public bool HasSessionInfo
        {
            get
            {
                return !string.IsNullOrEmpty(SessionKey) 
                    && !string.IsNullOrEmpty(SessionSecret) 
                    && FacebookObjectId.IsValid(UserId);
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

                if (FacebookObjectId.IsValid(UserId))
                {
                    XElement userXml = new XElement("userSettings",
                        new XAttribute("v", 2),
                        new XElement("friends",
                            from c in _friendLookup.Values
                            select (new XElement("contact"))
                                .NewXAttribute("interestLevel", c.InterestLevel)
                                .NewXAttribute("name", c.Name)
                                .NewXAttribute("uid", c.UserId)),
                        new XElement("knownFriendRequests",
                            from uid in _ignoredFriendRequests
                            select new XElement("contact",
                                new XAttribute("uid", uid))),
                        new XElement("unreadUnfriendings",
                            from uid in _unknownFriendRemovals
                            select new XElement("contact",
                                new XAttribute("uid", uid))),
                        new XElement("readMessages",
                            from messageId in _readMessages
                            select new XElement("message",
                                new XAttribute("id", messageId))));
                    userXml.Save(Path.Combine(Path.Combine(_settingsRootPath, UserId.ToString()), _UserSettingsFileName));
                }
            }
        }
    }
}
