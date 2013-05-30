
namespace Contigo
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using Standard;

    public delegate void SaveImageAsyncCallback(object sender, SaveImageCompletedEventArgs e);
    
    public class SaveImageCompletedEventArgs : AsyncCompletedEventArgs
    {
        private string _path;
        private int _imageNumber;
        private int _outOfTotal;

        internal SaveImageCompletedEventArgs(string path, object userState)
            : base(null, false, userState)
        {
            Verify.IsNeitherNullNorEmpty(path, "path");
            Assert.IsTrue(File.Exists(path));

            CurrentImageIndex = 0;
            TotalImageCount = 1;
            ImagePath = path;
        }

        internal SaveImageCompletedEventArgs(string path, int currentIndex, int totalImageCount, object userState)
            : base(null, false, userState)
        {
            Verify.IsNeitherNullNorEmpty(path, "path");
            Assert.IsTrue(File.Exists(path));

            Assert.BoundedInteger(0, currentIndex, totalImageCount);

            CurrentImageIndex = currentIndex;
            TotalImageCount = totalImageCount;

            ImagePath = path;
        }

        /// <summary>
        /// Initializes a new instance of the SaveImageCompletedEventArgs class for an error or a cancellation.
        /// </summary>
        /// <param name="error">Any error that occurred during the asynchronous operation.</param>
        /// <param name="cancelled">A value indicating whether the asynchronous operation was canceled.</param>
        /// <param name="userState">The user-supplied state object.</param>
        internal SaveImageCompletedEventArgs(Exception error, bool cancelled, object userState)
            : base(error, cancelled, userState)
        {
        }

        public string ImagePath
        {
            get
            {
                RaiseExceptionIfNecessary();
                return _path;;
            }
            private set { _path = value; }
        }

        public int CurrentImageIndex
        {
            get
            {
                RaiseExceptionIfNecessary();
                return _imageNumber;
            }
            private set { _imageNumber = value; }
        }

        public int TotalImageCount
        {
            get
            {
                RaiseExceptionIfNecessary();
                return _outOfTotal;
            }
            private set { _outOfTotal = value; }
        }

        public bool IsLast
        {
            get
            {
                RaiseExceptionIfNecessary();
                return _imageNumber == _outOfTotal - 1;
            }
        }
    }
}
