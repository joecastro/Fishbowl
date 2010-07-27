using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Contigo
{
    using Standard;

    internal interface IFBMergeable<T> : IMergeable<string, T>
    { }

    internal class FBMergeableCollection<T> : MergeableCollection<string, T> where T : class
    {
        public FBMergeableCollection() : base()
        {}

        public FBMergeableCollection(bool sort) : base(sort)
        {}

        public FBMergeableCollection(IEnumerable<T> dataObjects) : base(dataObjects)
        {}

        public FBMergeableCollection(IEnumerable<T> dataObjects, bool sort) : base(dataObjects, sort)
        {}
    }
}
