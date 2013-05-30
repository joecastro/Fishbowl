
namespace Contigo
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Xml.Linq;
    using Microsoft.Json.Serialization;
    using Standard;

    using JSON_ARRAY = System.Collections.Generic.IList<object>;
    using JSON_OBJECT = System.Collections.Generic.IDictionary<string, object>;

    using METHOD_MAP = System.Collections.Generic.SortedDictionary<string, string>;

    // This class is thread-safe.  It does not contain any mutable state.
    internal class FacebookWebApi
    {
        #region Fields

        private static readonly Uri _FacebookApiUri = new Uri(@"http://api.facebook.com/restserver.php");

        private static readonly string _ExtendedPermissionsColumns;
        // affiliations: type: [college | high school | work | region], year, name, nid, status
        // current_location: city, state, country (well defined), zip (may be zero)
        // education_history: year (4 digit, may be blank), name, concentration (list), degree
        // hometown_location: city, state, country
        // hs_info: hs1_name, hs2_name, grad_year, hs1_id, hs2_id
        // pic* also has pic_with_logo* analagous column.
        // work_history: location, company_name, description, position, start_date, end_date
        // family: relationship (one of parent, mother, father, sibling, sister, brother, child, son, daughter), uid (optional), name (optional), birthday (if the relative is a child, this is the birthday the user entered)
        private const string _UserColumns = "about_me, activities, affiliations, allowed_restrictions, birthday, birthday_date, books, current_location, education_history, email_hashes, family, first_name, has_added_app, hometown_location, hs_info, interests, is_app_user, is_blocked, last_name, locale, meeting_for, meeting_sex, movies, music, name, notes_count, online_presence, pic, pic_big, pic_small, pic_square, political, profile_blurb, profile_update_time, profile_url, proxied_email, quotes, relationship_status, religion, sex, significant_other_id, status, timezone, tv, uid, username, verified, wall_count, website, work_history";
        private const string _UserColumnsLite = "first_name, is_blocked, last_name, name, online_presence, pic, pic_big, pic_small, pic_square, profile_url, sex, status, uid, username";
        private const string _AlbumColumns = "aid, cover_pid, owner, name, created, modified, description, location, link, size, visible";
        private const string _PageColumns = "page_id, name, pic_small, pic_big, pic_square, pic, pic_large, page_url, type, website, has_added_app, founded, company_overview, mission, products, location, parking, public_transit, hours";
        private const string _PhotoColumns = "pid, aid, owner, src, src_big, src_small, link, caption, created";
        // Facebook's documentation also mentions a "type" column that has been deprecated in favor of using the attachment to figure it out.
        private const string _StreamTableColumns = "post_id, viewer_id, app_id, source_id, updated_time, created_time, filter_key, attribution, actor_id, target_id, message, app_data, action_links, attachment, comments, likes, privacy";
        private const string _StreamCommentColumns = "post_id, id, fromid, time, text";
        private const string _StreamFilterColumns = "uid, filter_key, name, rank, icon_url, is_visible, type, value";
        private const string _PhotoTagColumns = "pid, subject, text, xcoord, ycoord, created";
        private const string _ProfileColumns = "id, name, url, pic, pic_square, pic_small, pic_big, type, username";
        private const string _ThreadColumns = "thread_id, folder_id, subject, recipients, updated_time, parent_message_id, parent_thread_id, message_count, snippet, snippet_author, object_id, viewer_id, unread";

        private const string _SelectFriendsClause = "(SELECT uid2 FROM friend WHERE uid1={0})";

        private const string _GetPermissionsQueryString = "SELECT {0} FROM permissions WHERE uid={1}";

        private const string _GetFriendsQueryString = "SELECT " + _UserColumns + " FROM user WHERE uid IN " + _SelectFriendsClause;
        private const string _GetFriendsLimitOffsetFormatQueryString = _GetFriendsQueryString + " LIMIT {1} OFFSET {2}";
        private const string _GetFriendsOnlineStatusQueryString = "SELECT uid, online_presence FROM user WHERE uid IN " + _SelectFriendsClause;
        private const string _GetFriendsProfilesQueryString = "SELECT " + _ProfileColumns + " FROM profile WHERE id IN " + _SelectFriendsClause;
        private const string _GetSingleUserQueryString = "SELECT " + _UserColumns + " FROM user WHERE uid={0}";
        private const string _GetSingleProfileInfoQueryString = "SELECT " + _ProfileColumns + " FROM profile WHERE id={0}";
        private const string _GetSingleUserAlbumsQueryString = "SELECT " + _AlbumColumns + " FROM album WHERE owner={0} ORDER BY modified DESC";
        private const string _GetFriendsAlbumsQueryString = "SELECT " + _AlbumColumns + " FROM album WHERE owner IN " + _SelectFriendsClause + " ORDER BY modified DESC LIMIT 200";
        private const string _GetPhotosFromSingleUserAlbumsQueryString = "SELECT " + _PhotoColumns + " FROM photo WHERE aid IN (SELECT aid FROM album WHERE owner={0}) ORDER BY modified DESC";
        private const string _GetPhotoTagsFromAlbumQueryString = "SELECT " + _PhotoTagColumns + " FROM photo_tag WHERE pid IN (SELECT pid FROM photo WHERE aid=\"{0}\")";
        // Can't use this because it returns a limit of 5000 items which results in a bunch of albums with no photos.
        //private const string _GetPhotosFromFriendsAlbumsQueryString = "SELECT " + _PhotoFields + " FROM photo WHERE aid IN (SELECT aid FROM album WHERE owner IN " + _SelectFriendsClause + " ORDER BY modified DESC)";
        private const string _GetPhotosFromAlbumQueryString = "SELECT " + _PhotoColumns + " FROM photo WHERE aid=\"{0}\"";
        private const string _GetFriendsPhotosQueryString = "SELECT " + _PhotoColumns + " FROM photo WHERE aid IN (SELECT aid FROM album WHERE owner IN " + _SelectFriendsClause + " ORDER BY modified DESC)";
        private const string _GetCommentorsQueryString = "SELECT " + _UserColumns + " FROM user WHERE uid IN (SELECT fromid FROM comment where post_id IN (SELECT post_id FROM stream WHERE filter_key in (SELECT filter_key FROM stream_filter WHERE uid={0} and type='newsfeed')))";
        private const string _GetStreamPostsQueryString = "SELECT " + _StreamTableColumns + " FROM stream WHERE source_id={0} LIMIT 20";
        private const string _GetFriendsRecentActivityString = "SELECT " + _StreamTableColumns + " FROM stream WHERE source_id IN " + _SelectFriendsClause;
        private const string _GetStreamCommentsQueryString = "SELECT " + _StreamCommentColumns + " FROM comment WHERE post_id IN (SELECT post_id FROM stream WHERE filter_key in (SELECT filter_key FROM stream_filter WHERE uid={0} and type='newsfeed'))";
        private const string _GetStreamFiltersQueryString = "SELECT " + _StreamFilterColumns + " FROM stream_filter where uid={0}";

        private const string _GetInboxThreadsQueryString = "SELECT " + _ThreadColumns + " FROM thread where folder_id=0";
        private const string _GetUnreadInboxThreadsQueryString = _GetInboxThreadsQueryString + " and unread != 0";

        private const string _GetPhotoTagsMultiQueryString = "SELECT " + _PhotoTagColumns + " FROM photo_tag WHERE pid IN (SELECT pid FROM #{0})";

        private readonly JsonDataSerialization _jsonSerializer;

        private static readonly Dictionary<string, Permissions> _ReversePermissionLookup;

        /// <summary>
        /// A mapping of the Permissions enum to the extended permissions strings that we need to request from the server.
        /// </summary>
        // This map and the enum needs to be kept in sync with the query string for extended permissions.
        // If any of the Facebook fields become very volatile then we can dynamically generate strings
        // and maps in the static constructor.
        private static readonly Dictionary<Permissions, string> _PermissionLookup = new Dictionary<Permissions, string>
        {
            { Permissions.CreateEvent, "create_event" },
            { Permissions.Email, "email" },
            { Permissions.FriendsAboutMe, "friends_about_me" },
            { Permissions.FriendsActivities, "friends_activities" },
            { Permissions.FriendsBirthday, "friends_birthday" },
            { Permissions.FriendsEducationHistory, "friends_education_history" },
            { Permissions.FriendsEvents, "friends_events" },
            { Permissions.FriendsGroups, "friends_groups" },
            { Permissions.FriendsHometown, "friends_hometown" },
            { Permissions.FriendsInterests, "friends_interests" },
            { Permissions.FriendsLikes, "friends_likes" },
            { Permissions.FriendsLocation, "friends_location" },
            { Permissions.FriendsNotes, "friends_notes" },
            { Permissions.FriendsOnlinePresence, "friends_online_presence" },
            { Permissions.FriendsPhotos, "friends_photos" },
            { Permissions.FriendsRelationships, "friends_relationships" },
            { Permissions.FriendsReligionPolitics, "friends_religion_politics" },
            { Permissions.FriendsStatus, "friends_status" },
            { Permissions.FriendsVideos, "friends_videos" },
            { Permissions.FriendsWebsite, "friends_website" },
            { Permissions.FriendsWorkHistory, "friends_work_history" },
            { Permissions.OfflineAccess, "offline_access" },
            { Permissions.PhotoUpload, "photo_upload" },
            { Permissions.PublishStream, "publish_stream" },
            { Permissions.ReadFriendLists, "read_friendlists" },
            { Permissions.ReadInsights, "read_insights" },
            { Permissions.ReadMailbox, "read_mailbox" },
            { Permissions.ReadRequests, "read_requests" },
            { Permissions.ReadStream, "read_stream" },
            { Permissions.RsvpEvent, "rsvp_event" },
            { Permissions.UserAboutMe, "user_about_me" },
            { Permissions.UserActivities, "user_activities" },
            { Permissions.UserBirthday, "user_birthday" },
            { Permissions.UserEducationHistory, "user_education_history" },
            { Permissions.UserEvents, "user_events" },
            { Permissions.UserGroups, "user_groups" },
            { Permissions.UserHometown, "user_hometown" },
            { Permissions.UserInterests, "user_interests" },
            { Permissions.UserLikes, "user_likes" },
            { Permissions.UserLocation, "user_location" },
            { Permissions.UserNotes, "user_notes" },
            { Permissions.UserOnlinePresence, "user_online_presence" },
            { Permissions.UserPhotos, "user_photos" },
            { Permissions.UserRelationships, "user_relationships" },
            { Permissions.UserReligionPolitics, "user_religion_politics" },
            { Permissions.UserStatus, "user_status" },
            { Permissions.UserVideos, "user_videos" },
            { Permissions.UserWebsite, "user_website" },
            { Permissions.UserWorkHistory, "user_work_history" },
        };

        private readonly string _ApplicationKey;
        private readonly string _SessionKey;
        private readonly string _Secret;
        private readonly FacebookObjectId _UserId;
        private readonly FacebookService _Service;

        #endregion

        private void _Verify()
        {
            if (_Service == null)
            {
                throw new InvalidOperationException("Operation requires a valid FacebookService");
            }
        }

        #region Methods that don't require a session key

        public static Uri GetLoginUri(string appId, string successUri, string deniedUri, IEnumerable<Permissions> requiredPermissions)
        {
            string permissionsPart = "";

            bool isFirst = true;
            StringBuilder permBuilder = new StringBuilder();
            foreach (var permission in requiredPermissions)
            {
                if (!isFirst)
                {
                    // read_stream,publish_stream,offline_access
                    permBuilder.Append(",");
                }
                else
                {
                    isFirst = false;
                }

                permBuilder.Append(_PermissionLookup[permission]);
            }

            permissionsPart = permBuilder.ToString();

            return new Uri(string.Format("http://www.facebook.com/login.php?api_key={0}&connect_display=popup&v=1.0&next={1}&cancel_url={2}&fbconnect=true&return_session=true{3}{4}",
                appId, 
                successUri, 
                deniedUri, 
                string.IsNullOrEmpty(permissionsPart) ? "" : "&req_perms=",
                permissionsPart));
        }

        /* Not currently used.
        public static string GetAuthenticationToken(string appId, string secret)
        {
            var createTokenMap = new METHOD_MAP
            {
                { "method", "facebook.auth.createToken" },
                { "api_key", appId },
                { "v", "1.0" },
            };

            string authResponse = SendRequest(createTokenMap, secret);
            return XDocument.Parse(authResponse).Root.Value;
        }

        public static void GetSession(string appId, string authToken, string secret, out string sessionKey, out string userId)
        {
            var getSession = new SortedDictionary<string, string>
            {
                { "method", "facebook.auth.getSession" },
                { "auth_token", authToken },
                { "api_key", appId },
                { "v", "1.0" },
            };

            string xml = SendRequest(getSession, secret);

            DataSerialization.DeserializeSessionInfo(xml, out sessionKey, out userId);
        }
        */

        #endregion

        static FacebookWebApi()
        {
            // Verify that duplicated static data is constructed properly.
            _VerifyExtendedPermissionIntegrity();

            bool isFirst = true;
            var columnBuilder = new StringBuilder();
            _ReversePermissionLookup = new Dictionary<string, Permissions>();
            foreach (var pair in _PermissionLookup)
            {
                _ReversePermissionLookup.Add(pair.Value, pair.Key);

                if (!isFirst)
                {
                    columnBuilder.Append(", ");
                }
                else
                {
                    isFirst = false;
                }

                columnBuilder.Append(pair.Value);
            }

            _ExtendedPermissionsColumns = columnBuilder.ToString();

            /*
            ServicePointManager.MaxServicePoints = 16;
            ServicePointManager.MaxServicePointIdleTime = new TimeSpan(0, 10, 0).Milliseconds;
            //ServicePointManager.UseNagleAlgorithm = true;
            ServicePointManager.Expect100Continue = true;
            ServicePoint servicePoint = ServicePointManager.FindServicePoint(_FacebookApiUri);
            */
        }

        [Conditional("DEBUG")]
        private static void _VerifyExtendedPermissionIntegrity()
        {
            int knownPermissionCount = Enum.GetValues(typeof(Permissions)).Length;

            Assert.AreEqual(knownPermissionCount, _PermissionLookup.Count);
        }

        public FacebookWebApi(string applicationId, string sessionKey, FacebookObjectId userId, string secret)
        {
            _ApplicationKey = applicationId;
            _SessionKey = sessionKey;
            _UserId = userId;
            _Secret = secret;
            _Service = null;
            _jsonSerializer = new JsonDataSerialization(null);
        }

        public FacebookWebApi(FacebookService service, string secret)
        {
            _ApplicationKey = service.ApplicationKey;
            _SessionKey = service.SessionKey;
            _UserId = service.UserId;
            _Secret = secret;
            _Service = service;
            _jsonSerializer = new JsonDataSerialization(service);
        }

        /// <summary>
        /// Posts an FQL query to facebook.
        /// </summary>
        /// <param name="query">The FQL query to send.</param>
        /// <returns>The results of the FQL query.</returns>
        private string _SendQuery(string query)
        {
            Assert.IsNotOnMainThread();

            Assert.IsNeitherNullNorEmpty(query);

            _Verify();

            var queryMap = new METHOD_MAP
            {
                { "method", "facebook.fql.query" },
                { "query", query },
            };

            return _SendRequest(queryMap);
        }


        private string _SendMultiQuery(IList<string> names, IList<string> queries)
        {
            Assert.IsNotOnMainThread();

            _Verify();

            Assert.IsNotNull(names);
            Assert.IsNotNull(queries);
            Assert.AreEqual(names.Count, queries.Count);

            var dict = names
                .Zip(queries, (key, value) => new KeyValuePair<string, string>(key, value))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            var serializer = new JsonSerializer(typeof(object));

            string multiquery = serializer.Serialize(dict);

            var queryMap = new METHOD_MAP
            {
                { "method", "fql.multiquery" },
                { "queries", multiquery },
            };

            return _SendRequest(queryMap);
        }

        /// <summary>
        /// Sends a request with the given parameters to the server.
        /// </summary>
        /// <param name="requestPairs">A dictionary of parameters describing the request.</param>
        /// <returns>The HTTP response as a string.</returns>
        /// <remarks>
        /// This will modify the dictionary parameter to include additional information about the request.
        /// </remarks>
        private string _SendRequest(IDictionary<string, string> requestPairs)
        {
            Assert.IsNotOnMainThread();

            _Verify();

            if (!requestPairs.ContainsKey("api_key"))
            {
                requestPairs.Add("api_key", _ApplicationKey);
                requestPairs.Add("v", "1.0");
                requestPairs.Add("session_key", _SessionKey);
                // Need to signal that we're using the session secret instead.
                requestPairs.Add("ss", "1");
            }

            requestPairs["format"] = "json";

            return _SendRequest(requestPairs, _Secret);
        }


        private string _SendFileRequest(IDictionary<string, string> requestPairs, string filePath)
        {
            Assert.IsNotOnMainThread();

            _Verify();

            byte[] data = null;
            using (FileStream fs = File.Open(filePath, FileMode.Open))
            {
                data = new byte[fs.Length];
                fs.Read(data, 0, data.Length);
            }

            const string NewLine = "\r\n";
            string boundary = DateTime.Now.Ticks.ToString("x", CultureInfo.InvariantCulture);

            if (!requestPairs.ContainsKey("api_key"))
            {
                requestPairs.Add("api_key", _ApplicationKey);
                requestPairs.Add("v", "1.0");
                requestPairs.Add("session_key", _SessionKey);
                // Need to signal that we're using the session secret instead.
                requestPairs.Add("ss", "1");
                requestPairs.Add("sig", _GenerateSignature(requestPairs, _Secret));
            }

            requestPairs["format"] = "json";

            var builder = new StringBuilder();

            foreach (var pair in requestPairs)
            {
                builder
                    .Append("--").Append(boundary).Append(NewLine)
                    .Append("Content-Disposition: form-data; name=\"").Append(pair.Key).Append("\"").Append(NewLine)
                    .Append(NewLine)
                    .Append(pair.Value).Append(NewLine);
            }

            builder
                .Append("--").Append(boundary).Append(NewLine)
                .Append("Content-Disposition: form-data; filename=\"").Append("Sample.jpg").Append("\"").Append(NewLine)
                .Append("Content-Type: image/jpeg\r\n\r\n");

            byte[] bytes = Encoding.UTF8.GetBytes("\r\n" + "--" + boundary + "--" + "\r\n");
            byte[] buffer = Encoding.UTF8.GetBytes(builder.ToString());

            byte[] postData = null;
            using (MemoryStream stream = new MemoryStream((buffer.Length + data.Length) + bytes.Length))
            {
                stream.Write(buffer, 0, buffer.Length);
                stream.Write(data, 0, data.Length);
                stream.Write(bytes, 0, bytes.Length);
                postData = stream.GetBuffer();
            }

            var request = (HttpWebRequest)WebRequest.Create(_FacebookApiUri);
            // Use 1.0 because a significant number of proxies don't understand 1.1 (the default)
            request.ProtocolVersion = HttpVersion.Version10;

            request.ContentType = "multipart/form-data; boundary=" + boundary;
            request.Method = "POST";

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(postData, 0, postData.Length);
            }

            string result = null;
            using (WebResponse response = request.GetResponse())
            {
                using (var sr = new StreamReader(response.GetResponseStream()))
                {
                    result = sr.ReadToEnd();
                }
            }

            Exception e = _VerifyJsonResult(result, builder.ToString());
            if (e != null)
            {
                throw e;
            }

            return result;
        }

        private static string _SendRequest(IDictionary<string, string> requestPairs, string secret)
        {
            Assert.IsNotOnMainThread();

            string requestData = _GenerateRequestData(requestPairs, secret);

            var request = (HttpWebRequest)WebRequest.Create(_FacebookApiUri);
            // Use 1.0 because a significant number of proxies don't understand 1.1 (the default)
            request.ProtocolVersion = HttpVersion.Version10;

            request.ContentType = "application/x-www-form-urlencoded";
            request.Method = "POST";
            using (Stream requestStream = request.GetRequestStream())
            {
                using (var sw = new StreamWriter(requestStream))
                {
                    sw.Write(requestData);
                }
            }

            string result = null;
            using (WebResponse response = request.GetResponse())
            {
                using (var sr = new StreamReader(response.GetResponseStream()))
                {
                    result = sr.ReadToEnd();
                }
            }

            Exception e = _VerifyJsonResult(result, requestData);
            if (e != null)
            {
                throw e;
            }

            return result;
        }

        /// <summary>Check the response of a Facebook server call for error information.</summary>
        /// <param name="xml"></param>
        /// <returns>
        /// If the request was an error, returns an exception that describes the failure.
        /// Otherwise this returns null.
        private static Exception _VerifyJsonResult(string jsonInput, string sourceRequest)
        {
            try
            {
                return JsonDataSerialization.DeserializeFacebookException(jsonInput, sourceRequest);
            }
            catch (InvalidOperationException) { }

            // Yay, couldn't convert to an exception :) return null.
            return null;
        }

        /// <summary>
        /// Converts the dictionary describing a server request into a string in the format that the Facebook servers require,
        /// including an encrypted signature key based on the application's secret.
        /// </summary>
        /// <param name="requestPairs"></param>
        /// <returns></returns>
        private static string _GenerateRequestData(IDictionary<string, string> requestPairs, string secret)
        {
            if (!requestPairs.ContainsKey("sig"))
            {
                requestPairs.Add("sig", _GenerateSignature(requestPairs, secret));
            }
            return requestPairs.Aggregate(new StringBuilder(), (sb, kv) => sb.AppendFormat("&{0}={1}", kv.Key, kv.Value)).ToString();
        }

        private static string _GenerateSignature(IDictionary<string, string> requestPairs, string secret)
        {
            // Need to build a hash with the secret to authenticate this URL.
            string fullRequest = requestPairs.Aggregate(
                new StringBuilder(),
                (sb, kv) => sb.AppendFormat("{0}={1}", kv.Key, kv.Value))
                .Append(secret)
                .ToString();

            return Utility.GetHashString(fullRequest);
        }

        #region Methods that don't require a FacebookService/SessionKey

        public IEnumerable<Permissions> GetMissingPermissions(IEnumerable<Permissions> extendedPermissions)
        {
            var queryMap = new METHOD_MAP
            {
                { "method", "facebook.fql.query" },
                { "query", string.Format(_GetPermissionsQueryString, _ExtendedPermissionsColumns, _UserId) },
                { "api_key", _ApplicationKey },
                { "v", "1.0" },
                { "ss", "1" },
                { "session_key", _SessionKey },
                { "format", "json" },
            };

            string result = Utility.FailableFunction(() => _SendRequest(queryMap, _Secret));
            List<Permissions> grantedPermissions = (from permName in _jsonSerializer.DeserializeGrantedPermissionsList(result)
                                                    select _ReversePermissionLookup[permName])
                                                   .ToList();
            return extendedPermissions.Except(grantedPermissions);
        }

        #endregion

        public List<ActivityFilter> GetActivityFilters()
        {
            string result = Utility.FailableFunction(() => _SendQuery(string.Format(_GetStreamFiltersQueryString, _UserId)));
            return _jsonSerializer.DeserializeFilterList(result);
        }

        public FacebookContact TryGetUser(FacebookObjectId userId, bool getFullData)
        {
            _Verify();

            var userMap = new METHOD_MAP
            {
                { "method", "users.GetInfo" },
                { "uids", userId.ToString() },
                //{ "fields", _UserColumns },
            };

            if (getFullData)
            {
                userMap["fields"] = _UserColumns;
            }
            else
            {
                userMap["fields"] = _UserColumnsLite;
            }

            // Facebook bogusly errors this call fairly frequently.
            List<FacebookContact> contactList = null;
            int reaskCount = 0;
            do
            {
                string result = Utility.FailableFunction(5, () => _SendRequest(userMap));
                contactList = _jsonSerializer.DeserializeUsersList(result);
            } while (contactList.Count == 0 && ++reaskCount < 3);

            if (contactList.Count == 0)
            {
                // I'd like to do something better here.  This fails too frequently.
                // Maybe once we move to the graph API there will be a more privacy friendly way to get this data.
                return null;
                //throw new FacebookException("Unable to obtain information about the user.", null);
            }

            return contactList[0];
        }

        public List<MessageNotification> GetMailNotifications(bool includeRead)
        {
            _Verify();

            string query = includeRead
                ? _GetInboxThreadsQueryString 
                : _GetUnreadInboxThreadsQueryString;

            string result = Utility.FailableFunction(() => _SendQuery(query));
            List<MessageNotification> notifications = _jsonSerializer.DeserializeMessageQueryResponse(result);

            return notifications;
        }

        // This differs from Notifications.GetList in that it returns messages, pokes, shares, group and event invites, and friend requests.
        public void GetRequests(out List<Notification> friendRequests, out int unreadMessagesCount)
        {
            _Verify();

            var notificationMap = new METHOD_MAP
            {
                { "method", "Notifications.get" },
            };

            string result = Utility.FailableFunction(() => _SendRequest(notificationMap));
            _jsonSerializer.DeserializeNotificationsGetResponse(result, out friendRequests, out unreadMessagesCount);
        }

        public List<Notification> GetNotifications(bool includeRead)
        {
            _Verify();

            var notificationMap = new METHOD_MAP
            {
                { "method", "Notifications.getList" },
            };

            if (includeRead)
            {
                notificationMap.Add("include_read", "true");
            }

            string result = Utility.FailableFunction(() => _SendRequest(notificationMap));

            List<Notification> notifications = _jsonSerializer.DeserializeNotificationsListResponse(result);

            // Don't include hidden notifications in this result set.
            // Facebook also tends to leave stale notifications behind for activity posts that have been deleted.
            // If the title is empty, just don't include it.
            notifications.RemoveAll(n => n.IsHidden || string.IsNullOrEmpty(n.Title));

            return notifications;
        }

        public FacebookPhoto GetPhoto(string photoId)
        {
            _Verify();

            var photoMap = new METHOD_MAP
            {
                { "method", "photos.get" },
                { "pids", photoId },
            };

            string result = Utility.FailableFunction(() => _SendRequest(photoMap));

            List<FacebookPhoto> response = _jsonSerializer.DeserializePhotosGetResponse(result);

            FacebookPhoto photo = response.FirstOrDefault();
            Assert.IsNotNull(photo);

            return photo;
        }

        public List<FacebookPhotoTag> GetPhotoTags(FacebookObjectId photoId)
        {
            var tagMap = new METHOD_MAP
            {
                { "method", "photos.getTags" },
                { "pids", photoId.ToString() },
            };

            string response = Utility.FailableFunction(() => _SendRequest(tagMap));
            return _jsonSerializer.DeserializePhotoTagsList(response);
        }

        public List<FacebookPhotoTag> AddPhotoTag(FacebookObjectId photoId, FacebookObjectId userId, float x, float y)
        {
            Verify.IsTrue(FacebookObjectId.IsValid(userId), "Invalid userId");
            Verify.IsTrue(FacebookObjectId.IsValid(photoId), "Invalid photoId");

            x *= 100;
            y *= 100;
            var tagMap = new METHOD_MAP
            {
                { "method", "photos.addTag" },
                { "pid", photoId.ToString() },
                { "tag_uid", userId.ToString() },
                { "x", string.Format("{0:0.##}", x) },
                { "y", string.Format("{0:0.##}", y) },
            };

            string response = Utility.FailableFunction(() => _SendRequest(tagMap));

            return GetPhotoTags(photoId);
        }

        public FacebookPhotoAlbum CreateAlbum(string name, string description, string location)
        {
            _Verify();

            var createMap = new METHOD_MAP
            {
                { "method", "photos.createAlbum" },
                { "name", name },
            };

            if (!string.IsNullOrEmpty(description))
            {
                createMap.Add("description", description);
            }

            if (!string.IsNullOrEmpty(location))
            {
                createMap.Add("location", location);
            }

            string createAlbumResponse = Utility.FailableFunction(() => _SendRequest(createMap));

            FacebookPhotoAlbum album = _jsonSerializer.DeserializeUploadAlbumResponse(createAlbumResponse);
            album.RawPhotos = new FBMergeableCollection<FacebookPhoto>();
            return album;
        }

        public FacebookPhoto AddPhotoToAlbum(FacebookObjectId albumId, string caption, string imageFile)
        {
            _Verify();

            var updateMap = new METHOD_MAP
            {
                { "method", "photos.upload" },
            };

            if (FacebookObjectId.IsValid(albumId))
            {
                updateMap.Add("aid", albumId.ToString());
            }

            if (!string.IsNullOrEmpty(caption))
            {
                updateMap.Add("caption", caption);
            }

            string response = Utility.FailableFunction(() => _SendFileRequest(updateMap, imageFile));
            return _jsonSerializer.DeserializePhotoUploadResponse(response);
        }

        public FacebookPhotoAlbum GetAlbum(FacebookObjectId albumId)
        {
            Verify.IsTrue(FacebookObjectId.IsValid(albumId), "Invalid albumId");

            var albumMap = new METHOD_MAP
            {
                { "method", "photos.getAlbums" },
                { "aids", albumId.ToString() },
            };

            string response = Utility.FailableFunction(() => _SendRequest(albumMap));
            List<FacebookPhotoAlbum> albumsResponse = _jsonSerializer.DeserializeGetAlbumsResponse(response);

            Assert.IsFalse(albumsResponse.Count > 1);

            if (albumsResponse.Count == 0)
            {
                return null;
            }
            return albumsResponse[0];
        }

        public ActivityPost PublishStream(FacebookObjectId targetId, string message)
        {
            var streamMap = new METHOD_MAP
            {
                { "method", "stream.publish" },
                { "message", message },
                { "target_id", targetId.ToString() },
            };

            Utility.FailableFunction(() => _SendRequest(streamMap));

            // Return a proxy that looks close to what we expect the updated status to look like.
            // We'll replace it with the real one the next time we sync.
            return new ActivityPost(_Service)
            {
                ActorUserId = _UserId,
                TargetUserId = targetId,
                Attachment = null,
                CanComment = false,
                CanLike = false,
                CanRemoveComments = false,
                CommentCount = 0,
                Created = DateTime.Now,
                HasLiked = false,
                LikedCount = 0,
                LikeUri = null,
                Message = message,
                PostId = new FacebookObjectId("-1"),
                RawComments = new FBMergeableCollection<ActivityComment>(),
                Updated = DateTime.Now,
            };
        }

        public ActivityPost UpdateStatus(string newStatus)
        {
            var statusMap = new METHOD_MAP
            {
                { "method", "status.set" },
                { "status", newStatus },
                { "uid", _UserId.ToString() },
            };

            string result = Utility.FailableFunction(() => _SendRequest(statusMap));
            bool success = _jsonSerializer.DeserializeStatusSetResponse(result);
            Assert.IsTrue(success);

            // Return a proxy that looks close to what we expect the updated status to look like.
            // We'll replace it with the real one the next time we sync, unless a feed is being shown
            // from which this should be excluded.
            return new ActivityPost(_Service)
            {
                ActorUserId = _UserId,
                Attachment = null,
                CanComment = false,
                CanLike = false,
                CanRemoveComments = false,
                CommentCount = 0,
                Created = DateTime.Now,
                HasLiked = false,
                LikedCount = 0,
                LikeUri = null,
                Message = newStatus,
                PostId = new FacebookObjectId("FakeStatusId"),
                RawComments = new FBMergeableCollection<ActivityComment>(),
                TargetUserId = default(FacebookObjectId),
                Updated = DateTime.Now,
            };
        }

        // Consider: return a proxy post similar to posting a new status.
        public void PostLink(string comment, string uri)
        {
            Assert.IsNeitherNullNorWhitespace(comment);
            Assert.IsNeitherNullNorWhitespace(uri);

            var statusMap = new METHOD_MAP
            {
                { "method", "Links.Post" },
                { "url", uri },
                { "comment", comment },
            };

            Utility.FailableFunction(() => _SendRequest(statusMap));
        }

        public List<ActivityPost> GetStreamPosts(FacebookObjectId userId)
        {
            string result = Utility.FailableFunction(() => _SendQuery(string.Format(_GetStreamPostsQueryString, userId)));
            return _jsonSerializer.DeserializeStreamPostList(result);
        }

        public List<ActivityPost> GetStream(FacebookObjectId filterKey, int limit, DateTime getItemsSince)
        {
            Assert.IsTrue(limit > 0);

            // Facebook changed the semantics of the default feed, so we need to explicitly 
            // request the newsfeed filter to keep things working as expected.
            // I think everyone should have a filter with this key, but this is unfortunately fragile.
            if (!FacebookObjectId.IsValid(filterKey))
            {
                filterKey = new FacebookObjectId("nf");
            }

            long startTime = JsonDataSerialization.GetUnixTimestampFromDateTime(getItemsSince);
            Assert.IsTrue(startTime >= 0);

            // Don't use metadata field.  Facebook regressed support for it.
            // Getting profile data needs to be simulated at a higher level.
            var streamMap = new METHOD_MAP
            {
                { "method", "stream.get" },
                { "viewer_id", _UserId.ToString() },
                { "start_time", startTime.ToString("G") },
                { "limit", limit.ToString("G") },
                { "filter_key", filterKey.ToString() },
            };

            string result = Utility.FailableFunction(() => _SendRequest(streamMap));

            return _jsonSerializer.DeserializeStreamData(result);

            //var userIds = new HashSet<FacebookObjectId>();
            //foreach (var post in posts)
            //{
            //    userIds.Add(post.ActorUserId);
            //    userIds.Add(post.TargetUserId);
            //    userIds.AddRange(from comment in post.Comments select comment.FromUserId);
            //    userIds.AddRange(from liker in post.PeopleWhoLikeThis select liker.UserId);
            //}
        }

        public List<FacebookContact> GetFriendProfiles()
        {
            string result = Utility.FailableFunction(() => _SendQuery(string.Format(_GetFriendsProfilesQueryString, _UserId)));
            return _jsonSerializer.DeserializeProfileList(result);
        }

        public FacebookObjectId AddComment(ActivityPost post, string comment)
        {
            var commentMap = new METHOD_MAP
                {
                    { "method", "stream.addComment" },
                    { "post_id", post.PostId.ToString() },
                    { "comment", comment },
                };

            string result = Utility.FailableFunction(() => _SendRequest(commentMap));
            // retrieve the new comment Id.
            return _jsonSerializer.DeserializeAddCommentResponse(result);
        }

        public List<ActivityComment> GetComments(ActivityPost post)
        {
            Assert.IsNotNull(post);

            var commentMap = new METHOD_MAP
            {
                { "method", "stream.getComments" },
                { "post_id", post.PostId.ToString() },
            };

            string response = Utility.FailableFunction(10, () => _SendRequest(commentMap));
            return _jsonSerializer.DeserializeCommentsDataList(post, response);
        }

        public void RemoveComment(FacebookObjectId commentId)
        {
            if (!FacebookObjectId.IsValid(commentId))
            {
                // If we're removing a comment that we haven't yet posted we can't remove it.
                return;
            }
            var commentMap = new METHOD_MAP
            { 
                { "method", "stream.removeComment" },
                { "comment_id", commentId.ToString() },
            };

            Utility.FailableFunction(() => _SendRequest(commentMap));
        }

        public void AddLike(FacebookObjectId postId)
        {
            var likeMap = new METHOD_MAP
            {
                { "method", "stream.addLike" },
                { "post_id", postId.ToString() },
            };

            Utility.FailableFunction(() => _SendRequest(likeMap));
        }

        public void RemoveLike(FacebookObjectId postId)
        {
            var likeMap = new METHOD_MAP
            {
                { "method", "stream.removeLike" },
                { "post_id", postId.ToString() },
            };

            Utility.FailableFunction(() => _SendRequest(likeMap));
        }

        public List<FacebookContact> GetPages()
        {
            var pagesMap = new METHOD_MAP
            {
                { "method", "pages.getInfo" },
                { "fields", _PageColumns },
                { "uid", _UserId.ToString() },
            };

            string result = Utility.FailableFunction(() => _SendRequest(pagesMap));

            return _jsonSerializer.DeserializePagesList(result);
        }

        public List<FacebookContact> GetFriends()
        {
            // This tends to fail for large friends lists, so instead we batch it into multiple, smaller calls.
            int batchLimit = 50;
            var friendsSoFar = new List<FacebookContact>();
            for (int offset = 0; true; offset += batchLimit)
            {
                string friendQueryResult = Utility.FailableFunction(() => _SendQuery(string.Format(_GetFriendsLimitOffsetFormatQueryString, _UserId, batchLimit, offset)));
                var batchResult = _jsonSerializer.DeserializeUsersList(friendQueryResult);
                if (batchResult.Count == 0)
                {
                    break;
                }

                friendsSoFar.AddRange(batchResult);
            }

            return friendsSoFar;
        }

        public Dictionary<FacebookObjectId, OnlinePresence> GetFriendsOnlineStatus()
        {
            string result = Utility.FailableFunction(() => _SendQuery(string.Format(_GetFriendsOnlineStatusQueryString, _UserId)));
            return _jsonSerializer.DeserializeUserPresenceList(result);
        }

        public List<FacebookPhotoAlbum> GetFriendsPhotoAlbums()
        {
            string albumQueryResult = Utility.FailableFunction(() => _SendQuery(string.Format(_GetFriendsAlbumsQueryString, _UserId)));
            return _jsonSerializer.DeserializeGetAlbumsResponse(albumQueryResult);
        }

        public List<FacebookPhoto>[] GetPhotosWithTags(IEnumerable<FacebookObjectId> albumIds)
        {
            var names = new List<string>();
            var queries = new List<string>();

            int albumCount = 0;
            foreach (FacebookObjectId albumId in albumIds)
            {
                names.Add("get_photos" + albumCount);
                names.Add("get_tags" + albumCount);

                queries.Add(string.Format(_GetPhotosFromAlbumQueryString, albumId.ToString()));
                queries.Add(string.Format(_GetPhotoTagsMultiQueryString, "get_photos" + albumCount));
                ++albumCount;
            }

            string photoMultiQueryResult = Utility.FailableFunction(() => _SendMultiQuery(names, queries));

            JSON_ARRAY jsonMultiqueryArray = JsonDataSerialization.SafeParseArray(photoMultiQueryResult);

            var photoCollections = new List<FacebookPhoto>[albumCount];

            albumCount = 0;
            foreach (FacebookObjectId albumId in albumIds)
            {
                JSON_ARRAY photosResponseArray = (from JSON_OBJECT result in jsonMultiqueryArray
                                                  where result.Get<string>("name") == "get_photos" + albumCount
                                                  select result.Get<JSON_ARRAY>("fql_result_set"))
                                                 .First();
                List<FacebookPhoto> photos = _jsonSerializer.DeserializePhotosGetResponse(photosResponseArray);

                JSON_ARRAY tagsResponseArray = (from JSON_OBJECT result in jsonMultiqueryArray
                                                where result.Get<string>("name") == "get_tags" + albumCount
                                                select result.Get<JSON_ARRAY>("fql_result_set"))
                                                .First();
                List<FacebookPhotoTag> tags = _jsonSerializer.DeserializePhotoTagsList(tagsResponseArray);

                foreach (var photo in photos)
                {
                    photo.RawTags.Merge(from tag in tags where tag.PhotoId == photo.PhotoId select tag, false);
                }

                photoCollections[albumCount] = photos;
                ++albumCount;
            }
            return photoCollections;
        }

        public List<ActivityComment> GetPhotoComments(FacebookObjectId photoId)
        {
            var commentMap = new METHOD_MAP
            {
                { "method", "photos.getComments" },
                { "pid", photoId.ToString() },
            };

            string response = Utility.FailableFunction(() => _SendRequest(commentMap));
            return _jsonSerializer.DeserializePhotoCommentsResponse(response);
        }

        public bool GetPhotoCanComment(FacebookObjectId photoId)
        {
            var commentMap = new METHOD_MAP
            {
                { "method", "photos.canComment" },
                { "pid", photoId.ToString() },
            };

            string response = Utility.FailableFunction(() => _SendRequest(commentMap));
            return _jsonSerializer.DeserializePhotoCanCommentResponse(response);
        }

        public FacebookObjectId AddPhotoComment(FacebookObjectId photoId, string comment)
        {
            var addMap = new METHOD_MAP
            {
                { "method", "photos.addComment" },
                { "pid", photoId.ToString() },
                { "body", comment },
            };

            string response = Utility.FailableFunction(() => _SendRequest(addMap));
            return _jsonSerializer.DeserializePhotoAddCommentResponse(response);
        }

        public List<FacebookPhotoAlbum> GetUserAlbums(FacebookObjectId userId)
        {
            Verify.IsTrue(FacebookObjectId.IsValid(userId), "Invalid userId");

            string albumQueryResult = Utility.FailableFunction(() => _SendQuery(string.Format(_GetSingleUserAlbumsQueryString, userId)));
            return _jsonSerializer.DeserializeGetAlbumsResponse(albumQueryResult);
        }

        public void MarkNotificationsAsRead(params FacebookObjectId[] notificationIds)
        {
            Verify.IsNotNull(notificationIds, "notificationIds");
            
            var sb = new StringBuilder();
            bool isFirst = true;
            foreach (FacebookObjectId id in notificationIds)
            {
                if (FacebookObjectId.IsValid(id))
                {
                    if (!isFirst)
                    {
                        sb.Append(",");
                    }
                    sb.Append(id.ToString());
                }
            }

            if (sb.Length == 0)
            {
                return;
            }
            
            var readMap = new METHOD_MAP
            {
                { "method", "Notifications.markRead" },
                { "notification_ids", sb.ToString() },
            };

            Utility.FailableFunction(() => _SendRequest(readMap));
        }
    }
}
