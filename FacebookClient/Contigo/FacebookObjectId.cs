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
            _id = new SmallString(id);
            _cachedHashCode = _id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
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

        public static implicit operator string(FacebookObjectId id)
        {
            return id.ToString();
        }
    }
}
