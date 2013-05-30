
namespace Contigo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using Microsoft.Json.Serialization;
    using Standard;

    using JSON_ARRAY = System.Collections.Generic.IList<object>;
    using JSON_OBJECT = System.Collections.Generic.IDictionary<string, object>;
    using System.Text;

    internal enum JsonObjectType
    {
        NotJsonObject,
        Primitive,
        Array,
        Object,
    }

    internal static class JSON_OBJECT_EXTENSIONS
    {
        public static bool TryGetTypedValue<T>(this JSON_OBJECT dictionary, string key, out T value)
        {
            value = default(T);

            object o;
            if (dictionary.TryGetValue(key, out o))
            {
                value = (T)o;
                return value != null;
            }

            return false;
        }

        public static T Get<T>(this JSON_OBJECT dictionary, string key)
        {
            Verify.IsNeitherNullNorEmpty("key", key);
            return (T)dictionary[key];
        }

        public static JsonObjectType GetJsonObjectType(this object o)
        {
            if (o == null)
            {
                // Not kosher to pass a null this pointer...
                Assert.Fail();
                return JsonObjectType.Primitive;
            }

            Type t = o.GetType();
            bool isPrimitive = t == typeof(string)
                || t == typeof(bool)
                || t == typeof(int);

            if (isPrimitive)
            {
                return JsonObjectType.Primitive;
            }

            if (Utility.IsInterfaceImplemented(t, typeof(IDictionary<string, object>)))
            {
                return JsonObjectType.Object;
            }
                            
            if (Utility.IsInterfaceImplemented(t, typeof(IList<object>)))
            {
                return JsonObjectType.Array;
            }

            Assert.Fail();
            return JsonObjectType.NotJsonObject;
        }
    }

    internal class JsonDataSerialization
    {
        // Just in case Facebook messes up and gives us bad data for an id that's supposed to be unique.  Don't let it crash the app.
        private static int _badFacebookCounter = 1;

        internal static FacebookObjectId SafeGetUniqueId()
        {
            return new FacebookObjectId("FacebookGotItWrongCount_" + _badFacebookCounter++);
        }

        /// <summary>The start time for Unix based clocks.  Facebook usually returns their timestamps based on ticks from this value.</summary>
        private static readonly DateTime _UnixEpochTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Converts a timestamp from the facebook server into a UTC DateTime.
        /// </summary>
        /// <param name="ticks">The Unix epoch based tick count</param>
        /// <returns>The parameter as a DateTime.</returns>
        internal static DateTime GetDateTimeFromUnixTimestamp(long ticks)
        {
            return _UnixEpochTime.AddSeconds(ticks).ToLocalTime();
        }

        internal static long GetUnixTimestampFromDateTime(DateTime date)
        {
            date = date.ToUniversalTime();
            if (date > _UnixEpochTime)
            {
                return (long)(date - _UnixEpochTime).TotalSeconds;
            }
            return 0;
        }

        private readonly FacebookService _service;
        private static readonly JsonSerializer _serializer = new JsonSerializer(typeof(object));

        public JsonDataSerialization(FacebookService service)
        {
            _service = service;
        }

        private static object _SafeGetValue(JSON_OBJECT jsonObj, params string[] names)
        {
            if (jsonObj == null)
            {
                return null;
            }

            object lastChild = jsonObj;
            foreach (var name in names)
            {
                if (lastChild.GetJsonObjectType() != JsonObjectType.Object)
                {
                    return null;
                }

                jsonObj = (JSON_OBJECT)lastChild;
                if (!jsonObj.TryGetValue(name, out lastChild) || lastChild == null)
                {
                    return null;
                }
            }

            return lastChild;
        }

        private static string _SafeGetString(JSON_OBJECT jsonObj, params string[] names)
        {
            object o = _SafeGetValue(jsonObj, names);
            if (o == null)
            {
                return null;
            }

            if (o.GetJsonObjectType() != JsonObjectType.Primitive)
            {
                Assert.Fail();
                return null;
            }

            Assert.IsTrue(o is string);
            return (string)o;
        }

        private static FacebookObjectId _SafeGetId(JSON_OBJECT jsonObj, params string[] names)
        {
            var idValue = _SafeGetString(jsonObj, names);
            return new FacebookObjectId(idValue);
        }

        private static DateTime? _SafeGetDateTime(JSON_OBJECT jsonObj, params string[] names)
        {
            long? ticks = _SafeGetInt64(jsonObj, names);
            if (ticks == null)
            {
                return null;
            }

            return JsonDataSerialization.GetDateTimeFromUnixTimestamp(ticks.Value);
        }

        private static string _SafeGetJson(JSON_OBJECT jsonObj, params string[] names)
        {
            return _serializer.Serialize(_SafeGetValue(jsonObj, names));
        }

        private static float? _SafeGetSingle(JSON_OBJECT jsonObj, params string[] names)
        {
            object o = _SafeGetValue(jsonObj, names);
            if (o is float)
            {
                return (float)o;
            }
            if (o is string)
            {
                float ret;
                if (float.TryParse((string)o, out ret))
                {
                    return ret;
                }
            }
            return null;
        }

        private static bool? _SafeGetBoolean(JSON_OBJECT jsonObj, params string[] names)
        {
            object o = _SafeGetValue(jsonObj, names);
            if (o is bool)
            {
                return (bool)o;
            }

            // Facebook tends to return 0 and 1 as boolean parameters as well.
            int? i = null;
            if (o is string)
            {
                int temp;
                if (int.TryParse((string)o, out temp))
                {
                    i = temp;
                }
            }
            else if (o is int)
            {
                i = (int)o;
            }

            if (i.HasValue)
            {
                return i == 1;
            }

            return null;
        }

        private static int? _SafeGetInt32(JSON_OBJECT jsonObj, params string[] names)
        {
            object o = _SafeGetValue(jsonObj, names);
            if (o is int)
            {
                return (int)o;
            }
            if (o is string)
            {
                int i;
                if (int.TryParse((string)o, out i))
                {
                    return i;
                }
            }

            return null;
        }

        private static long? _SafeGetInt64(JSON_OBJECT jsonObj, params string[] names)
        {
            object o = _SafeGetValue(jsonObj, names);
            if (o is long)
            {
                return (long)o;
            }
            if (o is string)
            {
                long i;
                if (long.TryParse((string)o, out i))
                {
                    return i;
                }
            }

            return null;
        }

        private static Uri _SafeGetUri(JSON_OBJECT jsonObj, params string[] names)
        {
            object o = _SafeGetValue(jsonObj, names);
            if (o is string)
            {
                Uri ret;
                if (Uri.TryCreate((string)o, UriKind.Absolute, out ret))
                {
                    Assert.IsTrue(ret.IsAbsoluteUri);
                    return ret;
                }
            }
            return null;
        }

        public static JSON_OBJECT SafeParseObject(string jsonString)
        {
            object obj = _serializer.Deserialize(jsonString);
            // Mitigate Facebook's tendency to return 0 and 1 length arrays for FQL calls.
            JSON_ARRAY ary = obj as JSON_ARRAY;
            while (ary != null)
            {
                switch (ary.Count)
                {
                    case 0:
                        return null;
                    case 1:
                        obj = ary[0];
                        ary = obj as JSON_ARRAY;
                        break;
                    default:
                        // Probably a caller error expecting an object instead of an array...
                        Assert.Fail();
                        return null;
                }
            }
            Assert.IsTrue(obj is JSON_OBJECT);
            return obj as JSON_OBJECT;
        }

        public static JSON_ARRAY SafeParseArray(string jsonString)
        {
            var obj = _serializer.Deserialize(jsonString);
            Assert.IsTrue(obj is JSON_ARRAY);
            return obj as JSON_ARRAY;
        }

        private ActivityComment _DeserializeComment(JSON_OBJECT obj)
        {
            return new ActivityComment(_service)
            {
                CommentType = ActivityComment.Type.ActivityPost,
                FromUserId = _SafeGetId(obj, "fromid"),
                Time = _SafeGetDateTime(obj, "time") ?? _UnixEpochTime,
                Text = _SafeGetString(obj, "text"),
                CommentId = _SafeGetId(obj, "id"),
            };
        }

        public List<FacebookContact> DeserializeUsersList(string jsonString)
        {
            JSON_ARRAY userList = SafeParseArray(jsonString);
            return new List<FacebookContact>(from JSON_OBJECT jsonUser in userList select _DeserializeUser(jsonUser));
        }

        private WorkInfo _DeserializeWorkInfo(JSON_OBJECT jsonWorkInfo)
        {
            Assert.IsNotNull(jsonWorkInfo);

            return new WorkInfo
            {
                CompanyName = _SafeGetString(jsonWorkInfo, "company_name"),
                Description = _SafeGetString(jsonWorkInfo, "description"),
                EndDate = _SafeGetString(jsonWorkInfo, "end_date"),
                StartDate = _SafeGetString(jsonWorkInfo, "start_date"),
                Location = _DeserializeLocation(jsonWorkInfo.Get<JSON_OBJECT>("location")),
            };
        }

        private static Location _DeserializeLocation(JSON_OBJECT jsonLocation)
        {
            if (jsonLocation == null)
            {
                return null;
            }

            return new Location
            {
                // current_location: city, state, country (well defined), zip (may be zero)
                City = _SafeGetString(jsonLocation, "city"),
                Country = _SafeGetString(jsonLocation, "country"),
                State = _SafeGetString(jsonLocation, "state"),
                ZipCode = _SafeGetInt32(jsonLocation, "zip")
            };
        }

        private EducationInfo _DeserializeEducationInfo(JSON_OBJECT jsonEducationInfo)
        {
            int? maybeYear = _SafeGetInt32(jsonEducationInfo, "year");
            if (maybeYear == 0)
            {
                maybeYear = null;
            }

            var concentrationBuilder = new StringBuilder();
            JSON_ARRAY jsonConcentationList = jsonEducationInfo.Get<JSON_ARRAY>("concentrations");
            if (jsonConcentationList != null)
            {
                bool first = true;
                foreach (string conString in jsonConcentationList)
                {
                    if (!first)
                    {
                        concentrationBuilder.Append(", ");
                    }
                    else
                    {
                        first = false;
                    }

                    concentrationBuilder.Append(conString);
                }
            }

            return new EducationInfo
            {
                Concentrations = concentrationBuilder.ToString(),
                Degree = _SafeGetString(jsonEducationInfo, "degree"),
                Name = _SafeGetString(jsonEducationInfo, "name"),
                Year = maybeYear,
            };
        }

        private FacebookContact _DeserializeUser(JSON_OBJECT jsonUser)
        {
            Uri sourceUri = _SafeGetUri(jsonUser, "pic");
            Uri sourceBigUri = _SafeGetUri(jsonUser, "pic_big");
            Uri sourceSmallUri = _SafeGetUri(jsonUser, "pic_small");
            Uri sourceSquareUri = _SafeGetUri(jsonUser, "pic_square");

            Location currentLocation = null;
            Location hometownLocation = null;
            HighSchoolInfo hsInfo = null;
            List<EducationInfo> educationHistory = null;
            List<WorkInfo> workHistory = null;

            JSON_OBJECT jsonLocation;
            if (jsonUser.TryGetTypedValue<JSON_OBJECT>("current_location", out jsonLocation))
            {
                currentLocation = _DeserializeLocation(jsonLocation);
            }

            JSON_OBJECT htObject;
            if (jsonUser.TryGetTypedValue<JSON_OBJECT>("hometown_location", out htObject))
            {
                hometownLocation = _DeserializeLocation(htObject);
            }

            JSON_OBJECT jsonHighSchool;
            if (jsonUser.TryGetTypedValue<JSON_OBJECT>("hs_info", out jsonHighSchool))
            {
                hsInfo = new HighSchoolInfo
                {
                    GraduationYear = _SafeGetInt32(jsonHighSchool, "grad_year"),
                    //Id = _SafeGetValue(hsElement, "hs1_id") ?? "0",
                    //Id2 = _SafeGetValue(hsElement, "hs2_id") ?? "0",
                    Name = _SafeGetString(jsonHighSchool, "hs1_name"),
                    Name2 = _SafeGetString(jsonHighSchool, "hs2_name"),
                };
            }

            JSON_ARRAY jsonEducationHistoryList;
            if (jsonUser.TryGetTypedValue<JSON_ARRAY>("education_history", out jsonEducationHistoryList))
            {
                educationHistory = new List<EducationInfo>(from JSON_OBJECT jsonEducationInfo in jsonEducationHistoryList select _DeserializeEducationInfo(jsonEducationInfo));
            }
            else
            {
                educationHistory = new List<EducationInfo>();
            }

            JSON_ARRAY jsonWorkHistoryList;
            if (jsonUser.TryGetTypedValue<JSON_ARRAY>("work_history", out jsonWorkHistoryList))
            {
                workHistory = new List<WorkInfo>(from JSON_OBJECT jsonWorkInfo in jsonWorkHistoryList select _DeserializeWorkInfo(jsonWorkInfo));
            }
            else
            {
                workHistory = new List<WorkInfo>();
            }

            var contact = new FacebookContact(_service)
            {
                Name = _SafeGetString(jsonUser, "name"),
                FirstName = _SafeGetString(jsonUser, "first_name"),
                LastName = _SafeGetString(jsonUser, "last_name"),

                AboutMe = _SafeGetString(jsonUser, "about_me"),
                Activities = _SafeGetString(jsonUser, "activities"),
                // Affilitions =
                // AllowedRestrictions = 
                Birthday = _SafeGetString(jsonUser, "birthday"),
                MachineSafeBirthday = _SafeGetString(jsonUser, "birthday_date"),
                Books = _SafeGetString(jsonUser, "books"),
                CurrentLocation = currentLocation,
                EducationHistory = educationHistory.AsReadOnly(),
                Hometown = hometownLocation,
                HighSchoolInfo = hsInfo,
                Interests = _SafeGetString(jsonUser, "interests"),
                Image = new FacebookImage(_service, sourceUri, sourceBigUri, sourceSmallUri, sourceSquareUri),
                Movies = _SafeGetString(jsonUser, "movies"),
                Music = _SafeGetString(jsonUser, "music"),
                Quotes = _SafeGetString(jsonUser, "quotes"),
                RelationshipStatus = _SafeGetString(jsonUser, "relationship_status"),
                Religion = _SafeGetString(jsonUser, "religion"),
                Sex = _SafeGetString(jsonUser, "sex"),
                TV = _SafeGetString(jsonUser, "tv"),
                Website = _SafeGetString(jsonUser, "website"),
                ProfileUri = _SafeGetUri(jsonUser, "profile_url"),
                UserId = _SafeGetId(jsonUser, "uid"),
                UserName = _SafeGetString(jsonUser, "username"),
                ProfileUpdateTime = _SafeGetDateTime(jsonUser, "profile_update_time") ?? _UnixEpochTime,
                OnlinePresence = _DeserializePresenceFromUser(jsonUser),
            };

            if (!string.IsNullOrEmpty(_SafeGetString(jsonUser, "status", "message")))
            {
                contact.StatusMessage = new ActivityPost(_service)
                {
                    PostId = new FacebookObjectId("status_" + contact.UserId.ToString()),
                    ActorUserId = _SafeGetId(jsonUser, "uid"),
                    Created = _SafeGetDateTime(jsonUser, "status", "time") ?? _UnixEpochTime,
                    Updated = _SafeGetDateTime(jsonUser, "status", "time") ?? _UnixEpochTime,
                    Message = _SafeGetString(jsonUser, "status", "message"),
                    TargetUserId = default(FacebookObjectId),
                    CanLike = false,
                    HasLiked = false,
                    LikedCount = 0,
                    CanComment = false,
                    CanRemoveComments = false,
                    CommentCount = 0,
                };
            }

            return contact;
        }

        public List<MessageNotification> DeserializeMessageQueryResponse(string jsonString)
        {
            JSON_ARRAY messageList = SafeParseArray(jsonString);
            return new List<MessageNotification>(from JSON_OBJECT jsonThread in messageList select _DeserializeMessageNotification(jsonThread));
        }

        private MessageNotification _DeserializeMessageNotification(JSON_OBJECT jsonThread)
        {
            var message = new MessageNotification(_service)
            {
                Created = _SafeGetDateTime(jsonThread, "updated_time") ?? _UnixEpochTime,
                IsUnread = _SafeGetBoolean(jsonThread, "unread") ?? true,
                DescriptionText = _SafeGetString(jsonThread, "snippet"),
                IsHidden = false,
                NotificationId = _SafeGetId(jsonThread, "thread_id"),
                SenderId = _SafeGetId(jsonThread, "snippet_author"),
                Title = _SafeGetString(jsonThread, "subject"),
                Updated = _SafeGetDateTime(jsonThread, "updated_time") ?? DateTime.Now,
            };

            // TODO: This is actually a list of recipients.
            var jsonRecipientList = jsonThread.Get<JSON_ARRAY>("recipients");
            if (jsonRecipientList != null && jsonRecipientList.Count > 0)
            {
                message.RecipientId = new FacebookObjectId(jsonRecipientList[0].ToString());
            }

            if (FacebookObjectId.IsValid(message.NotificationId))
            {
                message.Link = new Uri(string.Format("http://www.facebook.com/inbox/#/inbox/?folder=[fb]messages&page=1&tid={0}", message.NotificationId));
            }
            else
            {
                Assert.Fail();
                message.NotificationId = SafeGetUniqueId();
                message.Link = new Uri("http://www.facebook.com/inbox");
            }

            return message;
        }

        private FacebookContact _DeserializePage(JSON_OBJECT jsonPage)
        {
            Uri sourceUri = _SafeGetUri(jsonPage, "pic");
            Uri sourceBigUri = _SafeGetUri(jsonPage, "pic_big");
            Uri sourceSmallUri = _SafeGetUri(jsonPage, "pic_small");
            Uri sourceSquareUri = _SafeGetUri(jsonPage, "pic_square");
            // No idea why there are both large and big...
            Uri sourceLargeUri = _SafeGetUri(jsonPage, "pic_large");

            // This is a light weight view of a page as a FacebookContact.
            // If the FacebookService were to expose pages as a first-class concept then there would be a dedicated class,
            // and probably a base class.
            var page = new FacebookContact(_service)
            {
                UserId = _SafeGetId(jsonPage, "page_id"),
                Name = _SafeGetString(jsonPage, "name"),
                ProfileUri = _SafeGetUri(jsonPage, "page_url"),
                Image = new FacebookImage(_service, sourceUri, sourceBigUri ?? sourceLargeUri, sourceSmallUri, sourceSquareUri),
            };

            return page;
        }

        public List<FacebookContact> DeserializePagesList(string jsonInput)
        {
            JSON_ARRAY jsonPages = SafeParseArray(jsonInput);
            return new List<FacebookContact>(from JSON_OBJECT jsonPage in jsonPages select _DeserializePage(jsonPage));
        }

        public Dictionary<FacebookObjectId, OnlinePresence> DeserializeUserPresenceList(string jsonString)
        {
            JSON_ARRAY jsonFriendsList = SafeParseArray(jsonString);
            return (from JSON_OBJECT jsonFriend in jsonFriendsList
                    let uid = _SafeGetId(jsonFriend, "uid")
                    let presence = _DeserializePresenceFromUser(jsonFriend)
                    select new { UserId = uid, Presence = presence })
                    .ToDictionary(item => item.UserId, item => item.Presence);
        }

        private static OnlinePresence _DeserializePresenceFromUser(JSON_OBJECT jsonFriend)
        {
            string presence = _SafeGetString(jsonFriend, "online_presence");
            switch (presence)
            {
                case "active": return OnlinePresence.Active;
                case "idle": return OnlinePresence.Idle;
                case "offline": return OnlinePresence.Offline;
                case "error": return OnlinePresence.Unknown;
            }
            return OnlinePresence.Unknown;
        }

        public List<FacebookPhoto> DeserializePhotosGetResponse(string jsonInput)
        {
            JSON_ARRAY photosList = SafeParseArray(jsonInput);
            return DeserializePhotosGetResponse(photosList);
        }

        public List<FacebookPhoto> DeserializePhotosGetResponse(JSON_ARRAY jsonPhotosResponse)
        {
            return new List<FacebookPhoto>(from JSON_OBJECT jsonPhoto in jsonPhotosResponse select _DeserializePhoto(jsonPhoto));
        }

        private FacebookPhoto _DeserializePhoto(JSON_OBJECT jsonPhoto)
        {
            Uri linkUri = _SafeGetUri(jsonPhoto, "link");
            Uri sourceUri = _SafeGetUri(jsonPhoto, "src");
            Uri smallUri = _SafeGetUri(jsonPhoto, "src_small");
            Uri bigUri = _SafeGetUri(jsonPhoto, "src_big");

            var photo = new FacebookPhoto(_service)
            {
                PhotoId = _SafeGetId(jsonPhoto, "pid"),
                AlbumId = _SafeGetId(jsonPhoto, "aid"),
                OwnerId = _SafeGetId(jsonPhoto, "owner"),
                Caption = _SafeGetString(jsonPhoto, "caption"),
                Created = _SafeGetDateTime(jsonPhoto, "created") ?? _UnixEpochTime,
                Image = new FacebookImage(_service, sourceUri, bigUri, smallUri, null),
                Link = linkUri,
            };

            return photo;
        }

        public List<FacebookPhotoTag> DeserializePhotoTagsList(string jsonInput)
        {
            JSON_ARRAY jsonPhotoTags = SafeParseArray(jsonInput);
            return DeserializePhotoTagsList(jsonPhotoTags);
        }

        public List<FacebookPhotoTag> DeserializePhotoTagsList(JSON_ARRAY jsonPhotoTags)
        {
            return new List<FacebookPhotoTag>(from JSON_OBJECT photoTag in jsonPhotoTags select _DeserializePhotoTag(photoTag));
        }

        private FacebookPhotoTag _DeserializePhotoTag(JSON_OBJECT jsonTag)
        {
            float xcoord = (_SafeGetSingle(jsonTag, "xcoord") ?? 0) / 100;
            float ycoord = (_SafeGetSingle(jsonTag,"ycoord") ?? 0) / 100;

            xcoord = Math.Max(Math.Min(1, xcoord), 0);
            ycoord = Math.Max(Math.Min(1, ycoord), 0);

            var tag = new FacebookPhotoTag(_service)
            {
                PhotoId = _SafeGetId(jsonTag, "pid"),
                ContactId = _SafeGetId(jsonTag, "subject"),
                Text = _SafeGetString(jsonTag, "text"),
                Offset = new System.Windows.Point(xcoord, ycoord),
            };

            return tag;
        }



        public List<string> DeserializeGrantedPermissionsList(string jsonString)
        {
            JSON_OBJECT permissions = SafeParseObject(jsonString);
            return (from pair in permissions where pair.Value.ToString() == "1" select pair.Key).ToList();
        }

        public List<ActivityFilter> DeserializeFilterList(string jsonString)
        {
            JSON_ARRAY jsonArray = SafeParseArray(jsonString);

            var filterList = new List<ActivityFilter>(from JSON_OBJECT filterObj in jsonArray select _DeserializeFilter(filterObj));
            return filterList;
        }

        private ActivityFilter _DeserializeFilter(JSON_OBJECT jsonFilter)
        {
            var filter = new ActivityFilter(_service)
            {
                // "uid" maps to the current user's UID.
                // "value" is a sometimes nil integer value.  Not sure what it's for.
                Key = _SafeGetId(jsonFilter, "filter_key"),
                Name = _SafeGetString(jsonFilter, "name"),
                Rank = _SafeGetInt32(jsonFilter, "rank") ?? Int32.MaxValue,
                // Facebook gives us an image map of both selected and not versions of the icon.
                // The right half is the selected state, so just return that as the image.
                // Update: Except they don't do it consistently.  It's not a matter of just being for hidden filters,
                // They just have the split images for 4 of them right now.  I have no idea what they're going to change it to in the future.
                // I'm going to predict that they'll stay square-ish, so I'm changing the FacebookImage to optionally check for the aspect ratio,
                // and then it can do the right thing if it looks too rectangular.
                Icon = new FacebookImage(
                    _service, 
                    _SafeGetUri(jsonFilter, "icon_url"),
                    true),
                IsVisible = _SafeGetBoolean(jsonFilter, "is_visible") ?? true,
                FilterType = _SafeGetString(jsonFilter, "type"),
            };

            return filter;
        }

        public List<ActivityPost> DeserializeStreamData(string jsonString)
        {
            JSON_OBJECT streamData = SafeParseObject(jsonString);

            return new List<ActivityPost>(from JSON_OBJECT jsonPost in (JSON_ARRAY)streamData["posts"] select _DeserializePost(jsonPost));
        }

        private ActivityPostAttachment _DeserializePhotoPostAttachmentData(ActivityPost post, JSON_OBJECT jsonAttachment)
        {
            if (jsonAttachment == null || jsonAttachment.Count == 0)
            {
                return null;
            }

            ActivityPostAttachment attachment = _DeserializeGenericPostAttachmentData(post, jsonAttachment);
            attachment.Type = ActivityPostAttachmentType.Photos;

            var photosEnum = from JSON_OBJECT jsonMediaObject in jsonAttachment["media"] as JSON_ARRAY
                             let photoElement = jsonMediaObject["photo"] as JSON_OBJECT
                             where photoElement != null
                             let link = _SafeGetUri(jsonMediaObject, "href")
                             select new FacebookPhoto(
                                 _service,
                                 _SafeGetId(photoElement, "aid"),
                                 _SafeGetId(photoElement, "pid"),
                                 _SafeGetUri(jsonMediaObject, "src"))
                                 {
                                     Link = link,
                                     OwnerId = _SafeGetId(photoElement, "owner"),
                                 };

            attachment.Photos = FacebookPhotoCollection.CreateStaticCollection(photosEnum);

            return attachment;
        }

        private ActivityPostAttachment _DeserializeVideoPostAttachmentData(ActivityPost post, JSON_OBJECT jsonAttachment)
        {
            if (jsonAttachment == null || jsonAttachment.Count == 0)
            {
                return null;
            }

            ActivityPostAttachment attachment = _DeserializeGenericPostAttachmentData(post, jsonAttachment);
            attachment.Type = ActivityPostAttachmentType.Video;

            var jsonMediaArray = jsonAttachment["media"] as JSON_ARRAY;
            if (jsonMediaArray != null && jsonMediaArray.Count > 0)
            {
                var jsonStreamMediaObject = jsonMediaArray[0] as JSON_OBJECT;
                Uri previewImageUri = _SafeGetUri(jsonStreamMediaObject, "src");

                attachment.VideoPreviewImage = new FacebookImage(_service, previewImageUri);
                attachment.VideoSource = _SafeGetUri(jsonStreamMediaObject, "href");
                // Not using this one because of a bug in Adobe's player when loading in an external browser...
                //XElement videoElement = streamElement.Element("video");
                //if (videoElement != null)
                //{
                //    attachment.VideoSource = _SafeGetUri(videoElement, "source_url");
                //}
            }

            return attachment;
        }

        private ActivityPostAttachment _DeserializeLinkPostAttachmentData(ActivityPost post, JSON_OBJECT jsonAttachment)
        {
            if (jsonAttachment == null || jsonAttachment.Count == 0)
            {
                return null;
            }

            ActivityPostAttachment attachment = _DeserializeGenericPostAttachmentData(post, jsonAttachment);
            attachment.Type = ActivityPostAttachmentType.Links;

            var linksEnum = from JSON_OBJECT jsonMediaObject in jsonAttachment.Get<JSON_ARRAY>("media")
                            let srcUri = _SafeGetUri(jsonMediaObject, "src")
                            let hrefUri = _SafeGetUri(jsonMediaObject, "href")
                            where srcUri != null && hrefUri != null
                            select new FacebookImageLink
                            {
                                Image = new FacebookImage(_service, srcUri),
                                Link = hrefUri,
                            };
            attachment.Links = new FacebookCollection<FacebookImageLink>(linksEnum);
            return attachment;
        }

        private ActivityPostAttachment _DeserializeGenericPostAttachmentData(ActivityPost post, JSON_OBJECT jsonAttachment)
        {
            Assert.IsNotNull(post);
            Uri iconUri = _SafeGetUri(jsonAttachment, "icon");

            return new ActivityPostAttachment(post)
            {
                Caption = _SafeGetString(jsonAttachment, "caption"),
                Link = _SafeGetUri(jsonAttachment, "href"),
                Name = _SafeGetString(jsonAttachment, "name"),
                Description = _SafeGetString(jsonAttachment, "description"),
                Properties = _SafeGetJson(jsonAttachment, "properties"),
                Icon = new FacebookImage(_service, iconUri),
            };
        }

        private ActivityPost _DeserializePost(JSON_OBJECT jsonPost)
        {
            var post = new ActivityPost(_service);

            JSON_OBJECT attachmentObject;
            if (jsonPost.TryGetTypedValue("attachment", out attachmentObject))
            {
                string postType = null;

                var jsonMediaArray = (JSON_ARRAY)_SafeGetValue(jsonPost, "attachment", "media");
                if (jsonMediaArray != null && jsonMediaArray.Count > 0)
                {
                    postType = _SafeGetString((JSON_OBJECT)jsonMediaArray[0], "type"); 
                }

                switch (postType)
                {
                    case "photo":
                        post.Attachment = _DeserializePhotoPostAttachmentData(post, attachmentObject);
                        break;
                    case "link":
                        post.Attachment = _DeserializeLinkPostAttachmentData(post, attachmentObject);
                        break;
                    case "video":
                        post.Attachment = _DeserializeVideoPostAttachmentData(post, attachmentObject);
                        break;

                    // We're not currently supporting music or flash.  Just treat it like a normal post...
                    case "music":
                    case "swf":

                    case "":
                    case null:
                        if (attachmentObject.Count != 0)
                        {
                            // We have attachment information but no rich stream-media associated with it.
                            ActivityPostAttachment attachment = _DeserializeGenericPostAttachmentData(post, attachmentObject);
                            if (!attachment.IsEmpty)
                            {
                                attachment.Type = ActivityPostAttachmentType.Simple;
                                post.Attachment = attachment;
                            }
                        }
                        break;
                    default:
                        Assert.Fail("Unknown type:" + postType);
                        break;
                }
            }

            post.PostId = _SafeGetId(jsonPost, "post_id");
            if (!FacebookObjectId.IsValid(post.PostId))
            {
                // Massive Facebook failure.
                // This happens too frequently for the assert to be useful.
                // Assert.Fail();
                post.PostId = SafeGetUniqueId();
            }
            post.ActorUserId = _SafeGetId(jsonPost, "actor_id");
            post.Created = _SafeGetDateTime(jsonPost, "created_time") ?? _UnixEpochTime;
            post.Message = _SafeGetString(jsonPost, "message");
            post.TargetUserId = _SafeGetId(jsonPost, "target_id");
            post.Updated = _SafeGetDateTime(jsonPost, "updated_time") ?? _UnixEpochTime;

            JSON_OBJECT likesElement;
            if (jsonPost.TryGetTypedValue("likes", out likesElement))
            {
                post.CanLike = _SafeGetBoolean(likesElement, "can_like") ?? false;
                post.HasLiked = _SafeGetBoolean(likesElement, "user_likes") ?? false;
                post.LikedCount = _SafeGetInt32(likesElement, "count") ?? 0;
                post.LikeUri = _SafeGetUri(likesElement, "likes", "href");
                //XElement friendsElement = likesElement.Element("friends");
                //XElement sampleElement = likesElement.Element("sample");
                //post.SetPeopleWhoLikeThisIds(
                //    Enumerable.Union(
                //        sampleElement == null
                //            ? new FacebookObjectId[0]
                //            : from uidElement in sampleElement.Elements("uid") select new FacebookObjectId(uidElement.Value),
                //        friendsElement == null
                //            ? new FacebookObjectId[0]
                //            : from uidElement in friendsElement.Elements("uid") select new FacebookObjectId(uidElement.Value)));
            }

            JSON_OBJECT jsonComments;
            jsonPost.TryGetTypedValue("comments", out jsonComments);

            post.CanComment = _SafeGetBoolean(jsonComments, "can_post") ?? false;
            post.CanRemoveComments = _SafeGetBoolean(jsonComments, "can_remove") ?? false;
            post.CommentCount = _SafeGetInt32(jsonComments, "count") ?? 0;

            if (jsonComments != null && post.CommentCount != 0)
            {
                JSON_ARRAY jsonCommentList;
                if (jsonComments.TryGetTypedValue("comment_list", out jsonCommentList))
                {
                    var commentNodes = from JSON_OBJECT jsonComment in jsonCommentList
                                       let comment = _DeserializeComment(jsonComment)
                                       where (comment.Post = post) != null
                                       select comment;

                    post.RawComments = new FBMergeableCollection<ActivityComment>(commentNodes);
                }
            }

            if (post.RawComments == null)
            {
                post.RawComments = new FBMergeableCollection<ActivityComment>();
            }

            // post.Comments = null;

            return post;
        }

        public List<FacebookContact> DeserializeProfileList(string jsonInput)
        {
            JSON_ARRAY profileList = SafeParseArray(jsonInput);
            return new List<FacebookContact>(from JSON_OBJECT profile in profileList select _DeserializeProfile(profile));
        }

        private FacebookContact _DeserializeProfile(JSON_OBJECT jsonProfile)
        {
            Uri sourceUri = _SafeGetUri(jsonProfile, "pic");
            Uri sourceBigUri = _SafeGetUri(jsonProfile, "pic_big");
            Uri sourceSmallUri = _SafeGetUri(jsonProfile, "pic_small");
            Uri sourceSquareUri = _SafeGetUri(jsonProfile, "pic_square");

            var profile = new FacebookContact(_service)
            {
                UserId = _SafeGetId(jsonProfile, "id"),
                Name = _SafeGetString(jsonProfile, "name"),
                Image = new FacebookImage(_service, sourceUri, sourceBigUri, sourceSmallUri, sourceSquareUri),
                ProfileUri = _SafeGetUri(jsonProfile, "url"),
                // ContactType = "type" => "user" | "page"
            };

            return profile;
        }

        public FacebookPhotoAlbum DeserializeUploadAlbumResponse(string jsonString)
        {
            // photos.Upload returns photo data embedded in the root node.
            JSON_OBJECT jsonAlbum = SafeParseObject(jsonString);
            FacebookPhotoAlbum album = _DeserializeAlbum(jsonAlbum);
            return album;
        }

        public List<FacebookPhotoAlbum> DeserializeGetAlbumsResponse(string jsonString)
        {
            JSON_ARRAY jsonAlbumsArray = SafeParseArray(jsonString);
            return new List<FacebookPhotoAlbum>(from JSON_OBJECT jsonAlbum in jsonAlbumsArray select _DeserializeAlbum(jsonAlbum));
        }

        private FacebookPhotoAlbum _DeserializeAlbum(JSON_OBJECT jsonAlbum)
        {
            Uri linkUri = _SafeGetUri(jsonAlbum, "link");

            var album = new FacebookPhotoAlbum(_service)
            {
                AlbumId = _SafeGetId(jsonAlbum, "aid"),
                CoverPicPid = _SafeGetId(jsonAlbum, "cover_pid"),
                OwnerId = _SafeGetId(jsonAlbum, "owner"),
                Title = _SafeGetString(jsonAlbum, "name"),
                Created = _SafeGetDateTime(jsonAlbum, "created") ?? _UnixEpochTime,
                LastModified = _SafeGetDateTime(jsonAlbum, "modified") ?? _UnixEpochTime,
                Description = _SafeGetString(jsonAlbum, "description"),
                Location = _SafeGetString(jsonAlbum, "location"),
                Link = linkUri,
                // Size = _SafeGetInt32(elt, "size"),
                // Visible = _SafeGetValue(elt, "visible"),
            };

            return album;
        }

        public FacebookPhoto DeserializePhotoUploadResponse(string jsonInput)
        {
            JSON_OBJECT jsonPhoto = SafeParseObject(jsonInput);
            return _DeserializePhoto(jsonPhoto);
        }

        public List<ActivityPost> DeserializeStreamPostList(string jsonInput)
        {
            JSON_ARRAY jsonPosts = SafeParseArray(jsonInput);
            return new List<ActivityPost>(from JSON_OBJECT jsonPost in jsonPosts select _DeserializePost(jsonPost));
        }

        public List<ActivityComment> DeserializeCommentsDataList(ActivityPost post, string jsonInput)
        {
            JSON_ARRAY jsonComments = SafeParseArray(jsonInput);
            return new List<ActivityComment>(from JSON_OBJECT jsonComment in jsonComments select _DeserializeComment(jsonComment));
        }

#if UNUSED_JSON_CALLS




        public static void DeserializeSessionInfo(string xml, out string sessionKey, out FacebookObjectId userId)
        {
            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            sessionKey = _SafeGetValue(xdoc.Root, "session_key");
            userId = _SafeGetId(xdoc.Root, "uid");
        }

        public List<FacebookPhoto> DeserializePhotosGetResponse(string xml)
        {
            var photoList = new List<FacebookPhoto>();

            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            return DeserializePhotosGetResponse((XElement)xdoc.FirstNode, ns);
        }

#endif

        public static Exception DeserializeFacebookException(string jsonInput, string request)
        {
            // Do a sanity check on the opening XML tags to see if it looks like an exception.
            if (jsonInput.Substring(0, Math.Min(jsonInput.Length, 200)).Contains("error_code"))
            {
                JSON_OBJECT errorObject = SafeParseObject(jsonInput);
                if (errorObject.ContainsKey("error_code"))
                {
                    return new FacebookException(
                        jsonInput,
                        _SafeGetInt32(errorObject, "error_code") ?? 0,
                        _SafeGetString(errorObject, "error_msg"),
                        request);
                }
            }

            return null;
        }

        public void DeserializeNotificationsGetResponse(string jsonString, out List<Notification> friendRequests, out int unreadMessageCount)
        {
            JSON_OBJECT jsonNotification = SafeParseObject(jsonString);
            var notificationList = new List<Notification>(from string id in jsonNotification.Get<JSON_ARRAY>("friend_requests") 
                                                          let uid = new FacebookObjectId(id)
                                                          where FacebookObjectId.IsValid(uid)
                                                          select new FriendRequestNotification(_service, uid));
            unreadMessageCount = _SafeGetInt32(jsonNotification, "messages", "unread") ?? 0;
            friendRequests = notificationList;
        }

        public List<Notification> DeserializeNotificationsListResponse(string jsonString)
        {
            JSON_OBJECT jsonNotificationsObject = SafeParseObject(jsonString);
            JSON_ARRAY jsonNotificationsList = jsonNotificationsObject.Get<JSON_ARRAY>("notifications");
            JSON_ARRAY jsonApplicationsList = jsonNotificationsObject.Get<JSON_ARRAY>("apps");
            var ret = new List<Notification>(from JSON_OBJECT jsonNotification in jsonNotificationsList 
                                             from JSON_OBJECT jsonApplication in jsonApplicationsList
                                             where jsonApplication.Get<string>("app_id") == jsonNotification.Get<string>("app_id")
                                             select _DeserializeNotification(jsonNotification, jsonApplication));
            Assert.AreEqual(jsonNotificationsList.Count, ret.Count);
            return ret;
        }

        private Notification _DeserializeNotification(JSON_OBJECT jsonNotification, JSON_OBJECT jsonApplication)
        {
            Uri appIconUri = _SafeGetUri(jsonApplication, "icon_url");

            // To make these consistent with the rest of Facebook's HTML, enclose these in div tags if they're present.
            string bodyHtml = _SafeGetString(jsonNotification, "body_html");
            if (!string.IsNullOrEmpty(bodyHtml))
            {
                bodyHtml = "<div>" + bodyHtml + "</div>";
            }

            string titleHtml = _SafeGetString(jsonNotification, "title_html");
            if (!string.IsNullOrEmpty(titleHtml))
            {
                titleHtml = "<div>" + titleHtml + "</div>";
            }

            var notification = new Notification(_service)
            {
                Created = _SafeGetDateTime(jsonNotification, "created_time") ?? _UnixEpochTime,
                Description = bodyHtml,
                DescriptionText = _SafeGetString(jsonNotification, "body_text"),
                IsHidden = _SafeGetBoolean(jsonNotification, "is_hidden") ?? false,
                IsUnread = _SafeGetBoolean(jsonNotification, "is_unread") ?? false,
                Link = _SafeGetUri(jsonNotification, "href"),
                NotificationId = _SafeGetId(jsonNotification, "notification_id"),
                RecipientId = _SafeGetId(jsonNotification, "recipient_id"),
                SenderId = _SafeGetId(jsonNotification, "sender_id"),
                Title = titleHtml,
                TitleText = _SafeGetString(jsonNotification, "title_text"),
                Updated = _SafeGetDateTime(jsonNotification, "updated_time") ?? _UnixEpochTime,
                Icon = new FacebookImage(_service, appIconUri),
            };

            return notification;
        }

        public bool DeserializePhotoCanCommentResponse(string jsonString)
        {
            // Not really a JSON string...
            return "true".Equals(jsonString, StringComparison.OrdinalIgnoreCase);
        }

        public FacebookObjectId DeserializePhotoAddCommentResponse(string jsonString)
        {
            // Not really a JSON string.... Returns the comment id.
            return new FacebookObjectId(jsonString);
        }

        public List<ActivityComment> DeserializePhotoCommentsResponse(string jsonInput)
        {
            JSON_ARRAY jsonComments = SafeParseArray(jsonInput);
            return new List<ActivityComment>(from JSON_OBJECT jsonComment in jsonComments select _DeserializePhotoComment(jsonComment));
        }

        private ActivityComment _DeserializePhotoComment(JSON_OBJECT jsonComment)
        {
            var comment = new ActivityComment(_service)
            {
                CommentType = ActivityComment.Type.Photo,
                CommentId = _SafeGetId(jsonComment, "pcid"),
                FromUserId = _SafeGetId(jsonComment, "from"),
                Time = _SafeGetDateTime(jsonComment, "time") ?? _UnixEpochTime,
                Text = _SafeGetString(jsonComment, "body"),
            };
            return comment;
        }

        public FacebookObjectId DeserializeAddCommentResponse(string jsonInput)
        {
            return new FacebookObjectId(jsonInput);
        }

        public bool DeserializeStatusSetResponse(string jsonInput)
        {
            return jsonInput == "true";
        }
    }
}
