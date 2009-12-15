
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
        private FacebookContactCollection(IEnumerable<FacebookContact> contacts)
            : base(contacts)
        {}

        internal FacebookContactCollection(MergeableCollection<FacebookContact> rawCollection, FacebookService service)
            : base(rawCollection, service)
        {}

    }
}
