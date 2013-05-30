
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
    using System.Windows.Shapes;
    using Contigo;
    using Standard;

    using IOPath = System.IO.Path;

    public static class SplashScreenOverlay
    {
        private static bool _hasGeneratedSplash = false;

        // Dimensions that match the face locations in the splash screen.
        // This data is specific to the 5-bubble formatted splash.  If we change the splash screen, need to recalculate the rectangles.
        private static readonly Rect[] _BubbleRects = new Rect[]
        {
            new Rect( 38, 205,  78,  78),  // Leftmost
            new Rect( 70,  95, 100, 100),  // Top left
            new Rect(189,  55,  60,  60),  // Top Right
            new Rect(199, 148,  78,  78),  // Middle Right
            new Rect(193, 245,  53,  53),  // Bottom Right
        };

        // Path to the custom splash screen.
        // TODO: This is known to be the place where other settings are stored, 
        // but it would be better to not have this path duplicated multiple places...
        public static readonly string CustomSplashPath =
            IOPath.Combine(
                IOPath.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Fishbowl"),
                FacebookClientApplication.FacebookApiId +  @"\splash.png");

        // Don't make this statically initialized because the pack:// uri may not yet have been registered.
        //private static readonly Uri _SplashResourceUri = new Uri("pack://application:,,,/Resources/Images/splash.png");

        // How to clip the pictures
        public enum ClipAlgorithm
        {
            Rectangular,
            Elliptical
        }

        /// <summary>
        /// Finds cached copy of the splash screen and deletes it on logout.
        /// </summary>
        public static void DeleteCustomSplashScreen()
        {
            try
            {
                Utility.SafeDeleteFile(CustomSplashPath);
            }
            // It's too bad if something is holding onto the file.
            catch (IOException)
            { }
        }

        public static void GenerateCustomSplashScreen(FacebookContactCollection friends)
        {
            // We don't need to lock on _hasGeneratedSplash because this should only ever enter on one thread.
            friends.VerifyAccess();

            if (_hasGeneratedSplash)
            {
                return;
            }
            _hasGeneratedSplash = true;
            // Do this on a separate thread to not block the UI.
            var t = new Thread((ParameterizedThreadStart)_BuildFriendsListsAndUpdateSplashImages)
            {
                Name = "Splash Screen Generation",
                IsBackground = true,
            };
            t.Start(friends);
        }


        /// <summary>
        /// Renders the passed in images over top the source image, masking with elliptical or rectangular opacity mask as specified, returns an image of this.
        /// </summary>
        /// <param name="sourceImage">Image to be overlaid</param>
        /// <param name="overlayPositions">Positions to draw facesToOverlay on sourceImage</param>
        /// <param name="facesToOverlay">Images to be overlaid on sourceimage</param>
        /// <param name="howToClip">Rectangular or elliptical clipping</param>
        /// <returns></returns>
        private static BitmapSource _AddOverlay(BitmapSource sourceImage, IList<Rect> overlayPositions, IList<ImageSource> overlayImages, ClipAlgorithm howToClip)
        {
            Verify.IsNotNull(sourceImage, "sourceImage");
            Verify.IsNotNull(overlayPositions, "overlayPositions");
            Verify.IsNotNull(overlayImages, "facesToOverlay");
            
            var imageCompositor = new Canvas
            {
                Width = sourceImage.PixelWidth,
                Height = sourceImage.PixelHeight,
            };

            imageCompositor.Children.Add(new Image { Source = sourceImage });

            // Note that the overlay rects and images may not match in length.
            // The user may not have enough friends to fill in the splash screen.  This is okay.
            foreach (var pair in overlayPositions.Zip(overlayImages, (rect, img) => new { Img = img, Rect = rect }))
            {
                imageCompositor.Children.Add(_GetCanvasOverlay(pair.Img, pair.Rect, howToClip));
            }
            
            return Utility.GenerateBitmapSource(imageCompositor, imageCompositor.Width, imageCompositor.Height, true);
        }

        private static void _BuildFriendsListsAndUpdateSplashImages(object friendsObj)
        {
            var friends = (FacebookContactCollection)friendsObj;
            try
            {
                IEnumerable<FacebookContact> lessInterestingFriendsEnum;
                List<FacebookContact> interestingFriends = friends.SplitWhere(f => f.InterestLevel >= 0.8, out lessInterestingFriendsEnum).ToList();
                List<FacebookContact> lessInterestingFriends = lessInterestingFriendsEnum.ToList();

                int friendCount = Math.Min((interestingFriends.Count + lessInterestingFriends.Count), 5);

                var chosenFriends = new List<FacebookContact>(friendCount);
                var rand = new Random(DateTime.Now.Millisecond);
                int selectedFriendCount = 0;

                if (lessInterestingFriends.Count > 0)
                {
                    FacebookContact lessInterest = lessInterestingFriends[rand.Next(lessInterestingFriends.Count - 1)];
                    lessInterestingFriends.Remove(lessInterest);
                    chosenFriends.Add(lessInterest);
                    selectedFriendCount++;
                }

                while ((selectedFriendCount < 5) && (interestingFriends.Count > 0 || lessInterestingFriends.Count > 0))
                {
                    if (interestingFriends.Count > 0)
                    {
                        FacebookContact interest = interestingFriends[rand.Next(interestingFriends.Count - 1)];
                        interestingFriends.Remove(interest);
                        chosenFriends.Add(interest);
                        selectedFriendCount++;
                    }
                    else if (lessInterestingFriends.Count > 0)
                    {
                        FacebookContact lessInterest = lessInterestingFriends[rand.Next(lessInterestingFriends.Count - 1)];
                        lessInterestingFriends.Remove(lessInterest);
                        chosenFriends.Add(lessInterest);
                        selectedFriendCount++;
                    }
                }

                var friendImages = new List<ImageSource>(friendCount);

                foreach (var friend in chosenFriends)
                {
                    friend.Image.GetImageAsync(
                        FacebookImageDimensions.Normal,
                        (sender, e) =>
                        {
                            if (e.Cancelled || e.Error != null)
                            {
                                return;
                            }
                            friendImages.Add(e.ImageSource);
                            if (friendImages.Count == friendCount)
                            {
                                try
                                {
                                    _FriendImageDownloadCompleted(friendImages);
                                }
                                catch (Exception ex)
                                {
                                    Assert.Fail(ex.Message);
                                }
                            }
                        });
                }
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }
        }

        private static void _FriendImageDownloadCompleted(List<ImageSource> friendImages)
        {
            BitmapSource overlaidImage = SplashScreenOverlay._AddOverlay(
                new BitmapImage(new Uri("pack://application:,,,/Resources/Images/splash.png")),
                _BubbleRects,
                friendImages,
                ClipAlgorithm.Elliptical);

            Utility.SaveToPng(overlaidImage, CustomSplashPath);
        }        

        // Creates an element with Canvas attached DPs and appropriate masking if specified.
        private static UIElement _GetCanvasOverlay(ImageSource overlaySource, Rect overlayPosition, ClipAlgorithm howToClip)
        {
            Brush opacityBrush = null;

            switch (howToClip)
            {
                case ClipAlgorithm.Elliptical:
                    double dimensionDelta = overlayPosition.Height - overlayPosition.Width;
                    opacityBrush = new DrawingBrush
                    {
                        Drawing = new GeometryDrawing
                        {
                            Geometry = new EllipseGeometry(overlayPosition),
                            Brush = new RadialGradientBrush
                            {
                                GradientOrigin = new Point(0.5, 0.5),
                                Center = new Point(0.5, 0.5),
                                // Since we subtracted Height From Width above (arbitrary) to normalize radii for 
                                // the gradient, subtract for Width and add for Height (I can show this on paper but this "works" for stretching radius.
                                RadiusX = 0.5 - (0.5 * (dimensionDelta / overlayPosition.Height)),
                                RadiusY = 0.5 + (0.5 * (dimensionDelta / overlayPosition.Width)),
                                GradientStops = 
                                {
                                    new GradientStop(Colors.White, 0.0),
                                    new GradientStop(Colors.White, 0.85),
                                    new GradientStop(Colors.Transparent, 1.0),
                                },
                            },
                        }
                    };

                    break;
                case ClipAlgorithm.Rectangular:
                    // TODO: Do we care to do any gradient opacity masking here?
                    // Doesnt't need to be answered until we have a splash screen w/ rectangular overlays.
                    break;
            }

            var rect = new Rectangle
            {
                Width = overlayPosition.Width,
                Height = overlayPosition.Height,
                Fill = new ImageBrush
                {
                    ImageSource = overlaySource,
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                },
                OpacityMask = opacityBrush,
            };

            Canvas.SetLeft(rect, overlayPosition.Left);
            Canvas.SetTop(rect, overlayPosition.Top);

            return rect;
        }
    }
}
