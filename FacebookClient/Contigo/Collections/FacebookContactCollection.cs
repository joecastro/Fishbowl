
namespace Contigo
{
    using System;
    using System.Collections.Generic;
    using Standard;

    public enum ContactSortOrder
    {
        AscendingByDisplayName,
        DescendingByDisplayName,
        AscendingByLastName,
        DescendingByLastName,
        AscendingByBirthday,
        DescendingByBirthday,
        AscendingByRecentActivity,
        DescendingByRecentActivity,
        AscendingByInterestLevel,
        DescendingByInterestLevel,
    }

    public class FacebookContactCollection : FacebookCollection<FacebookContact>
    {
        internal static FacebookContactCollection CreateStaticCollection(IEnumerable<FacebookContact> contacts)
        {
            return new FacebookContactCollection(contacts);
        }

        private FacebookContactCollection(IEnumerable<FacebookContact> contacts)
            : base(contacts)
        {}

        internal FacebookContactCollection(MergeableCollection<FacebookContact> rawCollection, FacebookService service)
            : base(rawCollection, service)
        {}

    }
}
