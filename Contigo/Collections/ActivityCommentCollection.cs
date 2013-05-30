namespace Contigo
{
    using Standard;

    public class ActivityCommentCollection : FacebookCollection<ActivityComment>
    {
        internal ActivityCommentCollection(FBMergeableCollection<ActivityComment> rawCollection, FacebookService service)
            : base(rawCollection, service)
        { }
    }
}
