namespace ClientManager.View
{
    using System;
    using System.Collections;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Interop;
    using Contigo;
    using Microsoft.Communications.Contacts;
    using Microsoft.Windows.Shell;
    using Standard;
    using FacebookClient;

    public sealed class ActionCommands
    {
        public ActionCommands(ViewManager viewManager)
        {
            Assert.IsNotNull(viewManager);
            foreach (PropertyInfo publicInstanceProperty in typeof(ActionCommands).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                ConstructorInfo cons = publicInstanceProperty.PropertyType.GetConstructor(new[] { typeof(ViewManager) });
                Assert.IsNotNull(cons);
                object command = cons.Invoke(new object[] { viewManager });
                Assert.AreEqual(command.GetType().Name, publicInstanceProperty.Name);
                publicInstanceProperty.SetValue(this, command, null);
            }
        }

        public AddCommentCommand AddCommentCommand { get; private set; }
        public AddCommentToPhotoCommand AddCommentToPhotoCommand { get; private set; }
        public AddLikeCommand AddLikeCommand { get; private set; }
        public CopyItemCommand CopyItemCommand { get; private set; }
        public GetMoreCommentsCommand GetMoreCommentsCommand { get; private set; }
        public MarkAsReadCommand MarkAsReadCommand { get; private set; }
        public MarkAllAsReadCommand MarkAllAsReadCommand { get; private set; }
        public RemoveCommentCommand RemoveCommentCommand { get; private set; }
        public RemoveLikeCommand RemoveLikeCommand { get; private set; }
        public SetNewsFeedFilterCommand SetNewsFeedFilterCommand { get; private set; }
        public SetSortOrderCommand SetSortOrderCommand { get; private set; }
        public StartSyncCommand StartSyncCommand { get; private set; }
        public WriteOnWallCommand WriteOnWallCommand { get; private set; }
        public ShowPhotoUploadWizardCommand ShowPhotoUploadWizardCommand { get; private set; }

        public SaveAlbumCommand SaveAlbumCommand { get; private set; }
        public SavePhotoCommand SavePhotoCommand { get; private set; }

        public UpdateWindowsLogonPictureCommand UpdateWindowsLogonPictureCommand { get; private set; }

        // TODO: These really belong on MainWindowCommands, but that's not currently properly exposed to XAML
        public CloseWindowCommand CloseWindowCommand { get; private set; }
        public MinimizeWindowCommand MinimizeWindowCommand { get; private set; }
        public MaximizeWindowCommand MaximizeWindowCommand { get; private set; }
        public RestoreWindowCommand RestoreWindowCommand { get; private set; }
    }

    public abstract class ActionCommand : ViewCommand
    {
        protected ActionCommand(ViewManager viewManager)
            : base(viewManager)
        {}

        protected sealed override void ExecuteInternal(object parameter)
        {
            PerformAction(parameter);
        }

        protected abstract void PerformAction(object parameter);
    }

    public sealed class ShowPhotoUploadWizardCommand : ActionCommand
    {
        public ShowPhotoUploadWizardCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override void PerformAction(object parameter)
        {
            FacebookClientApplication.Current2.MainWindow.ShowUploadWizard(parameter as string);
        }
    }

    public sealed class AddLikeCommand : ActionCommand
    {
        public AddLikeCommand(ViewManager viewManager)
            : base(viewManager)
        {}

        protected override bool CanExecuteInternal(object parameter)
        {
            if (parameter == null)
            {
                return false;
            }

            ActivityPost activityPost = parameter as ActivityPost;
            return activityPost.CanLike && !activityPost.HasLiked;
        }

        protected override void PerformAction(object parameter)
        {
            ActivityPost activityPost = parameter as ActivityPost;
            ServiceProvider.FacebookService.AddLike(activityPost);
        }
    }

    public sealed class RemoveLikeCommand : ActionCommand
    {
        public RemoveLikeCommand(ViewManager viewManager)
            : base(viewManager)
        {
        }

        protected override bool CanExecuteInternal(object parameter)
        {
            if (parameter == null)
            {
                return false;
            }

            ActivityPost activityPost = parameter as ActivityPost;
            return activityPost.HasLiked;
        }

        protected override void PerformAction(object parameter)
        {
            ActivityPost activityPost = parameter as ActivityPost;
            ServiceProvider.FacebookService.RemoveLike(activityPost);
        }
    }

    public sealed class GetMoreCommentsCommand : ActionCommand
    {
        public GetMoreCommentsCommand(ViewManager viewManager)
            : base(viewManager)
        {
        }

        protected override bool CanExecuteInternal(object parameter)
        {
            if (parameter == null)
            {
                return false;
            }

            ActivityPost activityPost = parameter as ActivityPost;
            return activityPost.HasMoreComments;
        }

        protected override void PerformAction(object parameter)
        {
            ActivityPost activityPost = parameter as ActivityPost;
            ServiceProvider.FacebookService.GetMoreComments(activityPost);
        }
    }

    public sealed class AddCommentCommand : ActionCommand
    {
        public AddCommentCommand(ViewManager viewManager)
            : base(viewManager)
        {
        }

        protected override bool CanExecuteInternal(object parameter)
        {
            if (parameter == null)
            {
                return false;
            }

            object[] parameterList = parameter as object[];
            ActivityPost activityPost = parameterList[0] as ActivityPost;

            if (activityPost == null)
            {
                return false;
            }

            return activityPost.CanComment;
        }

        protected override void PerformAction(object parameter)
        {
            object[] parameterList = parameter as object[];
            ActivityPost activityPost = parameterList[0] as ActivityPost;
            string comment = parameterList[1] as string;
            ServiceProvider.FacebookService.AddComment(activityPost, comment);
        }
    }

    public sealed class RemoveCommentCommand : ActionCommand
    {
        public RemoveCommentCommand(ViewManager viewManager)
            : base(viewManager)
        {
        }

        protected override bool CanExecuteInternal(object parameter)
        {
            ActivityComment comment = parameter as ActivityComment;

            if (comment == null)
            {
                return false;
            }

            return comment.IsMine;
        }

        protected override void PerformAction(object parameter)
        {
            ActivityComment comment = parameter as ActivityComment;
            ServiceProvider.FacebookService.RemoveComment(comment);
        }
    }

    public sealed class WriteOnWallCommand : ActionCommand
    {
        public WriteOnWallCommand(ViewManager viewManager)
            : base(viewManager)
        {}

        protected override bool CanExecuteInternal(object parameter)
        {
            object[] parameterList = parameter as object[];
            return parameterList != null && parameterList.Length == 2 &&
                parameterList[0] is FacebookContact && parameterList[1] is string;
        }

        protected override void PerformAction(object parameter)
        {
            object[] parameterList = parameter as object[];
            FacebookContact contact = parameterList[0] as FacebookContact;
            string comment = parameterList[1] as string;

            ServiceProvider.FacebookService.WriteOnWall(contact, comment);
        }
    }

    public sealed class SaveAlbumCommand : ActionCommand
    {
        public SaveAlbumCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override bool CanExecuteInternal(object parameter)
        {
            return parameter is FacebookPhotoAlbum;
        }

        protected override void PerformAction(object parameter)
        {
            var album = parameter as FacebookPhotoAlbum;
            if (album == null)
            {
                return;
            }

            string folderPath = null;
            if (Utility.IsOSVistaOrNewer)
            {
                IFileOpenDialog pFolderDialog = null;
                try
                {
                    pFolderDialog = CLSID.CoCreateInstance<IFileOpenDialog>(CLSID.FileOpenDialog);
                    pFolderDialog.SetOptions(pFolderDialog.GetOptions() | FOS.NOREADONLYRETURN | FOS.PICKFOLDERS);
                    pFolderDialog.SetTitle(string.Format("Select where to save \"{0}\"", album.Title));
                    pFolderDialog.SetOkButtonLabel("Save Album");

                    HRESULT hr = pFolderDialog.Show(new WindowInteropHelper(Application.Current.MainWindow).Handle);
                    if (hr.Failed)
                    {
                        return;
                    }

                    IShellItem pItem = null;
                    try
                    {
                        pItem = pFolderDialog.GetResult();
                        folderPath = ShellUtil.GetPathFromShellItem(pItem);
                    }
                    finally
                    {
                        Utility.SafeRelease(ref pItem);
                    }
                }
                finally
                {
                    Utility.SafeRelease(ref pFolderDialog);
                }
            }
            else
            {
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Choose where to save the album.",
                    ShowNewFolderButton = true,
                };
                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                folderPath = folderDialog.SelectedPath;
            }

            album.SaveToFolder(folderPath, _OnAlbumSaveProgressCallback, null);
            Process.Start(new ProcessStartInfo { FileName = folderPath });
        }

        private void _OnAlbumSaveProgressCallback(object sender, SaveImageCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled)
            {
                //FacebookClientApplication.Current2.MainWindow.SetTaskbarProgress(1F);
                return;
            }

            //FacebookClientApplication.Current2.MainWindow.SetTaskbarProgress((float)e.CurrentImageIndex / (float)e.TotalImageCount);
        }
    }

    public sealed class SavePhotoCommand : ActionCommand
    {
        private static readonly Guid _SavePhotoId = new Guid(0x8c18c882, 0x482b, 0x459d, 0x9a, 0xc6, 0x6c, 0xc5, 0x39, 0x71, 0xa7, 0xf7);

        public SavePhotoCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override bool CanExecuteInternal(object parameter)
        {
            return parameter is FacebookPhoto;
        }

        protected override void PerformAction(object parameter)
        {
            var photo = parameter as FacebookPhoto;
            if (photo == null)
            {
                return;
            }

            string defaultFileName = "Facebook Photo";
            if (photo.Album != null)
            {
                defaultFileName = photo.Album.Title + " (" + (photo.Album.Photos.IndexOf(photo) + 1) + ")";
            }

            string filePath = null;
            if (Utility.IsOSVistaOrNewer)
            {
                IFileSaveDialog pFileSaveDialog = null;
                try
                {
                    pFileSaveDialog = CLSID.CoCreateInstance<IFileSaveDialog>(CLSID.FileSaveDialog);
                    pFileSaveDialog.SetOptions(pFileSaveDialog.GetOptions() | FOS.FORCEFILESYSTEM | FOS.OVERWRITEPROMPT);
                    pFileSaveDialog.SetTitle("Select where to save the photo");
                    pFileSaveDialog.SetOkButtonLabel("Save Photo");
                    var filterspec = new COMDLG_FILTERSPEC { pszName = "Images", pszSpec = "*.jpg;*.png;*.bmp;*.gif" };
                    pFileSaveDialog.SetFileTypes(1, ref filterspec);
                    pFileSaveDialog.SetFileName(defaultFileName);
                    Guid clientId = _SavePhotoId;
                    pFileSaveDialog.SetClientGuid(ref clientId);

                    IShellItem pItem = null;
                    try
                    {
                        pItem = ShellUtil.GetShellItemForPath(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
                        pFileSaveDialog.SetDefaultFolder(pItem);
                    }
                    finally
                    {
                        Utility.SafeRelease(ref pItem);
                    }

                    HRESULT hr = pFileSaveDialog.Show(new WindowInteropHelper(Application.Current.MainWindow).Handle);
                    if (hr.Failed)
                    {
                        Assert.AreEqual((HRESULT)Win32Error.ERROR_CANCELLED, hr);
                        return;
                    }

                    pItem = null;
                    try
                    {
                        pItem = pFileSaveDialog.GetResult();
                        filePath = ShellUtil.GetPathFromShellItem(pItem);
                    }
                    finally
                    {
                        Utility.SafeRelease(ref pItem);
                    }
                }
                finally
                {
                    Utility.SafeRelease(ref pFileSaveDialog);
                }
            }
            else
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Image Files|*.jpg;*.png;*.bmp;*.gif",
                    FileName = defaultFileName,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                };

                if (saveFileDialog.ShowDialog(Application.Current.MainWindow) != true)
                {
                    return;
                }

                filePath = saveFileDialog.FileName;
            }

            FacebookImageSaveOptions fiso = FacebookImageSaveOptions.FindBetterName;

            // We told the file dialog to prompt about overwriting, so if the user specified a location
            // with a file extension and the file already exists, prepare to overwrite.
            // This isn't quite right because the file extension may be different, so we may overwrite a jpg 
            // when it was asked to be a gif, but it's not a likely scenario.
            if (System.IO.File.Exists(filePath))
            {
                fiso = FacebookImageSaveOptions.Overwrite;
            }

            photo.Image.SaveToFile(FacebookImageDimensions.Big, filePath, true, fiso, _OnPhotoSaveProgressCallback, null);
        }

        private void _OnPhotoSaveProgressCallback(object sender, SaveImageCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled)
            {
                //FacebookClientApplication.Current2.MainWindow.SetTaskbarProgress(1F);
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = e.ImagePath });

            //FacebookClientApplication.Current2.MainWindow.SetTaskbarProgress((float)e.CurrentImageIndex / (float)e.TotalImageCount);
        }
    }

    public sealed class CopyItemCommand : ActionCommand
    {
        public CopyItemCommand(ViewManager viewManager)
            : base(viewManager)
        {}

        protected override bool CanExecuteInternal(object parameter)
        {
            if (parameter == null)
            {
                return false;
            }

            ActivityPost activityPost = parameter as ActivityPost;
            return true;
        }

        protected override void PerformAction(object parameter)
        {
            ActivityPost activityPost = parameter as ActivityPost;

            String fullitemstring = activityPost.Actor + ": " + activityPost.Message + " " + activityPost.Updated.ToString();

            Clipboard.SetData(DataFormats.Text, fullitemstring);
        }
    }

    public sealed class UpdateWindowsLogonPictureCommand : ActionCommand
    {
        private ContactManager _contactManager = new ContactManager();

        public UpdateWindowsLogonPictureCommand(ViewManager viewManager)
            : base(viewManager)
        { }
        
        protected override bool CanExecuteInternal(object parameter)
        {
            return parameter is FacebookContact;
        }

        protected override void PerformAction(object parameter)
        {
            var contact = parameter as FacebookContact;
            if (contact != null)
            {
                contact.Image.SaveToFile(FacebookImageDimensions.Big, Path.GetTempFileName(), true, FacebookImageSaveOptions.Overwrite, _OnImageSaved, contact);
            }
        }

        private void _OnImageSaved(object sender, SaveImageCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error != null)
            {
                return;
            }

            _contactManager.Dispatcher.BeginInvoke((Action)(() =>
            {
                try
                {
                    var fbMeContact = (FacebookContact)e.UserState;
                    Contact meContact = _contactManager.MeContact;
                    if (meContact == null)
                    {
                        meContact = _contactManager.CreateContact();
                        meContact.Names.Default = new Name(fbMeContact.Name);
                        meContact.CommitChanges();
                        _contactManager.MeContact = meContact;
                    }

                    using (var fs = new FileStream(e.ImagePath, FileMode.Open))
                    {
                        meContact.Photos[PhotoLabels.UserTile] = new Photo(fs, "image/jpeg");
                        meContact.CommitChanges();
                    }
                }
                catch (IOException)
                {
                    // This is just an opportunistic operation.  Failure is acceptable.
                }
            }), null);
        }
    }

    public sealed class StartSyncCommand : ViewCommand
    {
        public StartSyncCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override void ExecuteInternal(object parameter)
        {
            ServiceProvider.FacebookService.Refresh();
        }
    }

    public sealed class MarkAsReadCommand : ViewCommand
    {
        public MarkAsReadCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override bool CanExecuteInternal(object parameter)
        {
            var notification = parameter as Notification;
            return notification != null && notification.IsUnread;
        }

        protected override void ExecuteInternal(object parameter)
        {
            var notification = parameter as Notification;
            if (notification != null)
            {
                ServiceProvider.FacebookService.ReadNotification(notification);
            }
        }
    }

    public sealed class MarkAllAsReadCommand : ViewCommand
    {
        public MarkAllAsReadCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override bool CanExecuteInternal(object parameter)
        {
            return parameter is IEnumerable;
        }

        protected override void ExecuteInternal(object parameter)
        {
            var enumerable = parameter as IEnumerable;
            if (enumerable == null)
            {
                return;
            }

            foreach (var notification in enumerable.OfType<Notification>())
            {
                ServiceProvider.FacebookService.ReadNotification(notification);
            }
        }
    }

    public sealed class SetNewsFeedFilterCommand : ViewCommand
    {
        public SetNewsFeedFilterCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override bool CanExecuteInternal(object parameter)
        {
            return parameter == null || parameter is ActivityFilter;
        }

        protected override void ExecuteInternal(object parameter)
        {
            var filter = parameter as ActivityFilter;
            ServiceProvider.FacebookService.NewsFeedFilter = filter;
        }
    }

    public sealed class SetSortOrderCommand : ViewCommand
    {
        public SetSortOrderCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override bool CanExecuteInternal(object parameter)
        {
            return parameter is PhotoAlbumSortOrder || parameter is ContactSortOrder;
        }

        protected override void ExecuteInternal(object parameter)
        {
            if (parameter is PhotoAlbumSortOrder)
            {
                var sortOrder = (PhotoAlbumSortOrder)parameter;
                ServiceProvider.FacebookService.PhotoAlbumSortOrder = sortOrder;
            }
            else if (parameter is ContactSortOrder)
            {
                var sortOrder = (ContactSortOrder)parameter;
                ServiceProvider.FacebookService.ContactSortOrder = sortOrder;
            }
        }
    }

    public sealed class AddCommentToPhotoCommand : ViewCommand
    {
        public AddCommentToPhotoCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override bool CanExecuteInternal(object parameter)
        {
            object[] parameters = parameter as object[];
            if (parameters == null || parameters.Length != 2)
            {
                return false;
            }

            FacebookPhoto photo = parameters[0] as FacebookPhoto;
            string comment = parameters[1] as string;
            return photo != null && photo.CanComment && !string.IsNullOrEmpty(comment);
        }

        protected override void ExecuteInternal(object parameter)
        {
            object[] parameters = parameter as object[];
            FacebookPhoto photo = parameters[0] as FacebookPhoto;
            string comment = parameters[1] as string;

            if (photo != null && photo.CanComment && !string.IsNullOrEmpty(comment))
            {
                ServiceProvider.FacebookService.AddComment(photo, comment);
            }
        }
    }

    public sealed class CloseWindowCommand : ActionCommand
    {
        public CloseWindowCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override bool CanExecuteInternal(object parameter)
        {
            return true;
        }

        protected override void  PerformAction(object parameter)
        {
            SystemCommands.CloseWindow(Application.Current.MainWindow);
        }
    }

    public sealed class MinimizeWindowCommand : ActionCommand
    {
        public MinimizeWindowCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override bool CanExecuteInternal(object parameter)
        {
            return true;
        }

        protected override void PerformAction(object parameter)
        {
            SystemCommands.MinimizeWindow(Application.Current.MainWindow);
        }
    }

    public sealed class MaximizeWindowCommand : ActionCommand
    {
        public MaximizeWindowCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override void PerformAction(object parameter)
        {
            SystemCommands.MaximizeWindow(Application.Current.MainWindow);
        }
    }

    public sealed class RestoreWindowCommand : ActionCommand
    {
        public RestoreWindowCommand(ViewManager viewManager)
            : base(viewManager)
        { }

        protected override void PerformAction(object parameter)
        {
            SystemCommands.RestoreWindow(Application.Current.MainWindow);
        }
    }
}
