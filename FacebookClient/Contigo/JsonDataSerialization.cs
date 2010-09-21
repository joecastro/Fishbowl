
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
                return true;
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
        /// <summary>The start time for Unix based clocks.  Facebook usually returns their timestamps based on ticks from this value.</summary>
        private static readonly DateTime _UnixEpochTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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
                if (!jsonObj.TryGetValue(name, out lastChild))
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

            return DataSerialization.GetDateTimeFromUnixTimestamp(ticks.Value);
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

        private static JSON_OBJECT _SafeParseObject(string jsonString)
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

        private static JSON_ARRAY _SafeParseArray(string jsonString)
        {
            var obj = _serializer.Deserialize(jsonString);
            Assert.IsTrue(obj is JSON_ARRAY);
            return obj as JSON_ARRAY;
        }

        private ActivityComment _DeserializeCommentData(JSON_OBJECT obj)
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

#if UNUSED_JSON_CALLS

        private FacebookContact _DeserializeUser(JSON_OBJECT obj)
        {
            Uri sourceUri = _SafeGetUri(obj, "pic");
            Uri sourceBigUri = _SafeGetUri(obj, "pic_big");
            Uri sourceSmallUri = _SafeGetUri(obj, "pic_small");
            Uri sourceSquareUri = _SafeGetUri(obj, "pic_square");

            Location currentLocation = null;
            Location hometownLocation = null;
            HighSchoolInfo hsInfo = null;
            List<EducationInfo> educationHistory = null;
            List<WorkInfo> workHistory = null;

            var clObject = obj["current_location"] as JSON_OBJECT;
            if (clObject != null)
            {
                currentLocation = _DeserializeLocation(clObject);
            }

            var htObject = obj["hometown_location"] as JSON_OBJECT;
            if (htObject != null)
            {
                hometownLocation = _DeserializeLocation(htObject);
            }

            var hsObject = obj["hs_info"] as JSON_OBJECT;
            if (hsObject != null)
            {
                hsInfo = new HighSchoolInfo
                {
                    GraduationYear = _SafeGetInt32(hsObject, "grad_year"),
                    //Id = _SafeGetValue(hsElement, "hs1_id") ?? "0",
                    //Id2 = _SafeGetValue(hsElement, "hs2_id") ?? "0",
                    Name = _SafeGetValue(hsObject, "hs1_name"),
                    Name2 = _SafeGetValue(hsObject, "hs2_name"),
                };
            }

            var ehObject = obj["education_history"] as JSON_OBJECT;
            if (ehObject != null)
            {
                educationHistory = new List<EducationInfo>(
                    from infoNode in ehObject["education_info"] as JSON_OBJECT
                    select _DeserializeEducationInfo(infoNode));
            }
            else
            {
                educationHistory = new List<EducationInfo>();
            }

            XElement whElement = obj.Element("work_history");
            if (whElement != null)
            {
                workHistory = new List<WorkInfo>(
                    from wiNode in whElement.Elements("work_info")
                    select _DeserializeWorkInfo(ns, wiNode));
            }

            var contact = new FacebookContact(_service)
            {
                Name = _SafeGetValue(obj, "name"),
                FirstName = _SafeGetValue(obj, "first_name"),
                LastName = _SafeGetValue(obj, "last_name"),

                AboutMe = _SafeGetValue(obj, "about_me"),
                Activities = _SafeGetValue(obj, "activities"),
                // Affilitions =
                // AllowedRestrictions = 
                Birthday = _SafeGetValue(obj, "birthday"),
                MachineSafeBirthday = _SafeGetValue(obj, "birthday_date"),
                Books = _SafeGetValue(obj, "books"),
                CurrentLocation = currentLocation,
                EducationHistory = educationHistory.AsReadOnly(),
                Hometown = hometownLocation,
                HighSchoolInfo = hsInfo,
                Interests = _SafeGetValue(obj, "interests"),
                Image = new FacebookImage(_service, sourceUri, sourceBigUri, sourceSmallUri, sourceSquareUri),
                Movies = _SafeGetValue(obj, "movies"),
                Music = _SafeGetValue(obj, "music"),
                Quotes = _SafeGetValue(obj, "quotes"),
                RelationshipStatus = _SafeGetValue(obj, "relationship_status"),
                Religion = _SafeGetValue(obj, "religion"),
                Sex = _SafeGetValue(obj, "sex"),
                TV = _SafeGetValue(obj, "tv"),
                Website = _SafeGetValue(obj, "website"),
                ProfileUri = _SafeGetUri(obj, "profile_url"),
                UserId = _SafeGetId(obj, "uid"),
                ProfileUpdateTime = _SafeGetDateTime(obj, "profile_update_time") ?? _UnixEpochTime,
                OnlinePresence = _DeserializePresenceNode(ns, obj),
            };

            if (!string.IsNullOrEmpty(_SafeGetValue(obj, "status", "message")))
            {
                contact.StatusMessage = new ActivityPost(_service)
                {
                    PostId = new FacebookObjectId("status_" + contact.UserId.ToString()),
                    ActorUserId = _SafeGetId(obj, "uid"),
                    Created = _SafeGetDateTime(obj, "status", "time") ?? _UnixEpochTime,
                    Updated = _SafeGetDateTime(obj, "status", "time") ?? _UnixEpochTime,
                    Message = _SafeGetValue(obj, "status", "message"),
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

        private FacebookContact _DeserializePage(XElement elt)
        {
            Uri sourceUri = _SafeGetUri(elt, "pic");
            Uri sourceBigUri = _SafeGetUri(elt, "pic_big");
            Uri sourceSmallUri = _SafeGetUri(elt, "pic_small");
            Uri sourceSquareUri = _SafeGetUri(elt, "pic_square");
            // No idea why there are both large and big...
            Uri sourceLargeUri = _SafeGetUri(elt, "pic_large");

            // This is a light weight view of a page as a FacebookContact.
            // If the FacebookService were to expose pages as a first-class concept then there would be a dedicated class,
            // and probably a base class.
            var page = new FacebookContact(_service)
            {
                UserId = _SafeGetId(elt, "page_id"),
                Name = _SafeGetValue(elt, "name"),
                ProfileUri = _SafeGetUri(elt, "page_url"),
                Image = new FacebookImage(_service, sourceUri, sourceBigUri ?? sourceLargeUri, sourceSmallUri, sourceSquareUri),
            };

            return page;
        }

        private static OnlinePresence _DeserializePresenceNode(JSON_OBJECT obj)
        {
            string presence = _SafeGetValue(obj, "online_presence");
            switch (presence)
            {
                case "active": return OnlinePresence.Active;
                case "idle": return OnlinePresence.Idle;
                case "offline": return OnlinePresence.Offline;
                case "error": return OnlinePresence.Unknown;
            }
            return OnlinePresence.Unknown;
        }

        private WorkInfo _DeserializeWorkInfo(XElement elt)
        {
            Assert.IsNotNull(elt);
            Assert.IsNotNull(ns);

            return new WorkInfo
            {
                CompanyName = _SafeGetValue(elt, "company_name"),
                Description = _SafeGetValue(elt, "description"),
                EndDate = _SafeGetValue(elt, "end_date"),
                StartDate = _SafeGetValue(elt, "start_date"),
                Location = _DeserializeLocation(ns, elt.Element("location")),
            };
        }

        private static Location _DeserializeLocation(XElement elt)
        {
            if (elt == null)
            {
                return null;
            }

            return new Location
            {
                // current_location: city, state, country (well defined), zip (may be zero)
                City = _SafeGetValue(elt, "city"),
                Country = _SafeGetValue(elt, "country"),
                State = _SafeGetValue(elt, "state"),
                ZipCode = _SafeGetInt32(elt, "zip")
            };
        }

        private EducationInfo _DeserializeEducationInfo(XElement infoNode)
        {
            int? maybeYear = _SafeGetInt32(infoNode, "year");
            if (maybeYear == 0)
            {
                maybeYear = null;
            }

            var concentrationBuilder = new StringBuilder();
            bool first = true;
            foreach (string conString in from c in infoNode.Elements("concentrations") select c.Value)
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

            return new EducationInfo
            {
                Concentrations = concentrationBuilder.ToString(),
                Degree = _SafeGetValue(infoNode, "degree"),
                Name = _SafeGetValue(infoNode, "name"),
                Year = maybeYear,
            };
        }

        private FacebookPhotoTag _DeserializePhotoTag(XNamespace ns, XElement elt)
        {
            float xcoord = (_SafeGetSingle(elt, "xcoord") ?? 0) / 100;
            float ycoord = (_SafeGetSingle(elt, "ycoord") ?? 0) / 100;

            xcoord = Math.Max(Math.Min(1, xcoord), 0);
            ycoord = Math.Max(Math.Min(1, ycoord), 0);

            var tag = new FacebookPhotoTag(_service)
            {
                PhotoId = _SafeGetId(elt, "pid"),
                ContactId = _SafeGetId(elt, "subject"),
                Text = _SafeGetValue(elt, "text"),
                Offset = new System.Windows.Point(xcoord, ycoord),
            };

            return tag;
        }

        private ActivityPostAttachment _DeserializePhotoPostAttachmentData(ActivityPost post, XNamespace ns, XElement elt)
        {
            if (elt == null)
            {
                return null;
            }

            ActivityPostAttachment attachment = _DeserializeGenericPostAttachmentData(post, ns, elt);
            attachment.Type = ActivityPostAttachmentType.Photos;

            var photosEnum = from smElement in elt.Element("media").Elements("stream_media")
                             let photoElement = smElement.Element("photo")
                             where photoElement != null
                             let link = _SafeGetUri(smElement, "href")
                             select new FacebookPhoto(
                                 _service,
                                 _SafeGetId(photoElement, "aid"),
                                 _SafeGetId(photoElement, "pid"),
                                 _SafeGetUri(smElement, "src"))
                             {
                                 Link = link,
                                 OwnerId = _SafeGetId(photoElement, "owner"),
                             };
            attachment.Photos = FacebookPhotoCollection.CreateStaticCollection(photosEnum);

            return attachment;
        }

        private ActivityPostAttachment _DeserializeVideoPostAttachmentData(ActivityPost post, XNamespace ns, XElement elt)
        {
            if (elt == null)
            {
                return null;
            }

            ActivityPostAttachment attachment = _DeserializeGenericPostAttachmentData(post, ns, elt);
            Uri previewImage = _SafeGetUri(elt, "media", "preview_img");

            attachment.Type = ActivityPostAttachmentType.Video;
            XElement mediaElement = elt.Element("media");
            if (mediaElement != null)
            {
                XElement streamElement = mediaElement.Element("stream_media");
                if (streamElement != null)
                {
                    Uri previewImageUri = _SafeGetUri(streamElement, "src");

                    attachment.VideoPreviewImage = new FacebookImage(_service, previewImageUri);
                    attachment.VideoSource = _SafeGetUri(streamElement, "href");
                    // Not using this one because of a bug in Adobe's player when loading in an external browser...
                    //XElement videoElement = streamElement.Element("video");
                    //if (videoElement != null)
                    //{
                    //    attachment.VideoSource = _SafeGetUri(videoElement, "source_url");
                    //}
                }
            }

            return attachment;
        }

        private ActivityPostAttachment _DeserializeLinkPostAttachmentData(ActivityPost post, XNamespace ns, XElement elt)
        {
            if (elt == null)
            {
                return null;
            }

            ActivityPostAttachment attachment = _DeserializeGenericPostAttachmentData(post, ns, elt);
            attachment.Type = ActivityPostAttachmentType.Links;

            var linksEnum = from smElement in elt.Element("media").Elements("stream_media")
                            let srcUri = _SafeGetUri(smElement, "src")
                            let hrefUri = _SafeGetUri(smElement, "href")
                            where srcUri != null && hrefUri != null
                            select new FacebookImageLink()
                            {
                                Image = new FacebookImage(_service, srcUri),
                                Link = hrefUri,
                            };
            attachment.Links = new FacebookCollection<FacebookImageLink>(linksEnum);
            return attachment;
        }

        private ActivityPostAttachment _DeserializeGenericPostAttachmentData(ActivityPost post, XNamespace ns, XElement elt)
        {
            Assert.IsNotNull(post);
            Uri iconUri = _SafeGetUri(elt, "icon");

            return new ActivityPostAttachment(post)
            {
                Caption = _SafeGetValue(elt, "caption"),
                Link = _SafeGetUri(elt, "href"),
                Name = _SafeGetValue(elt, "name"),
                Description = _SafeGetValue(elt, "description"),
                Properties = _SafeGetJson(elt, "properties"),
                Icon = new FacebookImage(_service, iconUri),
            };
        }

        public List<FacebookContact> DeserializePagesList(string xml)
        {
            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            var userNodes = from XElement elt in ((XElement)xdoc.FirstNode).Elements("page")
                            select _DeserializePage(ns, elt);
            return new List<FacebookContact>(userNodes);
        }

        public List<FacebookContact> DeserializeUsersList(string xml)
        {
            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            var userNodes = from XElement elt in ((XElement)xdoc.FirstNode).Elements("user")
                            select _DeserializeUser(ns, elt);
            return new List<FacebookContact>(userNodes);
        }

        public List<FacebookContact> DeserializeUsersListWithProfiles(XNamespace ns, XElement usersElement, XElement profilesElement)
        {
            var contacts = usersElement.Elements("user").Zip(
                profilesElement.Elements("profile"),
                (userElt, profileElt) =>
                {
                    Assert.AreEqual(
                        _SafeGetValue(userElt, "uid"),
                        _SafeGetValue(profileElt, "id"));

                    var c = _DeserializeUser(ns, userElt);
                    c.MergeImage(_DeserializeProfile(ns, profileElt).Image);
                    return c;
                });
            return new List<FacebookContact>(contacts);
        }

        public Dictionary<FacebookObjectId, OnlinePresence> DeserializeUserPresenceList(string xml)
        {
            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            var items = from userNode in xdoc.Root.Elements("user")
                        let uid = _SafeGetId(userNode, "uid")
                        let presence = _DeserializePresenceNode(ns, userNode)
                        select new { UserId = uid, Presence = presence };
            return items.ToDictionary(item => item.UserId, item => item.Presence);
        }

        public List<FacebookPhotoTag> DeserializePhotoTagsList(XElement root, XNamespace ns)
        {
            var tagList = new List<FacebookPhotoTag>();

            var tagNodes = from XElement elt in root.Elements("photo_tag")
                           let tag = _DeserializePhotoTag(ns, elt)
                           where tag != null
                           select tag;
            tagList.AddRange(tagNodes);
            return tagList;
        }

        public List<FacebookPhotoTag> DeserializePhotoTagsList(string xml)
        {
            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            return DeserializePhotoTagsList((XElement)xdoc.FirstNode, ns);
        }

#endif
        public List<string> DeserializeGrantedPermissionsList(string jsonString)
        {
            JSON_OBJECT permissions = _SafeParseObject(jsonString);
            return (from pair in permissions where pair.Value.ToString() == "1" select pair.Key).ToList();
        }

        public List<ActivityFilter> DeserializeFilterList(string jsonString)
        {
            JSON_ARRAY jsonArray = _SafeParseArray(jsonString);

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
                Icon = new FacebookImage(
                    _service, 
                    _SafeGetUri(jsonFilter, "icon_url"),
                    new Thickness(.5, 0, 0, 0)),
                IsVisible = _SafeGetBoolean(jsonFilter, "is_visible") ?? true,
                FilterType = _SafeGetString(jsonFilter, "type"),
            };

            return filter;
        }

        public void DeserializeStreamData(string jsonString, out List<ActivityPost> posts, out List<FacebookContact> userData)
        {
            JSON_OBJECT streamData = _SafeParseObject(jsonString);

            posts = new List<ActivityPost>(from JSON_OBJECT jsonPost in (JSON_ARRAY)streamData["posts"] select _DeserializePost(jsonPost));
            userData = new List<FacebookContact>(from JSON_OBJECT profile in (JSON_ARRAY)streamData["profiles"] select _DeserializeProfile(profile));
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
                post.PostId = DataSerialization.SafeGetUniqueId();
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
                                       let comment = _DeserializeCommentData(jsonComment)
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
            JSON_ARRAY profileList = _SafeParseArray(jsonInput);
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

#if UNUSED_JSON_CALLS

        public List<ActivityPost> DeserializePostDataList(string xml, bool isFQL)
        {

            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            XContainer rootList = null;
            if (isFQL)
            {
                rootList = ((XElement)xdoc.FirstNode);
            }
            else
            {
                rootList = ((XElement)xdoc.FirstNode).Element("posts");
            }

            return _DeserializePostDataList(ns, rootList);
        }


        public List<ActivityComment> DeserializeCommentsDataList(ActivityPost post, string xml)
        {
            var commentList = new List<ActivityComment>();

            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            var commentNodes = from XElement elt in ((XElement)xdoc.FirstNode).Elements("comment")
                               let comment = _DeserializeCommentData(ns, elt)
                               where (comment.Post = post) != null
                               select comment;
            commentList.AddRange(commentNodes);
            return commentList;
        }

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

        public List<FacebookPhoto> DeserializePhotosGetResponse(XElement root, XNamespace ns)
        {
            var photoList = new List<FacebookPhoto>();

            var photoNodes = from XElement elt in root.Elements("photo")
                             select _DeserializePhotoData(ns, elt);
            photoList.AddRange(photoNodes);
            return photoList;
        }

        private FacebookPhoto _DeserializePhotoData(XNamespace ns, XElement elt)
        {
            Uri linkUri = _SafeGetUri(elt, "link");
            Uri sourceUri = _SafeGetUri(elt, "src");
            Uri smallUri = _SafeGetUri(elt, "src_small");
            Uri bigUri = _SafeGetUri(elt, "src_big");

            var photo = new FacebookPhoto(_service)
            {
                PhotoId = _SafeGetId(elt, "pid"),
                AlbumId = _SafeGetId(elt, "aid"),
                OwnerId = _SafeGetId(elt, "owner"),
                Caption = _SafeGetValue(elt, "caption"),
                Created = _SafeGetDateTime(elt, "created") ?? _UnixEpochTime,
                Image = new FacebookImage(_service, sourceUri, bigUri, smallUri, null),
                Link = linkUri,
            };

            return photo;
        }

        public FacebookPhoto DeserializePhotoUploadResponse(string xml)
        {
            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            // photos.Upload returns photo data embedded in the root node.
            return _DeserializePhotoData(ns, xdoc.Root);
        }

        public FacebookPhotoAlbum DeserializeUploadAlbumResponse(string xml)
        {
            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            // photos.Upload returns photo data embedded in the root node.
            FacebookPhotoAlbum album = _DeserializeAlbumData(ns, xdoc.Root);
            return album;
        }

        public List<FacebookPhotoAlbum> DeserializeGetAlbumsResponse(string xml)
        {
            var albumList = new List<FacebookPhotoAlbum>();

            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            var albumNodes = from XElement elt in ((XElement)xdoc.FirstNode).Elements("album")
                             select _DeserializeAlbumData(ns, elt);
            albumList.AddRange(albumNodes);
            return albumList;
        }

        private FacebookPhotoAlbum _DeserializeAlbumData(XNamespace ns, XElement elt)
        {
            Uri linkUri = _SafeGetUri(elt, "link");

            var album = new FacebookPhotoAlbum(_service)
            {
                AlbumId = _SafeGetId(elt, "aid"),
                CoverPicPid = _SafeGetId(elt, "cover_pid"),
                OwnerId = _SafeGetId(elt, "owner"),
                Title = _SafeGetValue(elt, "name"),
                Created = _SafeGetDateTime(elt, "created") ?? _UnixEpochTime,
                LastModified = _SafeGetDateTime(elt, "modified") ?? _UnixEpochTime,
                Description = _SafeGetValue(elt, "description"),
                Location = _SafeGetValue(elt, "location"),
                Link = linkUri,
                // Size = _SafeGetInt32(elt, "size"),
                // Visible = _SafeGetValue(elt, "visible"),
            };

            return album;
        }

#endif
        public static Exception DeserializeFacebookException(string jsonInput, string request)
        {
            // Do a sanity check on the opening XML tags to see if it looks like an exception.
            if (jsonInput.Substring(0, Math.Min(jsonInput.Length, 200)).Contains("error_code"))
            {
                JSON_OBJECT errorObject = _SafeParseObject(jsonInput);
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

#if UNUSED_JSON_CALLS

        public void DeserializeNotificationsGetResponse(string xml, out List<Notification> friendRequests, out int unreadMessageCount)
        {
            var notificationList = new List<Notification>();

            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            // Get friend requests
            notificationList.AddRange(
                from XElement elt in ((XElement)xdoc.FirstNode).Element("friend_requests").Elements("uid")
                let uid = _SafeGetId(elt)
                where FacebookObjectId.IsValid(uid)
                select (Notification)new FriendRequestNotification(_service, uid));

            unreadMessageCount = _SafeGetInt32((XElement)xdoc.FirstNode, "messages", "unread") ?? 0;
            friendRequests = notificationList;
        }

        public List<Notification> DeserializeNotificationsListResponse(string xml)
        {
            var notificationList = new List<Notification>();

            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            // I should also be able to use the "apps" list to get the icons to display next to the notification.
            var notificationNodes = from XElement elt in ((XElement)xdoc.FirstNode).Element("notifications").Elements("notification")
                                    select _DeserializeNotificationData(ns, elt);
            notificationList.AddRange(notificationNodes);
            return notificationList;
        }

        private Notification _DeserializeNotificationData(XNamespace ns, XElement elt)
        {
            // To make these consistent with the rest of Facebook's HTML, enclose these in div tags if they're present.
            string bodyHtml = _SafeGetValue(elt, "body_html");
            if (!string.IsNullOrEmpty(bodyHtml))
            {
                bodyHtml = "<div>" + bodyHtml + "</div>";
            }

            string titleHtml = _SafeGetValue(elt, "title_html");
            if (!string.IsNullOrEmpty(titleHtml))
            {
                titleHtml = "<div>" + titleHtml + "</div>";
            }

            var notification = new Notification(_service)
            {
                Created = _SafeGetDateTime(elt, "created_time") ?? _UnixEpochTime,
                Description = bodyHtml,
                DescriptionText = _SafeGetValue(elt, "body_text"),
                IsHidden = _SafeGetValue(elt, "is_hidden") == "1",
                IsUnread = _SafeGetValue(elt, "is_unread") == "1",
                Link = _SafeGetUri(elt, "href"),
                NotificationId = _SafeGetId(elt, "notification_id"),
                RecipientId = _SafeGetId(elt, "recipient_id"),
                SenderId = _SafeGetId(elt, "sender_id"),
                Title = titleHtml,
                TitleText = _SafeGetValue(elt, "title_text"),
                Updated = _SafeGetDateTime(elt, "updated_time") ?? _UnixEpochTime,
            };

            return notification;
        }

        public FacebookObjectId DeserializeAddCommentResponse(string xml)
        {
            XDocument xdoc = _SafeParseObject(xml);
            return new FacebookObjectId(xdoc.Root.Value);
        }

        public List<ActivityComment> DeserializePhotoCommentsResponse(string xml)
        {
            var commentList = new List<ActivityComment>();

            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            var commentNodes = from XElement elt in ((XElement)xdoc.FirstNode).Elements("photo_comment")
                               select _DeserializePhotoCommentData(ns, elt);
            commentList.AddRange(commentNodes);
            return commentList;
        }

        private ActivityComment _DeserializePhotoCommentData(XNamespace ns, XElement elt)
        {
            var comment = new ActivityComment(_service)
            {
                CommentType = ActivityComment.Type.Photo,
                CommentId = _SafeGetId(elt, "pcid"),
                FromUserId = _SafeGetId(elt, "from"),
                Time = _SafeGetDateTime(elt, "time") ?? _UnixEpochTime,
                Text = _SafeGetValue(elt, "body"),
            };
            return comment;
        }

        public bool DeserializePhotoCanCommentResponse(string xml)
        {
            XDocument xdoc = _SafeParseObject(xml);
            return xdoc.Root.Value == "1";
        }

        public FacebookObjectId DeserializePhotoAddCommentResponse(string xml)
        {
            XDocument xdoc = _SafeParseObject(xml);
            return new FacebookObjectId(xdoc.Root.Value);
        }

        public string DeserializeStreamPublishResponse(string xml)
        {
            XDocument xdoc = _SafeParseObject(xml);
            return xdoc.Root.Value;
        }

        public bool DeserializeStatusSetResponse(string xml)
        {
            XDocument xdoc = _SafeParseObject(xml);
            return xdoc.Root.Value == "1";
        }

        public List<MessageNotification> DeserializeMessageQueryResponse(string xml)
        {
            var messageList = new List<MessageNotification>();

            XDocument xdoc = _SafeParseObject(xml);
            XNamespace ns = xdoc.Root.GetDefaultNamespace();

            var messageNodes = from XElement elt in ((XElement)xdoc.FirstNode).Elements("thread")
                               select _DeserializeMessageNotificationData(ns, elt);
            messageList.AddRange(messageNodes);
            return messageList;
        }

        private MessageNotification _DeserializeMessageNotificationData(XNamespace ns, XElement elt)
        {
            var message = new MessageNotification(_service)
            {
                Created = _SafeGetDateTime(elt, "updated_time") ?? _UnixEpochTime,
                IsUnread = _SafeGetValue(elt, "unread") == "1",
                DescriptionText = _SafeGetValue(elt, "snippet"),
                IsHidden = false,
                NotificationId = _SafeGetId(elt, "thread_id"),
                // TODO: This is actually a list of recipients.
                RecipientId = _SafeGetId(elt, "recipients", "uid"),
                SenderId = _SafeGetId(elt, "snippet_author"),
                Title = _SafeGetValue(elt, "subject"),
                Updated = _SafeGetDateTime(elt, "updated_time") ?? DateTime.Now,
            };

            if (FacebookObjectId.IsValid(message.NotificationId))
            {
                message.Link = new Uri(string.Format("http://www.facebook.com/inbox/#/inbox/?folder=[fb]messages&page=1&tid={0}", message.NotificationId));
            }
            else
            {
                Assert.Fail();
                message.NotificationId = DataSerialization.SafeGetUniqueId();
                message.Link = new Uri("http://www.facebook.com/inbox");
            }

            return message;
        }
#endif
    }
}
