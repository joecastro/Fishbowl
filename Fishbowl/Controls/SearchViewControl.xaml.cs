namespace FacebookClient
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using ClientManager.Controls;
    using Contigo;
    using Standard;

    public enum SearchViewMode
    {
        List,
        Explorer
    }

    public partial class SearchViewControl : UserControl
    {
        public static RoutedCommand SwitchToListViewCommand { get; private set; }
        public static RoutedCommand SwitchToPhotoExplorerCommand { get; private set; }

        private SearchViewMode displayMode = SearchViewMode.List;

        private readonly DoubleAnimation _listViewOpacityAnimation = new DoubleAnimation
        {
            Duration = new Duration(new TimeSpan(0, 0, 0, 0, 250)),
        };

        private readonly DoubleAnimation _listViewTransformAnimation = new DoubleAnimation
        {
            Duration = new Duration(new TimeSpan(0, 0, 0, 0, 250)),
            AccelerationRatio = 0.4,
            DecelerationRatio = 0.2,
        };

        private readonly DoubleAnimation photoExplorerAnimation = new DoubleAnimation
        {
            Duration = new Duration(new TimeSpan(0, 0, 0, 0, 250)),
        };

        static SearchViewControl()
        {
            SwitchToListViewCommand = new RoutedCommand("SwitchToListView", typeof(SearchViewControl));
            SwitchToPhotoExplorerCommand = new RoutedCommand("SwitchToPhotoExplorer", typeof(SearchViewControl));
        }

        public SearchViewControl()
        {
            InitializeComponent();
            
            this.CommandBindings.Add(new CommandBinding(SwitchToListViewCommand, new ExecutedRoutedEventHandler(this.OnSwitchToListViewCommand), new CanExecuteRoutedEventHandler(this.OnSwitchToListViewCanExecute)));
            this.CommandBindings.Add(new CommandBinding(SwitchToPhotoExplorerCommand, new ExecutedRoutedEventHandler(this.OnSwitchToPhotoExplorerCommand), new CanExecuteRoutedEventHandler(this.OnSwitchToPhotoExplorerCanExecute)));

            _listViewOpacityAnimation.Completed += new EventHandler(this.OnListViewAnimationCompleted);
            photoExplorerAnimation.Completed += new EventHandler(this.OnPhotoExplorerAnimationCompleted);

            this.DataContextChanged += new DependencyPropertyChangedEventHandler(this.OnSearchViewControlDataContextChanged);
        }

        /// <summary>
        /// When the data context changes, updates the center node of the photo explorer if it is currently displayed, 
        /// and switches to the explorer if a tag search query beginning with 'explore:' is entered.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Arguments describing the event.</param>
        private void OnSearchViewControlDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SearchResults searchResults = this.DataContext as SearchResults;

            if (searchResults != null)
            {
                if (searchResults.SearchText.StartsWith("explore:", StringComparison.OrdinalIgnoreCase))
                {
                    this.OnSwitchToPhotoExplorerCommand(null, null);
                }
                else
                {
                    if (this.displayMode == SearchViewMode.Explorer)
                    {
                        this.SetPhotoExplorerCenterNodeToQuery();
                    }
                }
            }
        }

        private void OnSwitchToListViewCommand(object sender, ExecutedRoutedEventArgs e)
        {
            this.displayMode = SearchViewMode.List;

            this.photoExplorerAnimation.To = 0;
            this.PhotoExplorerGrid.BeginAnimation(OpacityProperty, this.photoExplorerAnimation);

            this.SearchListView.Visibility = Visibility.Visible;
            this._listViewOpacityAnimation.To = 1;
            this.SearchListView.BeginAnimation(OpacityProperty, this._listViewOpacityAnimation);
            this._listViewTransformAnimation.To = 1;
            this.ListViewTransform.BeginAnimation(ScaleTransform.ScaleXProperty, this._listViewTransformAnimation);
            this.ListViewTransform.BeginAnimation(ScaleTransform.ScaleYProperty, this._listViewTransformAnimation);
        }

        private void OnSwitchToListViewCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.displayMode == SearchViewMode.Explorer;
        }

        private void OnSwitchToPhotoExplorerCommand(object sender, ExecutedRoutedEventArgs e)
        {
            this.displayMode = SearchViewMode.Explorer;
            
            this.PhotoExplorerGrid.Visibility = Visibility.Visible;
            this.SetPhotoExplorerCenterNodeToQuery();
            this.photoExplorerAnimation.To = 1;
            this.PhotoExplorerGrid.BeginAnimation(OpacityProperty, this.photoExplorerAnimation);

            this._listViewOpacityAnimation.To = 0;
            this.SearchListView.BeginAnimation(OpacityProperty, this._listViewOpacityAnimation);

            this._listViewTransformAnimation.To = 0.85;
            this.ListViewTransform.BeginAnimation(ScaleTransform.ScaleXProperty, this._listViewTransformAnimation);
            this.ListViewTransform.BeginAnimation(ScaleTransform.ScaleYProperty, this._listViewTransformAnimation);
        }

        /// <summary>
        /// Determines whether the photo explorer can be switched to (that is, is not currently being displayed).
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Arguments describing the event.</param>
        private void OnSwitchToPhotoExplorerCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.displayMode == SearchViewMode.List;
        }

        /// <summary>
        /// Sets the center node of the photo explorer to the search query text, less any qualifiers.
        /// </summary>
        private void SetPhotoExplorerCenterNodeToQuery()
        {
            SearchResults searchResults = this.DataContext as SearchResults;

            if (searchResults != null)
            {
                if (searchResults.SearchText.StartsWith("tag:", StringComparison.OrdinalIgnoreCase) || searchResults.SearchText.StartsWith("explore:", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = searchResults.SearchText.Split(':');
                    this.PhotoExplorer.CenterNode = PhotoExplorerTagNode.CreateTagNodeFromTag(parts[1]);
                }
                else
                {
                    this.PhotoExplorer.CenterNode = new PhotoExplorerBaseNode(null, "search: " + searchResults.SearchText);

                    for (int i = 0; i < searchResults.Count && i < PhotoExplorerControl.MaximumDisplayedPhotos; i++)
                    {
                        this.PhotoExplorer.CenterNode.RelatedNodes.Add(PhotoExplorerBaseNode.CreateNodeFromObject(searchResults[i]));
                    }
                }
            }
        }

        /// <summary>
        /// Collapses the photo explorer once its animation completes if it was fading out.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Arguments describing the event.</param>
        private void OnPhotoExplorerAnimationCompleted(object sender, EventArgs e)
        {
            if (this.PhotoExplorer.Opacity == 0)
            {
                this.PhotoExplorerGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.PhotoExplorer.Focus();
            }
        }

        /// <summary>
        /// Collapses the list view once its animation completes if it was fading out.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Arguments describing the event.</param>
        private void OnListViewAnimationCompleted(object sender, EventArgs e)
        {
            if (this.SearchListView.Opacity == 0)
            {
                this.SearchListView.Visibility = Visibility.Collapsed;
            }
            else
            {
                Assert.AreEqual(PhotoExplorerGrid.Opacity, 0);
                PhotoExplorerGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void OnNavigateToContentButtonClicked(object sender, RoutedEventArgs e)
        {
            ClientManager.ServiceProvider.ViewManager.NavigateToContent(sender);
        }
    }
}