
namespace Contigo
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Windows;
    using System.Windows.Media.Imaging;
    using Standard;
    
    public enum FacebookImageDimensions
    {
        /// <summary>Photo with a max width of 130px and max height of 130px.</summary>
        Normal,
        /// <summary>Photo with a max width of 720px and max height of 720px.</summary>
        Big,
        /// <summary>Photo with a max width of 75px and max height of 225px.</summary>
        Small,
        /// <summary>Square photo with a width and height of 50px.  Usually available for user images.</summary>
        Square,
    }

    public enum FacebookImageSaveOptions
    {
        PreserveOriginal,
        Overwrite,
        FindBetterName,
    }

    public class FacebookImage : IFacebookObject
    {
        private class _ImageCallbackState
        {
            public GetImageSourceAsyncCallback Callback { get; set; }
            public FacebookImageDimensions RequestedSize { get; set; }
        }

        private static readonly Dictionary<FacebookImageDimensions, Size> _DimensionLookup = new Dictionary<FacebookImageDimensions, Size>
        {
            { FacebookImageDimensions.Normal, new Size(130, 130) },
            { FacebookImageDimensions.Big,    new Size(720, 720) },
            { FacebookImageDimensions.Small,  new Size(75,  225) },
            { FacebookImageDimensions.Square, new Size(50,   50) },
        };

        public static Size GetDimensionSize(FacebookImageDimensions dimensions)
        {
            Size size;
            if (_DimensionLookup.TryGetValue(dimensions, out size))
            {
                return size;
            }

            throw new ArgumentException("Invalid enum value", "dimensions");
        }

        private bool _isSquarish;
        private SmallUri? _normal;
        private SmallUri? _big;
        private SmallUri? _small;
        private SmallUri? _square;
        private WeakReference _normalWR;
        private WeakReference _bigWR;
        private WeakReference _smallWR;
        private WeakReference _squareWR;

        private FacebookImage(FacebookService service)
        {
            Assert.IsNotNull(service);
            SourceService = service;
        }

        internal FacebookImage(FacebookService service, Uri uri)
            : this(service, uri, false)
        {}

        internal FacebookImage(FacebookService service, Uri uri, bool shouldBeSquarish)
        {
            Assert.IsNotNull(service);
            SourceService = service;

            if (uri != null)
            {
                _normal = new SmallUri(uri.OriginalString);
                SourceService.WebGetter.QueueImageRequest(_normal.Value);
                _isSquarish = shouldBeSquarish;
            }
        }

        internal FacebookImage(FacebookService service, Uri normal, Uri big, Uri small, Uri square)
        {
            Assert.IsNotNull(service);
            SourceService = service;

            if (normal != null)
            {
                _normal = new SmallUri(normal.OriginalString);
                SourceService.WebGetter.QueueImageRequest(_normal.Value);
            }
            if (small != null)
            {
                _small = new SmallUri(small.OriginalString);
                SourceService.WebGetter.QueueImageRequest(_small.Value);
            }
            if (big != null)
            {
                _big = new SmallUri(big.OriginalString);
                SourceService.WebGetter.QueueImageRequest(_big.Value);
            }
            if (square != null)
            {
                _square = new SmallUri(square.OriginalString);
                SourceService.WebGetter.QueueImageRequest(_square.Value);
            }
        }

        public void GetImageAsync(FacebookImageDimensions requestedSize, GetImageSourceAsyncCallback callback)
        { 
            Verify.IsNotNull(callback, "callback");

            Assert.IsNotNull(SourceService);
            Assert.IsNotNull(SourceService.WebGetter);

            SmallUri ss = _GetSmallUriFromRequestedSize(requestedSize);
            if (ss == default(SmallUri))
            {
                callback(this, new GetImageSourceCompletedEventArgs(new InvalidOperationException("The requested image doesn't exist"), false, this));
            }

            BitmapSource img;
            if (_TryGetFromWeakCache(requestedSize, out img))
            {
                callback(this, new GetImageSourceCompletedEventArgs(img, this));
            }

            var userState = new _ImageCallbackState { Callback = callback, RequestedSize = requestedSize };
            SourceService.WebGetter.GetImageSourceAsync(this, userState, ss, _AddWeakCacheCallback);
        }

        private bool _TryGetFromWeakCache(FacebookImageDimensions requestedSize, out BitmapSource img)
        {
            img = null;
            switch (requestedSize)
            {
                case FacebookImageDimensions.Big:
                    if (_bigWR != null)
                    {
                        img = _bigWR.Target as BitmapSource;
                    }
                    break;
                case FacebookImageDimensions.Normal:
                    if (_normalWR != null)
                    {
                        img = _normalWR.Target as BitmapSource;
                    }
                    break;
                case FacebookImageDimensions.Small:
                    if (_smallWR != null)
                    {
                        img = _smallWR.Target as BitmapSource;
                    }
                    break;
                case FacebookImageDimensions.Square:
                    if (_squareWR != null)
                    {
                        img = _squareWR.Target as BitmapSource;
                    }
                    break;
            }

            return img != null;
        }

        private void _AddWeakCacheCallback(object sender, GetImageSourceCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled)
            {
                return;
            }

            var bs = (BitmapSource)e.ImageSource;
            if (_isSquarish)
            {
                if (e.ImageSource.Height == 0)
                {
                    return;
                }

                double aspectRatio = e.ImageSource.Width / e.ImageSource.Height;
                // Allow for some flexibility.  This is really a special case for newsfeed filter icons and I can't really predict how Facebook is going to break things in the future.
                if (aspectRatio > 1.8 && aspectRatio < 2.5)
                {
                    // Take the right half of the image.
                    bs = new CroppedBitmap(bs, new Int32Rect((int)(.5 * bs.Width), 0, (int)(bs.Width *.5), (int)bs.Height));
                }
                else
                {
                    // If we didn't need to do cropping then we won't in the future, so just short circuit this next time.
                    _isSquarish = false;
                }
            }

            var userState = (_ImageCallbackState)e.UserState;
            switch (userState.RequestedSize)
            {
                case FacebookImageDimensions.Big:
                    _bigWR = new WeakReference(bs);
                    break;
                case FacebookImageDimensions.Normal:
                    _normalWR = new WeakReference(bs);
                    break;
                case FacebookImageDimensions.Small:
                    _smallWR = new WeakReference(bs);
                    break;
                case FacebookImageDimensions.Square:
                    _squareWR = new WeakReference(bs);
                    break;
            }

            if (userState.Callback != null)
            {
                userState.Callback(this, new GetImageSourceCompletedEventArgs(bs, this));
            }
        }

        public void SaveToFile(FacebookImageDimensions requestedSize, string path, bool addExtension, FacebookImageSaveOptions options, SaveImageAsyncCallback callback, object userState)
        {
            SaveToFile(requestedSize, path, addExtension, options, callback, userState, null, null);
        }

        internal void SaveToFile(FacebookImageDimensions requestedSize, string path, bool addExtension, FacebookImageSaveOptions options, SaveImageAsyncCallback callback, object userState, int? index, int? total)
        {
            Verify.IsNeitherNullNorEmpty(path, "path");
            Verify.IsNotNull(callback, "callback");
            Assert.Implies(total != null, index != null);
            Assert.Implies(total == null, index == null);

            SafeCopyFileOptions scfo = (options == FacebookImageSaveOptions.FindBetterName)
                ? SafeCopyFileOptions.FindBetterName
                : (options == FacebookImageSaveOptions.Overwrite)
                    ? SafeCopyFileOptions.Overwrite
                    : SafeCopyFileOptions.PreserveOriginal;
            
            SourceService.WebGetter.GetLocalImagePathAsync(this, null, _GetSmallUriFromRequestedSize(requestedSize),
                (sender, e) =>
                {
                    string cachePath = e.ImagePath;
                    if (addExtension)
                    {
                        string ext = Path.GetExtension(cachePath);
                        path = Path.ChangeExtension(path, ext);
                    }

                    try
                    {
                        string actualPath = Utility.SafeCopyFile(cachePath, path, scfo);
                        if (actualPath == null)
                        {
                            throw new IOException("Unable to save the image to the requested location.");
                        }

                        SaveImageCompletedEventArgs sicea = null;
                        if (total == null)
                        {
                            sicea = new SaveImageCompletedEventArgs(actualPath, userState);
                        }
                        else
                        {
                            sicea = new SaveImageCompletedEventArgs(actualPath, index.Value, total.Value, userState);
                        }

                        callback(this, sicea);
                        return;
                    }
                    catch (Exception ex)
                    {
                        callback(this, new SaveImageCompletedEventArgs(ex, false, userState));
                        return;
                    }
                });
        }

        public bool IsCached(FacebookImageDimensions requestedSize)
        {
            SmallUri sizedString = _GetSmallUriFromRequestedSize(requestedSize);
            //Assert.IsNotDefault(sizedString);
            string path;
            return SourceService.WebGetter.TryGetImageFile(sizedString, out path);
        }

        private SmallUri _GetSmallUriFromRequestedSize(FacebookImageDimensions requestedSize)
        {
            // If one url type isn't available, try to fallback on other provided values.

            SmallUri? str = null;
            switch (requestedSize)
            {
                case FacebookImageDimensions.Big:    str = _big ?? _normal ?? _small ?? _square; break;
                case FacebookImageDimensions.Small:  str = _small ?? _normal ?? _big ?? _square; break;
                case FacebookImageDimensions.Square: str = _square ?? _small ?? _normal ?? _big; break;
                case FacebookImageDimensions.Normal: str = _normal ?? _big ?? _small ?? _square; break;
            }

            return str ?? default(SmallUri);
        }

        #region IFacebookObject Members

        FacebookService IFacebookObject.SourceService { get; set; }

        private FacebookService SourceService
        {
            get { return ((IFacebookObject)this).SourceService; }
            set { ((IFacebookObject)this).SourceService = value; }
        }

        #endregion

        public override bool Equals(object obj)
        {
            return this.Equals(obj as FacebookImage);
        }

        public override int GetHashCode()
        {
            return _big.GetHashCode() ^ _normal.GetHashCode() ^ _small.GetHashCode() << 8 ^ _square.GetHashCode() >> 8;
        }

        #region IEquatable<FacebookImage> Members

        public bool Equals(FacebookImage other)
        {
            if (other == null)
            {
                return false;
            }

            return other._big == _big
                && other._normal == _normal
                && other._small == _small
                && other._square == _square;
        }

        #endregion

        internal bool PartiallyEquals(FacebookImage other)
        {
            if (other == null)
            {
                return false;
            }

            return other._big == _big
                || other._normal == _normal
                || other._small == _small
                || other._square == _square;
        }

        internal bool SafeMerge(FacebookImage other)
        {
            bool modified = false;

            if (other == null)
            {
                return false;
            }

            if (_normal == null && other._normal != null)
            {
                _normal = other._normal;
                modified = true;
            }

            if (_small == null && other._small != null)
            {
                _small = other._small;
                modified = true;
            }

            if (_square == null && other._square != null)
            {
                _square = other._square;
                modified = true;
            }

            if (_big == null && other._big != null)
            {
                _big = other._big;
                modified = true;
            }

            return modified;
        }

        public FacebookImage Clone()
        {
            var img = new FacebookImage(SourceService);
            img._isSquarish = _isSquarish;
            img._big = _big;
            img._bigWR = _bigWR;
            img._normal = _normal;
            img._normalWR = _normalWR;
            img._small = _small;
            img._smallWR = _smallWR;
            img._square = _square;
            img._squareWR = _squareWR;

            return img;
        }

        public bool IsEmpty
        {
            get
            {
                return _GetSmallUriFromRequestedSize(FacebookImageDimensions.Normal) == default(SmallUri);
            }
        }

        internal bool IsMostlyEmpty
        {
            get
            {
                return _GetSmallUriFromRequestedSize(FacebookImageDimensions.Big) == _GetSmallUriFromRequestedSize(FacebookImageDimensions.Square)
                    && _square != null;
            }
        }
    }
}
