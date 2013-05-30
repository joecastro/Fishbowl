namespace Contigo
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using Standard;

    public class ActivityPostCollection : FacebookCollection<ActivityPost>
    {
        private static bool _IsInteresting(FacebookContact contact)
        {
            return contact.InterestLevel > .1;
        }

        private readonly FBMergeableCollection<ActivityPost> _filteredCollection;
        private readonly FBMergeableCollection<ActivityPost> _rawCollection;
        private readonly Dictionary<FacebookContact, bool> _interestMap; 

        internal ActivityPostCollection(FBMergeableCollection<ActivityPost> sourceCollection, FacebookService service, bool filterable)
            : base(sourceCollection, service)
        {
            if (filterable)
            {
                _interestMap = new Dictionary<FacebookContact, bool>();
                _rawCollection = sourceCollection;
                // Sort this filtered view same as the underlying collection.
                // If there's ever a custom sort on ActivityPosts, I need to add an INotifyPropertyChanged implementation to keep the sort orders in sync.
                _filteredCollection = new FBMergeableCollection<ActivityPost>(from post in sourceCollection where _IsInteresting(post.Actor) select post, true);
                base.ReplaceSourceCollection(_filteredCollection);

                foreach (var contact in from p in sourceCollection select p.Actor)
                {
                    _interestMap.Add(contact, _IsInteresting(contact));
                    contact.PropertyChanged += _OnContactPropertyChanged;
                }

                sourceCollection.CollectionChanged += _OnRawCollectionChanged;
            }
        }

        private void _OnContactPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "InterestLevel")
            {
                var contact = (FacebookContact)sender;
                if (_IsInteresting(contact) != _interestMap[contact])
                {
                    _interestMap[contact] = _IsInteresting(contact);
                    _UpdateCollection();
                }
            }
        }

        void _OnRawCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    // If an item got added, was it something we care to see?
                    Assert.AreEqual(1, e.NewItems.Count);
                    var newPost = (ActivityPost)e.NewItems[0];
                    if (_IsInteresting(newPost.Actor))
                    {
                        _filteredCollection.Add(newPost);
                        _interestMap[newPost.Actor] = true; 
                    }
                    break;

                case NotifyCollectionChangedAction.Move:
                    _filteredCollection.RefreshSort();
                    break;

                case NotifyCollectionChangedAction.Remove:
                    // If an item got removed, was it one we were really showing?
                    Assert.AreEqual(1, e.OldItems.Count);
                    var oldPost = (ActivityPost)e.OldItems[0];
                    if (_IsInteresting(oldPost.Actor))
                    {
                        if (_filteredCollection.Remove(oldPost))
                        {
                            if (!_filteredCollection.Any(post => post.Actor.UserId == oldPost.Actor.UserId))
                            {
                                oldPost.Actor.PropertyChanged -= _OnContactPropertyChanged;
                                _interestMap.Remove(oldPost.Actor);
                            }
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    // Unsupported
                    Assert.Fail();
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var contact in _interestMap.Keys)
                    {
                        contact.PropertyChanged -= _OnContactPropertyChanged;
                    }

                    _interestMap.Clear();
                    _filteredCollection.Clear();

                    break;
            }
        }

        private void _UpdateCollection()
        {
            _filteredCollection.Merge(from post in _rawCollection where _IsInteresting(post.Actor) select post, false);

            foreach (var contact in from p in _rawCollection select p.Actor)
            {
                if (!_interestMap.ContainsKey(contact))
                {
                    _interestMap.Add(contact, _IsInteresting(contact));
                    contact.PropertyChanged += _OnContactPropertyChanged;
                }
            }

        }
    }
}