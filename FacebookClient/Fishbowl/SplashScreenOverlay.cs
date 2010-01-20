
namespace FacebookClient
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;
    using ClientManager;
    using Contigo;
    using Standard;

    static class SplashScreenOverlay
    {
        // Path to the custom splash screen 
        public static readonly string CustomSplashPath = Environment.ExpandEnvironmentVariables("%APPDATA%\\fishbowl_customsplash.png");

        // How to clip the pictures
        public enum ClipAlgorithm
        {
            Rectangular,
            Elliptical
        }

        /// <summary>
        /// Renders the passed in images over top the source image, masking with elliptical or rectangular opacity mask as specified, returns an image of this.
        /// </summary>
        /// <param name="sourceImage">Image to be overlaid</param>
        /// <param name="overlayPositions">Positions to draw facesToOverlay on sourceImage</param>
        /// <param name="facesToOverlay">Images to be overlaid on sourceimage</param>
        /// <param name="howToClip">Rectangular or elliptical clipping</param>
        /// <returns></returns>
        public static BitmapSource AddOverlay(BitmapSource sourceImage, Rect[] overlayPositions, BitmapSource[] facesToOverlay, ClipAlgorithm howToClip)
        {
            var myImage = new Image
            {
                Source = sourceImage,
            };

            Canvas imageCompositor = new Canvas();
            imageCompositor.Width = sourceImage.PixelWidth;
            imageCompositor.Height = sourceImage.PixelHeight;
            imageCompositor.Children.Add(myImage);

            for (int index = 0; ((index < overlayPositions.Length) && (index < facesToOverlay.Length)); index++)
            {
                imageCompositor.Children.Add(_GetCanvasOverlay(facesToOverlay[index], overlayPositions[index], howToClip));
            }
            
            return (BitmapSource)Utility.GenerateBitmapSource((Visual)imageCompositor, imageCompositor.Width, imageCompositor.Height, true);
        }

        /// <summary>
        /// Finds cached copy of the splash screen and deletes it on logout.
        /// </summary>
        public static void DeleteCustomSplashScreen()
        {
            try
            {
                if (File.Exists(CustomSplashPath))
                {
                    Utility.SafeDeleteFile(CustomSplashPath);
                }
            }
            catch { }; // Do nothing.  If something is holding a handle to this file, not much we can do about it.
        }

        /// <summary>
        /// Callback for when the Friends list changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public static void FriendsPopulated(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Prevent doing anything until we have friends in the collection.
            // TODO: Can we un-subscribe from the notification after getting friends?
            if ((ServiceProvider.ViewManager.Friends.Count > 0) && !_createdFriendsSplash)
            {
                try
                {
                    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(
                        delegate
                        {
                            try
                            {
                                BuildFriendsListsAndUpdateSplashImages();
                            }
                            catch { };
                            return null;
                        })
                        , null);
                    _createdFriendsSplash = true;
                }
                catch
                {
                    // Do nothing if this fails, but ideally this should eventually "phone home" with a callstack...
                };
            }
        }

        // Only respond to the friend collection changing the first time 
        // (if the user adds friend(s) in a session, this could get called twice.
        private static bool _createdFriendsSplash = false;
        private static int _currentCount = 0;
        private static int _friendCount = 0;
        private static void BuildFriendsListsAndUpdateSplashImages()
        {
            // No need to write a custom splash screen to the appdata folder for people without friends
            // This is unlikely though since we should only get here if the collection is populated.
            if (ServiceProvider.ViewManager.Friends.Count == 0)
            {
                return;
            }

            List<FacebookContact> interestingFriends = 
                (from friend in ServiceProvider.ViewManager.Friends where friend.InterestLevel >= 0.8 select friend).ToList();
            List<FacebookContact> lessInterestingFriends = 
                (from friend in ServiceProvider.ViewManager.Friends where friend.InterestLevel < 0.8 select friend).ToList();
            
            _friendCount = Math.Min((interestingFriends.Count + lessInterestingFriends.Count), 5);
            FacebookContact[] chosenFriends = new FacebookContact[_friendCount];
            Random rand = new Random(DateTime.Now.Millisecond);
            int selectedFriendCount = 0;

            if (lessInterestingFriends.Count > 0)
            {
                FacebookContact lessInterest = lessInterestingFriends[rand.Next(lessInterestingFriends.Count - 1)];
                lessInterestingFriends.Remove(lessInterest);
                chosenFriends[selectedFriendCount] = lessInterest;
                selectedFriendCount++;
            }

            while ((selectedFriendCount < 5) && interestingFriends.Count > 0 && lessInterestingFriends.Count > 0)
            {
                if (interestingFriends.Count > 0)
                {
                    FacebookContact interest = interestingFriends[rand.Next(interestingFriends.Count - 1)];
                    interestingFriends.Remove(interest);
                    chosenFriends[selectedFriendCount] = interest;
                    selectedFriendCount++;
                }
                else if (lessInterestingFriends.Count > 0)
                {
                    FacebookContact lessInterest = lessInterestingFriends[rand.Next(lessInterestingFriends.Count - 1)];
                    lessInterestingFriends.Remove(lessInterest);
                    chosenFriends[selectedFriendCount] = lessInterest;
                    selectedFriendCount++;
                }
            }
            GetImageSourceAsyncCallback imageDownloadCompleted = new GetImageSourceAsyncCallback(_OnGetFriendImageCompleted);
            friendImages = new List<BitmapSource>(_friendCount);
            friendCompleted = new ManualResetEvent(false);

            for (int count = 0; count < selectedFriendCount; count++)
            {
                chosenFriends[count].Image.GetImageAsync(FacebookImageDimensions.Square, imageDownloadCompleted);
            }
        }

        static List<BitmapSource> friendImages;
        static ManualResetEvent friendCompleted;
        private static void _OnGetFriendImageCompleted(object sender, GetImageSourceCompletedEventArgs e)
        {
            friendImages.Add((BitmapSource)e.ImageSource);
            ++_currentCount;
            _FriendImageDownloadCompleted();
        }

        private static void _FriendImageDownloadCompleted()
        {
            if (_currentCount < 5)
            {
                return;
            }
            // No point recording a custom splash screen if we failed to get images for it.
            // 2nd part is for debugging, don't check in like this.
            if (friendImages.Count > 0)
            {
                // This data is specific to the 5-bubble formatted splash.  If we change the splash screen, need to recalculate the rectangles.
                Rect[] fishbowlBubbles = new Rect[]{ new Rect(new Point(68,92), new Point(173,197)), 
                                                 new Rect(new Point(188,54), new Point(246,113)),
                                                 new Rect(new Point(37,204), new Point(112,282)), 
                                                 new Rect(new Point(198,150), new Point(275,228)),
                                                 new Rect(new Point(191,243), new Point(247,295))};

                BitmapImage imageToOverLay = new BitmapImage();
                imageToOverLay.BeginInit();
                imageToOverLay.UriSource = new Uri("pack://application:,,,/Resources/Images/splash.png");
                imageToOverLay.EndInit();
                // End "This data is specific ..."

                BitmapSource overlaidImage = SplashScreenOverlay.AddOverlay(imageToOverLay, fishbowlBubbles,
                    (friendImages.ToArray()), SplashScreenOverlay.ClipAlgorithm.Elliptical);

                Image myImg = new Image();
                myImg.Source = overlaidImage;
                Standard.Utility.SaveToPng((FrameworkElement)myImg, Environment.ExpandEnvironmentVariables(SplashScreenOverlay.CustomSplashPath), new Rect(new Size(overlaidImage.PixelWidth, overlaidImage.PixelHeight)));
            }
        }        

        private static UIElement _GetCanvasOverlay(BitmapSource bitmapSource, Rect rect, ClipAlgorithm howToClip)
        {
            Image faceImage = new Image();
            faceImage.Source = bitmapSource;
            return _GetCanvasOverlay(faceImage, rect, howToClip);
        }

        // Creates an element with Canvas attached DPs and appropriate masking if specified.
        private static UIElement _GetCanvasOverlay(Image face, Rect overlayPosition, ClipAlgorithm howToClip)
        {
            face.Width = overlayPosition.Width;
            face.Height = overlayPosition.Height;
            Canvas.SetLeft(face, overlayPosition.Left);
            Canvas.SetTop(face, overlayPosition.Top);

            switch (howToClip)
            {
                case ClipAlgorithm.Elliptical:

                    var edgeFading = new RadialGradientBrush
                    {
                        GradientOrigin = new Point(0.5, 0.5),
                        Center = new Point(0.5, 0.5),
                        // Currently perfectly circular but wouldn't be too hard to make this match the dimensions of the rect if needed.
                        RadiusX = 0.5,
                        RadiusY = 0.5
                    };

                    edgeFading.GradientStops.AddRange(
                        new GradientStop(Colors.White, 0.0),
                        new GradientStop(Color.FromArgb(0xDF, 0, 0, 0), 0.87),
                        new GradientStop(Colors.Transparent, 1.0));

                    var ellipseDrawing = new GeometryDrawing
                    {
                        Geometry = new EllipseGeometry(overlayPosition),
                        Brush = edgeFading,
                    };

                    var drawBrush = new DrawingBrush(ellipseDrawing);

                    face.OpacityMask = drawBrush;
                    break;
                case ClipAlgorithm.Rectangular:
                    // TODO: Do we care to do any gradient opacity masking here?
                    // Doesnt't need to be answered until we have a splash screen w/ rectangular overlays.
                    break;
            }

            return face;
        }
    }
}
