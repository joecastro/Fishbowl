namespace Contigo
{
    using Standard;
    using System;

    public struct FacebookObjectId : IEquatable<FacebookObjectId>
    {
        private readonly SmallString _id;
        private readonly int _cachedHashCode;

        internal FacebookObjectId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                _id = default(SmallString);
                _cachedHashCode = 0;

                return;
            }

            _id = new SmallString(id);
            _cachedHashCode = _id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            // This is a struct type.  Why is it being compared to something that is nullable?
            // When I've seen this it's always been a caller error.
            Assert.IsNotNull(obj);
            try
            {
                return Equals((FacebookObjectId)obj);
            }
            catch (InvalidCastException)
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return _cachedHashCode;
        }

        public override string ToString()
        {
            return _id.GetString();
        }

        public bool Equals(FacebookObjectId other)
        {
            if (_cachedHashCode != other._cachedHashCode)
            {
                return false;
            }

            return _id.Equals(other._id);
        }

        public static bool operator ==(FacebookObjectId left, FacebookObjectId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FacebookObjectId left, FacebookObjectId right)
        {
            return !left.Equals(right);
        }

        public static bool IsValid(FacebookObjectId id)
        {
            return id != default(FacebookObjectId);
        }

        public static FacebookObjectId Create(string id)
        {
            return new FacebookObjectId(id);
        }
    }
}
