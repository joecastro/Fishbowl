using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Standard;
using System.IO;
using System.Xml.Linq;

namespace Contigo
{
    internal class ServiceSettings
    {
        private const string _SettingsFileName = "ServiceSettings.xml";
        private readonly string _settingsPath;
        private readonly Dictionary<string, double> _userLookupInterestLevels = new Dictionary<string, double>();
        private readonly object _lock = new object();

        private string _sessionKey;
        private string _userId;

        public ServiceSettings(string folderPath)
        {
            Utility.EnsureDirectory(folderPath);

            _settingsPath = Path.Combine(folderPath, _SettingsFileName);
            try
            {
                if (File.Exists(_settingsPath))
                {
                    XDocument xdoc = XDocument.Load(_settingsPath);

                    if (1 != (int)xdoc.Root.Attribute("v"))
                    {
                        return; 
                    }

                    foreach (var interestInfo in
                        from contactNode in xdoc.Root.Element("contacts").Elements("contact")
                        select new
                        {
                            UserId = (string)contactNode.Attribute("uid"),
                            InterestLevel = (double)contactNode.Attribute("interestLevel")
                        })
                    {
                        _userLookupInterestLevels.Add(interestInfo.UserId, interestInfo.InterestLevel);
                    }
                }
            }
            catch (Exception)
            {
                _userLookupInterestLevels.Clear();
                _sessionKey = null;
                _userId = null;
            }
        }

        public void AddInterestLevel(string userId, double value)
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

        public bool GetSessionInfo(out string sessionKey, out string userId)
        {
            lock (_lock)
            {
                sessionKey = _sessionKey;
                userId = _userId;

                return _sessionKey != null;
            }
        }

        public void ClearSessionInfo()
        {
            SaveSessionInfo(null, null);
        }

        public void SaveSessionInfo(string sessionKey, string userId)
        {
            lock (_lock)
            {
                _sessionKey = sessionKey;
                _userId = userId;
            }
        }

        public void Save()
        {
            lock (_lock)
            {
                XElement xml = new XElement("settings",
                    new XAttribute("v", 1),
                    new XElement("sessionInfo",
                        new XElement("sessionKey", _sessionKey),
                        new XElement("userId", _userId)),
                    new XElement("contacts",
                        from pair in _userLookupInterestLevels
                        where pair.Value != FacebookContact.DefaultInterestLevel
                        select new XElement("contact",
                            new XAttribute("interestLevel", pair.Value),
                            new XAttribute("uid", pair.Key))));
                xml.Save(_settingsPath);
            }
        }
    }
}
