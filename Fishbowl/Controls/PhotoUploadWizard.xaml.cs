namespace FacebookClient
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media.Imaging;
    using ClientManager;
    using ClientManager.Controls;
    using Contigo;
    using Standard;

    /// <summary>
    /// Interaction logic for PhotoUploadWizard.xaml
    /// </summary>
    [TemplatePart(Name = "PART_AlbumPickerPage", Type = typeof(Panel))]
    [TemplatePart(Name = "PART_UploadProgressPage", Type = typeof(Panel))]
    [TemplatePart(Name = "PART_ZapScroller", Type = typeof(ZapScroller))]
    [TemplatePart(Name = "PART_AlbumName", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_AlbumLocation", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_AlbumDescription", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_NextPhotoImage", Type = typeof(Image))]
    [TemplatePart(Name = "PART_UploadStatus", Type = typeof(TextBlock))]
    [TemplatePart(Name = "PART_CloseCancelButton", Type = typeof(Button))]
    public partial class PhotoUploadWizard : UserControl
    {
        private static readonly string[] _ImageExtensions = new string[] { ".jpg", ".jpeg", ".bmp", ".png", ".gif" };

        public class UploadFile
        {
            public UploadFile(string path)
            {
                Path = path;
                Description = string.Empty;
            }

            public string Path { get; private set; }
            public string Description { get; set; }
        }

        private class NewPhotoAlbum
        {
            public string Title { get { return "New Album"; } }
            public string Location { get { return string.Empty; } }
            public string Description { get { return string.Empty; } }
            public FacebookPhoto CoverPic { get { return null; } }
        }

        public enum PhotoUploaderPage
        {
            PickAlbum,
            Upload
        }

        public static readonly DependencyProperty FileCountProperty = DependencyProperty.Register(
            "FileCount",
            typeof(int),
            typeof(PhotoUploadWizard),
            new FrameworkPropertyMetadata(default(int)));

        public int FileCount
        {
            get { return (int)GetValue(FileCountProperty); }
            set { SetValue(FileCountProperty, value); }
        }

        public static readonly DependencyProperty UploadAlbumNameProperty = DependencyProperty.Register(
            "UploadAlbumName",
            typeof(string),
            typeof(PhotoUploadWizard),
            new FrameworkPropertyMetadata(""));

        public string UploadAlbumName
        {
            get { return (string)GetValue(UploadAlbumNameProperty); }
            set { SetValue(UploadAlbumNameProperty, value); }
        }

        public static readonly DependencyProperty UploadCountProperty = DependencyProperty.Register(
            "UploadCount",
            typeof(int),
            typeof(PhotoUploadWizard),
            new FrameworkPropertyMetadata(default(int)));

        public int UploadCount
        {
            get { return (int)GetValue(UploadCountProperty); }
            set { SetValue(UploadCountProperty, value); }
        }

        public static readonly DependencyProperty PageProperty = DependencyProperty.Register(
            "Page",
            typeof(PhotoUploaderPage),
            typeof(PhotoUploadWizard),
            new FrameworkPropertyMetadata(
                (d, e) => ((PhotoUploadWizard)d)._OnPagePropertyChanged(e)));

        public PhotoUploaderPage Page
        {
            get { return (PhotoUploaderPage)GetValue(PageProperty); }
            set { SetValue(PageProperty, value); }
        }

        private Panel _albumPickerPage;
        private Panel _uploadProgressPage;
        private ZapScroller _zapScroller;
        private ComboBox _albumsComboBox;
        private TextBox _albumName;
        private TextBox _albumLocation;
        private TextBox _albumDescription;
        private Image _nextPhotoImage;
        private TextBlock _uploadStatus;
        private TextBlock _uploadPhotoStatusTextBlock;
        private Button _closeCancelButton;
        private string _defaultCurrentAlbum;

        private Thread _workerThread;

        public PhotoUploadWizard()
        {
            InitializeComponent();

            CommandBindings.Add(new CommandBinding(UploadCommand, new ExecutedRoutedEventHandler(OnUploadExecuted), new CanExecuteRoutedEventHandler(OnUploadCanExecute)));
            Files = new ObservableCollection<UploadFile>();

            ServiceProvider.ViewManager.MeContact.PhotoAlbums.CollectionChanged += new NotifyCollectionChangedEventHandler((sender, e) =>
            {
                _UpdatePhotoAlbums();
            });
        }

        public static RoutedCommand SelectedAlbumChangedCommand = new RoutedCommand("SelectedAlbumChanged", typeof(PhotoUploadWizard));
        public static RoutedCommand UploadCommand = new RoutedCommand("Upload", typeof(PhotoUploadWizard));

        public ObservableCollection<UploadFile> Files { get; private set; }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _uploadPhotoStatusTextBlock = Template.FindName("PART_UploadPhotoStatusTextBlock", this) as TextBlock;
            _albumPickerPage = Template.FindName("PART_AlbumPickerPage", this) as Panel;
            _uploadProgressPage = Template.FindName("PART_UploadProgressPage", this) as Panel;
            _zapScroller = Template.FindName("PART_ZapScroller", this) as ZapScroller;
            _albumsComboBox = Template.FindName("PART_AlbumsComboBox", this) as ComboBox;
            _albumName = Template.FindName("PART_AlbumName", this) as TextBox;
            _albumLocation = Template.FindName("PART_AlbumLocation", this) as TextBox;
            _albumDescription = Template.FindName("PART_AlbumDescription", this) as TextBox;
            _nextPhotoImage = Template.FindName("PART_NextPhotoImage", this) as Image;
            _uploadStatus = Template.FindName("PART_UploadStatus", this) as TextBlock;
            _closeCancelButton = Template.FindName("PART_CloseCancelButton", this) as Button;

            _albumName.TextChanged += new TextChangedEventHandler((sender, e) => CommandManager.InvalidateRequerySuggested());
            _albumLocation.TextChanged += new TextChangedEventHandler((sender, e) => CommandManager.InvalidateRequerySuggested());
            _albumDescription.TextChanged += new TextChangedEventHandler((sender, e) => CommandManager.InvalidateRequerySuggested());
            
            _UpdatePhotoAlbums();
        }

        public void Show(IEnumerable<string> fileList)
        {
            if (CheckWorkerThread())
            {
                Files.Clear();
                Files.AddRange(from fileName in fileList select new UploadFile(fileName));
                ServiceProvider.ViewManager.ShowDialog(this);
                Page = PhotoUploaderPage.PickAlbum;

                _UpdatePhotoAlbums();
            }
        }

        public void Hide()
        {
            if (CheckWorkerThread())
            {
                ServiceProvider.ViewManager.EndDialog(this);
                FacebookClientApplication.Current2.MainWindow.ClearTaskbarProgress();
                Files.Clear();
            }
        }

        private bool CheckWorkerThread()
        {
            if (_workerThread != null)
            {
                if (!_workerThread.IsAlive)
                {
                    _workerThread = null;
                }
                else
                {
                    _workerThread.Abort();
                    _workerThread.Join();
                    _workerThread = null;
                }
            }

            return true;
        }

        private void _UpdatePhotoAlbums()
        {
            if (_albumsComboBox != null)
            {
                _albumsComboBox.Items.Clear();
                _albumsComboBox.Items.Add(new NewPhotoAlbum());

                int currentIndex = 1;
                int defaultIndex = 0;
                foreach (var album in from myAlbum in ServiceProvider.ViewManager.MeContact.PhotoAlbums
                                      where myAlbum.CanAddPhotos
                                      select myAlbum)
                {
                    if (album.Title.Equals(_defaultCurrentAlbum))
                    {
                        defaultIndex = currentIndex;
                    }

                    _albumsComboBox.Items.Add(album);
                    ++currentIndex;
                }

                _albumsComboBox.SelectedIndex = defaultIndex;
            }
        }

        private void _OnPagePropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((PhotoUploaderPage)e.NewValue == PhotoUploaderPage.PickAlbum)
            {
                _UpdatePhotoAlbums();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void OnUploadExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Assert.IsNull(_workerThread);

            string albumName = _albumName.Text;
            string albumDescription = _albumDescription.Text;
            string albumLocation = _albumLocation.Text;

            FacebookPhotoAlbum album = _albumsComboBox.SelectedItem as FacebookPhotoAlbum;

            Page = PhotoUploaderPage.Upload;
            UploadAlbumName = album != null ? album.Title : albumName;
            _closeCancelButton.Content = "Cancel";
            UploadCount = 0;
            FileCount = Files.Count;

            FacebookClientApplication.Current2.MainWindow.SetTaskbarProgress(0);

            _uploadStatus.Text = "";

            // This is terrible, but for some reason I can't get StringFormat to work correctly with the MultiBinding in this case.
            // This needs to be looked into next time there's any investment made into the PhotoUploadWizard.
            // (There are other things here that are also terrible (i.e. not localizable, not designable) so this isn't making anything worse...)
            _uploadPhotoStatusTextBlock.Text = "Uploading photo " + (UploadCount + 1) + " of " + FileCount + " to album " + UploadAlbumName;

            _workerThread = new Thread(new ThreadStart(() =>
            {
                try
                {
                    if (album == null)
                    {
                        album = ServiceProvider.ViewManager.CreatePhotoAlbum(albumName, albumDescription, albumLocation);
                    }

                    int count = 0;

                    foreach (var file in Files)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            BitmapImage image = new BitmapImage(new Uri(file.Path));
                            image.Freeze();
                            _nextPhotoImage.Source = image;
                        }));

                        string path = DragContainer.ConstrainImage(file.Path, FacebookImage.GetDimensionSize(FacebookImageDimensions.Big));
                        ServiceProvider.ViewManager.AddPhotoToAlbum(album, file.Description, path);
                        count++;

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UploadCount = count;
                            _uploadPhotoStatusTextBlock.Text = "Uploading photo " + (UploadCount + 1) + " of " + FileCount + " to album " + UploadAlbumName;
                            FacebookClientApplication.Current2.MainWindow.SetTaskbarProgress((float)UploadCount / Files.Count);
                        }));
                    }

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ServiceProvider.ViewManager.NavigationCommands.NavigateToContentCommand.Execute(album);
                        FacebookClientApplication.Current2.MainWindow.ClearTaskbarProgress();
                        Hide();
                    }));
                }
                catch (ThreadAbortException)
                {
                }
                catch (Exception)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _nextPhotoImage.Source = null;
                        _uploadStatus.Text = "Upload failed.";
                        _closeCancelButton.Content = "Close";
                    }));
                }
            }));
            _workerThread.Start();
        }

        private void OnUploadCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (_albumName == null || _albumName.Text.Length == 0)
            {
                e.CanExecute = false;
                return;
            }

            if (_albumsComboBox.SelectedItem is FacebookPhotoAlbum)
            {
                e.CanExecute = true;
                return;
            }

            foreach (var album in ServiceProvider.ViewManager.MeContact.PhotoAlbums) // this isn't quite right.. we might not have all the albums yet.
                                                                                     // this is a good quick check, though. we'll fail later if we don't catch it here.
            {
                if (album.Title == _albumName.Text)
                {
                    e.CanExecute = false;
                    return;
                }
            }

            e.CanExecute = true;
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void RemovePhotoButtonClick(object sender, RoutedEventArgs e)
        {
            if (_zapScroller.CurrentItemIndex >= 0 && _zapScroller.CurrentItemIndex < Files.Count)
            {
                Files.RemoveAt(_zapScroller.CurrentItemIndex);
                if (Files.Count == 0)
                {
                    Hide();
                }
            }
        }

        private static bool _IsImageFile(string fileName)
        {
            Assert.IsNeitherNullNorEmpty(fileName);
            string ext = Path.GetExtension(fileName).ToLower();
            foreach (string imgExt in _ImageExtensions)
            {
                if (imgExt == ext)
                {
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<string> _GetImageFiles(string[] fileNames, int maxFiles)
        {
            var directories = new List<string>();
            foreach (string file in fileNames)
            {
                if (File.Exists(file))
                {
                    if (_IsImageFile(file))
                    {
                        yield return file;
                        --maxFiles;
                        if (maxFiles == 0)
                        {
                            yield break;
                        }
                    }
                }
                else if (Directory.Exists(file))
                {
                    directories.Add(file);
                }
            }

            // We've processed all the top-level files.
            // If there are still files to be gotten then start walking the directories.
            foreach (string directory in directories)
            {
                DirectoryInfo di = new DirectoryInfo(directory);
                foreach (string imgExt in _ImageExtensions)
                {
                    foreach (FileInfo fi in FileWalker.GetFiles(di, "*" + imgExt, true))
                    {
                        yield return fi.FullName;
                        --maxFiles;
                        if (maxFiles == 0)
                        {
                            yield break;
                        }
                    }
                }
            }
        }

        public static List<string> FindImageFiles(string[] fileNames)
        {
            return new List<string>(_GetImageFiles(fileNames, 50));
        }

        public void SetDefaultAlbum(string albumName)
        {
            _defaultCurrentAlbum = albumName;
        }
    }
}
