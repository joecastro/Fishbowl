
namespace StandardTests
{
    using System;
    using System.Text;
    using System.Collections.Generic;
    using System.Linq;
    using System.ComponentModel;
    using Standard;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using UTVerify = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using UTVerify2 = Standard.UTVerify;

    /// <summary>
    /// Summary description for UnitTest1
    /// </summary>
    [TestClass]
    public class MergeableCollectionTests
    {
        private class _UO : INotifyPropertyChanged, IComparable<_UO>, IEquatable<_UO>, IMergeable<string, _UO>
        {
            private int _sortValue;
            private string _id;

            public _UO(string name, int value)
            {
                Name = name;
                SortValue = value;
            }

            public string Name
            {
                get { return _id ?? ""; }
                set
                {
                    if (_id != (value ?? ""))
                    {
                        _id = value ?? "";
                        _NotifyPropertyChanged("Name");
                    }
                }

            }
            public int SortValue
            {
                get { return _sortValue; }
                set
                {
                    if (_sortValue != value)
                    {
                        _sortValue = value;
                        _NotifyPropertyChanged("SortValue");
                    }
                }
            }

            private void _NotifyPropertyChanged(string propertyName)
            {
                var handler = PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            #region INotifyPropertyChanged Members

            public event PropertyChangedEventHandler PropertyChanged;

            #endregion

            #region IComparable<_UpdatableObject> Members

            public int CompareTo(_UO other)
            {
                if (other == null)
                {
                    return 1;
                }

                return SortValue.CompareTo(other.SortValue);
            }

            #endregion

            public override string ToString()
            {
                return Name + ": " + SortValue;
            }

            public override bool Equals(object obj)
            {
                var other = obj as _UO;
                if (other == null)
                {
                    return false;
                }
                return this.Equals(other);
            }

            public override int GetHashCode()
            {
                return Name.GetHashCode() ^ SortValue.GetHashCode();
            }

            #region IEquatable<_UO> Members

            public bool Equals(_UO other)
            {
                if (other == null)
                {
                    return false;
                }
                return other.Name == this.Name && other.SortValue == this.SortValue;
            }

            #endregion

            #region IMergeable<_UO> Members

            public string FKID
            {
                get { return Name; }
            }

            public void Merge(_UO other)
            {
                UTVerify.IsNotNull(other);
                UTVerify.AreEqual(other.SortValue, this.SortValue);

                this.Name = other.Name;
                var handler = PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs("Name"));
                }
            }

            #endregion
        }
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void SortedCollectionPropertyUpdatesTest()
        {
            var sourceList = new List<_UO>
            {
                new _UO("A", 1),
                new _UO("B", 3),
                new _UO("C", 5),
                new _UO("D", 7),
                new _UO("E", 9),
            };

            sourceList.Reverse();
            var collection = new MergeableCollection<string, _UO>(sourceList);
            sourceList.Reverse();

            UTVerify2.CollectionsAreEqual(sourceList, collection);

            sourceList[0].SortValue = 2;

            UTVerify2.CollectionsAreEqual(sourceList, collection);

            var item = sourceList[1];
            item.SortValue = 10;
            sourceList.RemoveAt(1);
            sourceList.Add(item);

            UTVerify2.CollectionsAreEqual(sourceList, collection);
        }

        [TestMethod]
        public void SortedCollectionInsertMiddleTest()
        {
            var sourceList = new List<_UO>
            {
                new _UO("A", 1),
                new _UO("B", 2),
                new _UO("X", 24),
                new _UO("Y", 25),
                new _UO("Z", 26),
            };

            var updateList = new List<_UO>
            {
                new _UO("D", 4),
                new _UO("C", 3),
                new _UO("E", 5),
            };

            var collection = new MergeableCollection<string, _UO>(sourceList);
            collection.Merge(updateList, true);

            updateList.Sort();

            sourceList.InsertRange(2, updateList);
            UTVerify2.CollectionsAreEqual(sourceList, collection);
        }

        [TestMethod]
        public void ReplaceSingleItemWithMergeInUnboundedCollection()
        {
            char c = 'A';
            var sourceList = new List<_UO>();
            for (int i = 1; i <= 20; ++i)
            {
                sourceList.Add(new _UO(c++.ToString(), i));
            }

            var collection = new MergeableCollection<string, _UO>(sourceList);
            collection.Add(new _UO("Z", 0));

            sourceList.Insert(0, new _UO("Z", 0));

            UTVerify2.CollectionsAreEqual(sourceList, collection);

            sourceList[0] = new _UO("ZZ", 0);
            sourceList.RemoveAt(sourceList.Count - 1);
            collection.Merge(sourceList, false, null);

            UTVerify2.CollectionsAreEqual(sourceList, collection);
        }

        [TestMethod]
        public void RemoveAndReplaceWithBoundedMerge()
        {
            char c = 'A';
            var sourceList = new List<_UO>();
            for (int i = 1; i <= 20; ++i)
            {
                sourceList.Add(new _UO(c++.ToString(), i));
            }

            var collection = new MergeableCollection<string, _UO>(sourceList);

            var zItem = new _UO("Z", 0);
            collection.Add(zItem);

            sourceList.Insert(0, new _UO("Z", 0));

            UTVerify2.CollectionsAreEqual(sourceList, collection);

            var item = collection.FindFKID("Z");
            UTVerify2.AreReferenceEqual(item, zItem);

            collection.Remove(item);

            var zItem2 = new _UO("Z", 0);
            sourceList[0] = zItem2;

            collection.Merge(new _UO[] { zItem2, sourceList[1] }, true, sourceList.Count);

            UTVerify2.CollectionsAreEqual(sourceList, collection);
        }

    }
}
