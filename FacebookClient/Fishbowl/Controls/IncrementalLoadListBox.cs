namespace FacebookClient
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Threading;
    using Standard;

    /// <remarks>
    /// The underlying data source should have most of its data already available before this class is constructed.
    /// This class only incrementally updates its ItemsSource on load.
    /// </remarks>
    public class IncrementalLoadListBox : ListBox
    {
        private const int PerItemMillisecondDelay = 10;

        private readonly ObservableCollection<object> _collection = new ObservableCollection<object>();
        private DispatcherTimer _timer;
        private bool _isLoading;
        private List<object> _originalList;

        public static DependencyProperty ActualItemsSourceProperty = DependencyProperty.Register(
            "ActualItemsSource", 
            typeof(IEnumerable), 
            typeof(IncrementalLoadListBox),
            new FrameworkPropertyMetadata(
                (d, e) => ((IncrementalLoadListBox)d)._OnActualItemsSourceChanged(e)),
            target => target == null || target is INotifyCollectionChanged);

        public IEnumerable ActualItemsSource
        {
            get { return (IEnumerable)GetValue(ActualItemsSourceProperty); }
            set { SetValue(ActualItemsSourceProperty, value); }
        }

        public IncrementalLoadListBox()
        {
            base.ItemsSource = _collection;

            this.Loaded += (sender, e) =>
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromMilliseconds(PerItemMillisecondDelay);
                _timer.Tick += _OnTimerTick;

                if (ActualItemsSource != null)
                {
                    ((INotifyCollectionChanged)ActualItemsSource).CollectionChanged += _OnActualItemsSourceCollectionChanged;

                    _originalList = new List<object>(ActualItemsSource.OfType<object>());
                    if (_originalList.Count == 0)
                    {
                        _originalList = null;
                        return;
                    }

                    _isLoading = true;
                    _timer.Start();
                }
            };

            this.Unloaded += (sender, e) =>
            {
                _timer.Tick -= _OnTimerTick;
                _timer.IsEnabled = false;

                if (ActualItemsSource != null)
                {
                    ((INotifyCollectionChanged)ActualItemsSource).CollectionChanged -= _OnActualItemsSourceCollectionChanged;
                }
            };
        }


        private void _OnActualItemsSourceChanged(DependencyPropertyChangedEventArgs e)
        {
            if (!this.IsLoaded)
            {
                return;
            }

            _collection.Clear();

            if (_isLoading)
            {
                _timer.Stop();
                _isLoading = false;
            }

            if (e.OldValue != null)
            {
                ((INotifyCollectionChanged)e.OldValue).CollectionChanged -= _OnActualItemsSourceCollectionChanged;
            }

            if (e.NewValue == null)
            {
                return;
            }

            ((INotifyCollectionChanged)ActualItemsSource).CollectionChanged += _OnActualItemsSourceCollectionChanged;

            // Copy the items as they currently stand. We'll add these to the list incrementally,
            // and worry about any intermediate changes at the end.
            _originalList = new List<object>(ActualItemsSource.OfType<object>());
            if (_originalList.Count == 0)
            {
                _originalList = null;
                return;
            }

            _isLoading = true;
            _timer.Start();
            _OnTimerTick(null, null);
        }

        private void _OnTimerTick(object sender, EventArgs e)
        {
            Assert.IsTrue(_isLoading);
            if (!_isLoading)
            {
                return;
            }

            _collection.Add(_originalList[0]);
            _originalList.RemoveAt(0);

            if (_originalList.Count == 0)
            {
                _originalList = null;
                _isLoading = false;
                _timer.Stop();

                // Now we have to account for any intermediate changes and make _collection identical to ActualItemsSource.
                // The assumption here is that we'll have most of the data in the correct order to begin with, so this shouldn't take long.
                // TODO: do something better than n^2.

                int pos = 0;

                foreach (object item in this.ActualItemsSource)
                {
                    int idx = _collection.IndexOf(item); // does unnecessary work.
                    if (idx == -1)
                    {
                        _collection.Insert(pos, item);
                    }
                    else if (idx != pos)
                    {
                        object temp = _collection[pos];
                        _collection[pos] = item;
                        _collection[idx] = temp;
                    }

                    pos++;
                }

                if (_collection.Count != pos)
                {
                    for (int i = _collection.Count - 1; i >= pos; i--)
                    {
                        _collection.RemoveAt(i);
                    }
                }

                _VerifyCollection();
            }
        }

        private void _VerifyCollection()
        {           
            int idx = 0;

            foreach (object item in this.ActualItemsSource)
            {
                Assert.IsTrue(_collection.Count > idx);
                Assert.ReferenceEquals(_collection[idx++], item);
            }

            Assert.IsTrue(_collection.Count == idx);
        }

        private void _OnActualItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            base.VerifyAccess();

            if (!_isLoading)
            {
                // At this point, _collection should have been identical to ActualItemsSource before this change.

                int idx;

                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        idx = e.NewStartingIndex;
                        foreach (var item in e.NewItems)
                        {
                            if (idx == -1)
                            {
                                _collection.Add(item);
                            }
                            else
                            {
                                _collection.Insert(idx++, item);
                            }
                        }

                        break;
                    case NotifyCollectionChangedAction.Remove:
                        idx = e.OldStartingIndex;
                        foreach (var item in e.OldItems)
                        {
                            _collection.RemoveAt(idx);
                        }

                        break;
                    case NotifyCollectionChangedAction.Reset:
                        _collection.Clear();
                        break;
                    case NotifyCollectionChangedAction.Move:
                        Assert.Fail();
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        Assert.Fail();
                        break;
                }
            }
        }
    }
}
