//-----------------------------------------------------------------------
// <copyright file="PhotoViewerControl.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
//     Control used to display a full photo.
// </summary>
//-----------------------------------------------------------------------

namespace FacebookClient
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using ClientManager;
    using ClientManager.Controls;
    using Contigo;
    using FacebookClient.Controls;
    using Standard;

    [
        TemplatePart(Name = "PART_PhotoDisplay", Type = typeof(PhotoDisplayControl)),
        TemplatePart(Name = "PART_PhotoScrollViewer", Type = typeof(ScrollViewer)),
        TemplatePart(Name = "PART_TagTargetElement", Type = typeof(TagTarget)),
        TemplatePart(Name = "PART_PhotoTaggerControl", Type = typeof(PhotoTaggerControl)),
    ]
    public class PhotoViewerControl : SizeTemplateControl
    {
        public static readonly DependencyProperty FacebookPhotoProperty = DependencyProperty.Register(
            "FacebookPhoto", 
            typeof(FacebookPhoto), 
            typeof(PhotoViewerControl));

        public FacebookPhoto FacebookPhoto
        {
            get { return (FacebookPhoto)GetValue(FacebookPhotoProperty); }
            set { SetValue(FacebookPhotoProperty, value); }
        }

        public static readonly DependencyProperty TaggingPhotoProperty = DependencyProperty.Register(
            "TaggingPhoto", 
            typeof(bool), 
            typeof(PhotoViewerControl), 
            new UIPropertyMetadata(
                false, 
                OnTaggingPhotoChanged));

        public bool TaggingPhoto
        {
            get { return (bool)GetValue(TaggingPhotoProperty); }
            set { SetValue(TaggingPhotoProperty, value); }
        }

        public static RoutedCommand ExploreTagCommand { get; private set; }
        public static RoutedCommand SetAsDesktopBackgroundCommand { get; private set; }
        public static RoutedCommand IsMouseOverTagCommand { get; private set; }
        public static RoutedCommand SavePhotoAsCommand { get; private set; }
        public static RoutedCommand SaveAlbumCommand { get; private set; }
        public static RoutedCommand StartSlideShowCommand { get; private set; }

        private PhotoDisplayControl _photoDisplay;
        private ScrollViewer _photoScrollViewer;
        private TagTarget _tagTarget;
        private PhotoTaggerControl _photoTagger;

        static PhotoViewerControl()
        {
            ExploreTagCommand = new RoutedCommand("ExploreTag", typeof(PhotoViewerControl));
            SetAsDesktopBackgroundCommand = new RoutedCommand("SetAsDesktopBackground", typeof(PhotoViewerControl));
            IsMouseOverTagCommand = new RoutedCommand("IsMouseOverTag", typeof(PhotoViewerControl));
            SaveAlbumCommand = new RoutedCommand("SaveAlbum", typeof(PhotoViewerControl));
            SavePhotoAsCommand = new RoutedCommand("SavePhotoAs", typeof(PhotoViewerControl));
            StartSlideShowCommand = new RoutedCommand("StartSlideShow", typeof(PhotoViewerControl));
        }
      
        public PhotoViewerControl()
        {
            // Set the key commands for the photo viewer control.
            CommandBindings.Add(new CommandBinding(ExploreTagCommand, new ExecutedRoutedEventHandler(OnExploreTagCommand)));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Print, new ExecutedRoutedEventHandler(OnPrintCommand)));
            CommandBindings.Add(new CommandBinding(SetAsDesktopBackgroundCommand, new ExecutedRoutedEventHandler(OnSetAsDesktopBackgroundCommand)));
            CommandBindings.Add(new CommandBinding(SavePhotoAsCommand, (sender, e) => ((PhotoViewerControl)sender)._OnSavePhotoCommand(e)));
            CommandBindings.Add(new CommandBinding(SaveAlbumCommand, new ExecutedRoutedEventHandler(OnSaveAlbumCommand)));
            CommandBindings.Add(new CommandBinding(IsMouseOverTagCommand, new ExecutedRoutedEventHandler(OnIsMouseOverTagCommand)));
            CommandBindings.Add(new CommandBinding(StartSlideShowCommand, (sender, e) => ((PhotoViewerControl)sender)._StartSlideShow()));

            MouseLeftButtonDown += new MouseButtonEventHandler(PhotoViewerControl_MouseLeftButtonDown);
            MouseLeftButtonUp += new MouseButtonEventHandler(PhotoViewerControl_MouseLeftButtonUp);

            KeyDown += new KeyEventHandler(OnKeyDown);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _photoDisplay = this.Template.FindName("PART_PhotoDisplay", this) as PhotoDisplayControl;
            _photoScrollViewer = this.Template.FindName("PART_PhotoScrollViewer", this) as ScrollViewer;
            _tagTarget = this.Template.FindName("PART_TagTargetElement", this) as TagTarget;
            _photoTagger = this.Template.FindName("PART_PhotoTaggerControl", this) as PhotoTaggerControl;

            _photoScrollViewer.PreviewMouseWheel += new MouseWheelEventHandler(OnPhotoScrollViewerPreviewMouseWheel);
            _photoTagger.TagsCanceledEvent += new TagsCanceledEventHandler(PhotoTagger_TagsCanceledEvent);
            _photoTagger.TagsUpdatedEvent += new TagsUpdatedEventHandler(PhotoTagger_TagsUpdatedEvent);
            _photoDisplay.PhotoStateChanged += new PhotoStateChangedEventHandler(PhotoDisplay_PhotoStateChanged);

            // Focus the control so that we can grab keyboard events.
            this.Focus();
        }

        // Weird that this is public.  It's used as a generic handler for mouse scrolling from the filmstrip panel...
        public static void HandleScrollViewerMouseWheel(ScrollViewer scrollViewer, bool checkExtent, MouseWheelEventArgs e)
        {
            // This ScrollViewer will swallow mousewheel events even when it can't be scrolled down any more.
            // We workaround this by raising a new event in that case.

            if (!e.Handled)
            {
                if (!checkExtent || scrollViewer.ExtentHeight <= scrollViewer.ViewportHeight)
                {
                    e.Handled = true;
                    UIElement parent = (UIElement)scrollViewer.Parent;

                    parent.RaiseEvent(
                        new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                        {
                            RoutedEvent = UIElement.MouseWheelEvent,
                            Source = scrollViewer
                        });
                }
            }
        }

        private void OnPhotoScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            HandleScrollViewerMouseWheel((ScrollViewer)sender, true, e);
        }

        /// <summary>
        /// Allows the photo to be zoomed in and out using the mouse wheel.
        /// </summary>
        /// <param name="e">Arguments describing the event.</param>
        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Delta > 0)
                {
                    PhotoDisplayControl.ZoomPhotoInCommand.Execute(e, this._photoDisplay);
                }
                else if (e.Delta < 0)
                {
                    PhotoDisplayControl.ZoomPhotoOutCommand.Execute(e, this._photoDisplay);
                }

                e.Handled = true;
            }
            else
            {
                base.OnPreviewMouseWheel(e);
            }
        }

        /// <summary>
        /// Allows the photo to be fit to the current window size using the mouse wheel.
        /// </summary>
        /// <param name="e">Arguments describing the event.</param>
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.MiddleButton == MouseButtonState.Pressed)
                {
                    this._photoDisplay.FittingPhotoToWindow = true;
                }
            }
        }

        /// <summary>
        /// Command to use the photo explorer to explore a tag.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnExploreTagCommand(object sender, ExecutedRoutedEventArgs e)
        {
            PhotoViewerControl photoViewer = sender as PhotoViewerControl;
            if (photoViewer != null)
            {
                ServiceProvider.ViewManager.NavigationCommands.NavigateSearchCommand.Execute("explore:" + e.Parameter.ToString());
            }
        }

        /// <summary>
        /// Command to print the current photo.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnPrintCommand(object sender, ExecutedRoutedEventArgs e)
        {
            PhotoViewerControl photoViewer = sender as PhotoViewerControl;
            if (photoViewer != null)
            {
                try
                {
                    string photoLocalPath = photoViewer.FacebookPhoto.Image.GetCachePath(FacebookImageDimensions.Big);

                    if (!string.IsNullOrEmpty(photoLocalPath))
                    {
                        photoLocalPath = System.IO.Path.GetFullPath(photoLocalPath);
                        object photoFile = photoLocalPath;
                        WIA.CommonDialog dialog = new WIA.CommonDialogClass();
                        dialog.ShowPhotoPrintingWizard(ref photoFile);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// Sets the currently displayed photo as the desktop background.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnSetAsDesktopBackgroundCommand(object sender, ExecutedRoutedEventArgs e)
        {
            PhotoViewerControl photoViewer = sender as PhotoViewerControl;
            if (photoViewer != null)
            {
                string photoLocalPath = photoViewer.FacebookPhoto.Image.GetCachePath(FacebookImageDimensions.Big);

                if (!string.IsNullOrEmpty(photoLocalPath))
                {
                    string wallpaperPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FacebookClient\\Wallpaper") + Path.GetExtension(photoLocalPath);

                    try
                    {
                        // Clear the current wallpaper (releases lock on current bitmap)
                        NativeMethods.SystemParametersInfo(SPI.SETDESKWALLPAPER, 0, String.Empty, SPIF.UPDATEINIFILE | SPIF.SENDWININICHANGE);

                        // Delete the old wallpaper if it exists
                        if (File.Exists(wallpaperPath))
                        {
                            File.Delete(wallpaperPath);
                        }

                        if (!Directory.Exists(Path.GetDirectoryName(wallpaperPath)))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(wallpaperPath));
                        }

                        // Copy from temp location to my pictures
                        File.Copy(photoLocalPath, wallpaperPath);

                        // Set the desktop background
                        NativeMethods.SystemParametersInfo(SPI.SETDESKWALLPAPER, 0, wallpaperPath, SPIF.UPDATEINIFILE | SPIF.SENDWININICHANGE);
                    }
                    catch (Exception)
                    {
                        //ServiceProvider.Logger.Warning(exception.Message);
                    }
                }
            }
        }

        private void _OnSavePhotoCommand(ExecutedRoutedEventArgs e)
        {
            string photoLocalPath = FacebookPhoto.Image.GetCachePath(FacebookImageDimensions.Big);
            if (!string.IsNullOrEmpty(photoLocalPath))
            {
                string defaultFileName = Path.GetFileName(photoLocalPath);
                if (FacebookPhoto.Album != null)
                {
                    defaultFileName = FacebookPhoto.Album.Title + " (" + (FacebookPhoto.Album.Photos.IndexOf(FacebookPhoto) + 1) + ")";
                }

                var fileDialog = new System.Windows.Forms.SaveFileDialog();

                fileDialog.Filter = "Image Files|*.jpg;*.png;*.bmp;*.gif";
                fileDialog.DefaultExt = Path.GetExtension(photoLocalPath);
                fileDialog.FileName = defaultFileName;
                fileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

                if (fileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string imagePath = fileDialog.FileName;

                    try
                    {
                        // Copy the file to the save location, overwriting if necessary
                        File.Copy(photoLocalPath, imagePath, true);
                    }
                    catch (Exception)
                    {
                        //ServiceProvider.Logger.Warning(exception.Message);
                    }
                }
            }
        }
        
        /// <summary>
        /// Saves every photo in the currently displayed album to a user-provided location.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnSaveAlbumCommand(object sender, ExecutedRoutedEventArgs e)
        {
            PhotoViewerControl photoViewer = sender as PhotoViewerControl;
            if (photoViewer != null)
            {
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                folderDialog.Description = "Choose where to save the album.";
                folderDialog.ShowNewFolderButton = true;
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    photoViewer.FacebookPhoto.Album.SaveToFolder(folderDialog.SelectedPath);
                }
            }
        }

        /// <summary>
        /// Handler for key press events that target the photo control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments describing the event.</param>
        private static void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                PhotoViewerControl photoViewerControl = sender as PhotoViewerControl;
                if (photoViewerControl != null)
                {
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        switch (e.Key)
                        {
                            case Key.OemPlus:
                                PhotoDisplayControl.ZoomPhotoInCommand.Execute(null, photoViewerControl._photoDisplay);
                                e.Handled = true;
                                break;
                            case Key.OemMinus:
                                PhotoDisplayControl.ZoomPhotoOutCommand.Execute(null, photoViewerControl._photoDisplay);
                                e.Handled = true;
                                break;
                            case Key.D0:
                                PhotoDisplayControl.FitPhotoToWindowCommand.Execute(null, photoViewerControl._photoDisplay);
                                e.Handled = true;
                                break;
                            default:
                                break;
                        }
                    }
                    else if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
                    {
                        switch (e.Key)
                        {
                            case Key.Escape:
                                photoViewerControl.Focus();
                                e.Handled = true;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        private Point GetRelativeMousePosition()
        {
            Point mousePosition = Mouse.GetPosition(this._photoDisplay.PhotoImage);
            mousePosition.X /= this._photoDisplay.PhotoImage.ActualWidth;
            mousePosition.Y /= this._photoDisplay.PhotoImage.ActualHeight;
            return mousePosition;
        }

        /// <summary>
        /// On mouse left button down capture mouse if user is tagging photo.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Event args.</param>
        private void PhotoViewerControl_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            if (this.TaggingPhoto)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// On mouse left button up show tag target and menu.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Event args.</param>
        private static void PhotoViewerControl_MouseLeftButtonUp(object sender, MouseEventArgs e)
        {
            PhotoViewerControl photoViewer = sender as PhotoViewerControl;
            if (photoViewer != null)
            {
                if (photoViewer.TaggingPhoto)
                {
                    Point pt = e.GetPosition(photoViewer._photoDisplay.PhotoImage);
                    MatrixTransform mt = (MatrixTransform)photoViewer._photoDisplay.TransformToVisual(photoViewer._photoDisplay.PhotoImage);

                    photoViewer._tagTarget.Scale = Math.Sqrt(1 / mt.Matrix.M11);

                    double widthByTwo = photoViewer._tagTarget.Width / 2;
                    double heightByTwo = photoViewer._tagTarget.Height / 2;
                    double horizontalOffset = photoViewer._photoScrollViewer.HorizontalOffset * mt.Matrix.M11;
                    double verticalOffset = photoViewer._photoScrollViewer.VerticalOffset * mt.Matrix.M22;

                    Point transPt = new Point(widthByTwo * mt.Matrix.M11, heightByTwo * mt.Matrix.M22);

                    double left;
                    double top;

                    // Ensure tag target element does not go outside of horizontal boundry
                    if (pt.X < transPt.X)
                    {
                        left = transPt.X;
                    }
                    else if (pt.X > (photoViewer._photoDisplay.PhotoImage.ActualWidth - transPt.X))
                    {
                        left = photoViewer._photoDisplay.PhotoImage.ActualWidth - transPt.X;
                    }
                    else
                    {
                        left = pt.X;
                    }

                    // Ensure tag target element does not go outside of vertical boundry
                    if (pt.Y < transPt.Y)
                    {
                        top = transPt.Y;
                    }
                    else if (pt.Y > (photoViewer._photoDisplay.PhotoImage.ActualHeight - transPt.Y))
                    {
                        top = photoViewer._photoDisplay.PhotoImage.ActualHeight - transPt.Y;
                    }
                    else
                    {
                        top = pt.Y;
                    }

                    // Set new coordinates
                    transPt = photoViewer._photoDisplay.PhotoImage.TranslatePoint(
                        new Point(left + horizontalOffset, top + verticalOffset),
                        photoViewer._photoScrollViewer);
                    photoViewer._tagTarget.TransformPoint = transPt;
                    PhotoViewerControl.PlaceTaggerControl(photoViewer, transPt);

                    photoViewer._tagTarget.Visibility = Visibility.Visible;
                    photoViewer._photoTagger.Open();
                }

                e.Handled = true;
            }
        }

        /// <summary>
        /// Place the photo tagger to the bottom left of given point.
        /// </summary>
        /// <param name="photoViewer">PhotoViewerControl which holds the tagger control.</param>
        /// <param name="topRightCorner">Top right corner point of tagger.</param>
        private static void PlaceTaggerControl(PhotoViewerControl photoViewer, Point topRightCorner)
        {
            double widthByTwo = photoViewer._tagTarget.Width / 2;
            double heightByTwo = photoViewer._tagTarget.Height / 2;

            double x = topRightCorner.X - photoViewer._photoTagger.Width - widthByTwo;
            double y = topRightCorner.Y - heightByTwo;

            if (topRightCorner.Y + photoViewer._photoTagger.Height > photoViewer._photoScrollViewer.ActualHeight)
            {
                y = photoViewer._photoScrollViewer.ActualHeight - photoViewer._photoTagger.Height - heightByTwo;
            }

            if (topRightCorner.X - widthByTwo < photoViewer._photoTagger.Width)
            {
                x = topRightCorner.X + widthByTwo;
            }

            photoViewer._photoTagger.TransformPoint = new Point(x, y);
        }

        /// <summary>
        /// Handler to change the control layout when FittingPhotoToWindow changes so that
        /// fit to window does indeed cause the photo to fit to window.
        /// </summary>
        /// <param name="newValue">The new FittingPhotoToWindow value.</param>
        protected static void OnTaggingPhotoChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            PhotoViewerControl photoViewer = sender as PhotoViewerControl;

            if (photoViewer != null)
            {
                if ((bool)e.NewValue)
                {
                    photoViewer._photoDisplay.PhotoImage.Cursor = Cursors.Cross;
                }
                else
                {
                    photoViewer._photoDisplay.PhotoImage.Cursor = Cursors.Arrow;
                    photoViewer._tagTarget.Visibility = Visibility.Collapsed;
                    photoViewer._photoTagger.Close();
                }
            }
        }

        /// <summary>
        /// Photo tagging has been canceled.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Event args.</param>
        private void PhotoTagger_TagsCanceledEvent(object sender, EventArgs e)
        {
            if (this.TaggingPhoto)
            {
                this.TaggingPhoto = false;
            }
        }

        /// <summary>
        /// Photo has been tagged.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Event args.</param>
        private void PhotoTagger_TagsUpdatedEvent(object sender, TagsUpdatedArgs e)
        {
            if (this.TaggingPhoto)
            {
                this.TaggingPhoto = false;

                FacebookPhoto photo = (FacebookPhoto)this.DataContext;

                if (photo != null)
                {
                    Point pt = this._tagTarget.TransformPoint;

                    MatrixTransform mt = (MatrixTransform)this._photoDisplay.TransformToVisual(this._photoDisplay.PhotoImage);

                    pt = new Point(
                        (this._photoScrollViewer.TranslatePoint(
                            this._tagTarget.TransformPoint, this._photoDisplay.PhotoImage).X -
                            this._photoScrollViewer.HorizontalOffset * mt.Matrix.M11) /
                            this._photoDisplay.PhotoImage.ActualWidth,
                        (this._photoScrollViewer.TranslatePoint(
                            this._tagTarget.TransformPoint, this._photoDisplay.PhotoImage).Y -
                            this._photoScrollViewer.VerticalOffset * mt.Matrix.M22) /
                            this._photoDisplay.PhotoImage.ActualHeight
                        );

                    // Add photo tag
                    ClientManager.ServiceProvider.ViewManager.AddTagToPhoto(
                        photo,
                        e.SelectedContact,
                        pt);
                }
            }
        }

        /// <summary>
        /// Tag target scale has changed, reposition.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Event args.</param>
        private void PhotoDisplay_PhotoStateChanged(object sender, EventArgs e)
        {
            if (this.TaggingPhoto)
            {
                this.TaggingPhoto = false;
            }

            this._tagTarget.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Hide the tag target, or show it and position it accordingly.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Event args.</param>
        private static void OnIsMouseOverTagCommand(object sender, ExecutedRoutedEventArgs e)
        {
            PhotoViewerControl photoViewer = sender as PhotoViewerControl;

            if (photoViewer != null && !photoViewer.TaggingPhoto)
            {
                if (e.Parameter == null)
                {
                    // Hide tag target
                    photoViewer._tagTarget.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Position tag and show
                    Point pt = (Point)e.Parameter;

                    if (pt != null)
                    {
                        MatrixTransform mt = (MatrixTransform)photoViewer._photoDisplay.TransformToVisual(photoViewer._photoDisplay.PhotoImage);

                        photoViewer._tagTarget.Scale = Math.Sqrt(1 / mt.Matrix.M11);

                        double widthByTwo = photoViewer._tagTarget.Width / 2;
                        double heightByTwo = photoViewer._tagTarget.Height / 2;

                        //Point transPt = mt.Transform(new Point(widthByTwo, heightByTwo));
                        Point transPt = new Point(widthByTwo * mt.Matrix.M11, heightByTwo * mt.Matrix.M22);

                        // Convert point from percentage to pixels
                        pt = new Point(pt.X * photoViewer._photoDisplay.PhotoImage.ActualWidth +
                            photoViewer._photoScrollViewer.HorizontalOffset * mt.Matrix.M11,
                            pt.Y * photoViewer._photoDisplay.PhotoImage.ActualHeight +
                            photoViewer._photoScrollViewer.VerticalOffset * mt.Matrix.M22);

                        double left;
                        double top;

                        // Ensure tag target element does not go outside of photo boundry
                        if (pt.X < transPt.X)
                        {
                            left = transPt.X;
                        }
                        else if (pt.X > (photoViewer._photoDisplay.PhotoImage.ActualWidth - transPt.X))
                        {
                            left = photoViewer._photoDisplay.PhotoImage.ActualWidth - transPt.X;
                        }
                        else
                        {
                            left = pt.X;
                        }

                        // Ensure tag target element does not go outside of photo boundry
                        if (pt.Y < transPt.Y)
                        {
                            top = transPt.Y;
                        }
                        else if (pt.Y > (photoViewer._photoDisplay.PhotoImage.ActualHeight - transPt.Y))
                        {
                            top = photoViewer._photoDisplay.PhotoImage.ActualHeight - transPt.Y;
                        }
                        else
                        {
                            top = pt.Y;
                        }

                        // Set new coordinates
                        transPt = photoViewer._photoDisplay.PhotoImage.TranslatePoint(new Point(left, top), photoViewer._photoScrollViewer);
                        photoViewer._tagTarget.TransformPoint = transPt;

                        photoViewer._tagTarget.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void _StartSlideShow()
        {
            ((FacebookClientApplication)Application.Current).SwitchToSlideShow(FacebookPhoto.Album.Photos, FacebookPhoto.Album.Photos.IndexOf(FacebookPhoto));
        }
    }
}