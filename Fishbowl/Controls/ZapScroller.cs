
namespace FacebookClient
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Input;
    using System.Windows.Threading;
    using Standard;

    #region ActionICommand

    [TypeConverter(typeof(ActionICommandConverter))]
    public class ActionICommand : ICommand
    {
        public static ActionICommand Create(Action action)
        {
            Verify.IsNotNull(action, "action");
            return Create(action, null);
        }

        public static ActionICommand Create(Action action, Func<bool> canExecuteFunction)
        {
            Verify.IsNotNull(action, "action");

            Action foo;
            return Create(action, canExecuteFunction, out foo);
        }

        public static ActionICommand Create(Action action, Func<bool> canExecuteFunction, out Action canExecuteChanged)
        {
            Verify.IsNotNull(action, "action");

            ActionICommand command = new ActionICommand(action, canExecuteFunction);

            canExecuteChanged = command.onCanExecuteChanged;

            return command;
        }

        public bool CanExecute
        {
            get
            {
                if (m_canExecuteFunction != null)
                {
                    return m_canExecuteFunction();
                }
                else
                {
                    return true;
                }
            }
        }

        public void Execute()
        {
            m_action();
        }

        public event EventHandler CanExecuteChanged;

        bool ICommand.CanExecute(object parameter)
        {
            return CanExecute;
        }

        void ICommand.Execute(object parameter)
        {
            Execute();
        }

        protected virtual void OnCanExecuteChanged(EventArgs args)
        {
            Verify.IsNotNull(args, "args");

            EventHandler handler = this.CanExecuteChanged;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        #region Implementation

        private ActionICommand(Action action, Func<bool> canExecuteFunction)
        {
            //Util.RequireNotNull(action, "action");
            m_action = action;

            m_canExecuteFunction = canExecuteFunction;
        }

        private void onCanExecuteChanged()
        {
            OnCanExecuteChanged(EventArgs.Empty);
        }

        private readonly Func<bool> m_canExecuteFunction;
        private readonly Action m_action;

        #endregion

    }

    public class ActionICommandConverter : TypeConverter
    {
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(ICommand))
            {
                return true;
            }
            else if (destinationType == typeof(string))
            {
                return true;
            }
            else
            {
                return base.CanConvertTo(context, destinationType);
            }
        }

        public override object ConvertTo(
            ITypeDescriptorContext context,
            CultureInfo culture,
            object value,
            Type destinationType)
        {
            if (destinationType == typeof(ICommand))
            {
                return (ICommand)value;
            }
            else if (destinationType == typeof(string))
            {
                return ((ActionICommand)value).ToString();
            }
            else
            {
                return base.ConvertTo(context, culture, value, destinationType);
            }
        }
    }

    #endregion

    public class ZapScroller : ItemsControl
    {
        static ZapScroller()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZapScroller), new FrameworkPropertyMetadata(typeof(ZapScroller)));

            FocusableProperty.OverrideMetadata(
                typeof(ZapScroller),
                new FrameworkPropertyMetadata(false));
        }

        public ZapScroller()
        {
            m_commandItemsRO = new ReadOnlyObservableCollection<ZapCommandItem>(m_commandItems);

            _firstCommand = ActionICommand.Create(First, canFirst, out m_canFirstChanged);
            _previousCommand = ActionICommand.Create(Previous, CanPrevious, out m_canPreviousChanged);
            _nextCommand = ActionICommand.Create(Next, CanNext, out m_canNextChanged);
            _lastCommand = ActionICommand.Create(Last, canLast, out m_canLastChanged);
            _moreCommand = ActionICommand.Create(More, canMore, out m_canMoreChanged);
        }

        public ICommand FirstCommand { get { return _firstCommand; } }

        public ICommand PreviousCommand { get { return _previousCommand; } }

        public ICommand NextCommand { get { return _nextCommand; } }

        public ICommand LastCommand { get { return _lastCommand; } }

        public ICommand MoreCommand { get { return _moreCommand; } }

        public ReadOnlyObservableCollection<ZapCommandItem> Commands
        {
            get
            {
                return m_commandItemsRO;
            }
        }

        public static readonly DependencyProperty CommandItemTemplateProperty =
            DependencyProperty.Register("CommandItemTemplate", typeof(DataTemplate), typeof(ZapScroller));

        public DataTemplate CommandItemTemplate
        {
            get { return (DataTemplate)GetValue(CommandItemTemplateProperty); }
            set { SetValue(CommandItemTemplateProperty, value); }
        }

        public static readonly DependencyProperty CommandItemTemplateSelectorProperty =
            DependencyProperty.Register("CommandItemTemplateSelector", typeof(DataTemplateSelector), typeof(ZapScroller));

        public DataTemplateSelector CommandItemTemplateSelector
        {
            get { return (DataTemplateSelector)GetValue(CommandItemTemplateSelectorProperty); }
            set { SetValue(CommandItemTemplateSelectorProperty, value); }
        }

        private static readonly DependencyPropertyKey ItemCountPropertyKey =
            DependencyProperty.RegisterReadOnly("ItemCount",
            typeof(int), typeof(ZapScroller), new PropertyMetadata(0));

        public static readonly DependencyProperty ItemCountProperty = ItemCountPropertyKey.DependencyProperty;

        public int ItemCount
        {
            get { return (int)GetValue(ItemCountProperty); }
        }

        public static readonly DependencyProperty CurrentItemIndexProperty =
            DependencyProperty.Register("CurrentItemIndex", typeof(int), typeof(ZapScroller),
            new PropertyMetadata(new PropertyChangedCallback(currentItemIndex_changed)));

        public int CurrentItemIndex
        {
            get { return (int)GetValue(CurrentItemIndexProperty); }
            set { SetValue(CurrentItemIndexProperty, value); }
        }

        public static readonly DependencyProperty CurrentItemProperty =
            DependencyProperty.Register("CurrentItem", typeof(object), typeof(ZapScroller));

        public object CurrentItem
        {
            get { return GetValue(CurrentItemProperty); }
            set { SetValue(CurrentItemProperty, value); }
        }

        public static readonly DependencyProperty PlayTimeIntervalProperty =
            DependencyProperty.Register("PlayTimeInterval", typeof(int), typeof(ZapScroller),
            new PropertyMetadata(new PropertyChangedCallback(playTimeInterval_changed)));

        public int PlayTimeInterval
        {
            get { return (int)GetValue(PlayTimeIntervalProperty); }
            set { SetValue(PlayTimeIntervalProperty, value); }
        }

        public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
            "IsPlaying", 
            typeof(bool),
            typeof(ZapScroller),
            new PropertyMetadata(
                false,
                isPlaying_changed));

        public bool IsPlaying
        {
            get { return (bool)GetValue(IsPlayingProperty); }
            set { SetValue(IsPlayingProperty, value); }
        }

        public static readonly DependencyProperty IsShuffledProperty =
            DependencyProperty.Register("IsShuffled", typeof(bool), typeof(ZapScroller),
            new PropertyMetadata(new PropertyChangedCallback(isShuffled_changed)));

        public bool IsShuffled
        {
            get { return (bool)GetValue(IsShuffledProperty); }
            set { SetValue(IsShuffledProperty, value); }
        }

        public void First()
        {
            if (canFirst())
            {
                CurrentItemIndex = 0;
            }
        }

        public void Previous()
        {
            if (IsShuffled)
            {
                if (m_currentIndexInIndexList > 0)
                {
                    CurrentItemIndex = m_indexList[--m_currentIndexInIndexList];
                }
                else
                {
                    m_currentIndexInIndexList = m_indexList.Count - 1;
                    CurrentItemIndex = m_currentIndexInIndexList;
                }
                return;
            }
            if (CanPrevious())
            {
                CurrentItemIndex--;
            }
        }

        public void Next()
        {
            if (IsShuffled)
            {
                if (m_currentIndexInIndexList < m_indexList.Count - 1)
                {
                    CurrentItemIndex = m_indexList[++m_currentIndexInIndexList];
                }
                else
                {
                    m_currentIndexInIndexList = 0;
                    CurrentItemIndex = m_currentIndexInIndexList;
                }
                return;
            }
            if (CanNext())
            {
                CurrentItemIndex++;
            }
        }

        public void Last()
        {
            if (canLast())
            {
                CurrentItemIndex = (ItemCount - 1);
            }
        }

        public void More()
        {
            if (canMore())
            {
                if (CurrentItemIndex == ItemCount - 1)
                    CurrentItemIndex = 0;
                else
                    CurrentItemIndex = CurrentItemIndex + 1;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            findZapDecorator();

            var panel = m_zapDecorator.Child as ZapPanel;

            if (panel != null)
            {
                panel.Measure(availableSize);
                return panel.DesiredSize;
            }
            return base.MeasureOverride(availableSize);
        }

        public static RoutedEvent CurrentItemIndexChangedEvent =
            EventManager.RegisterRoutedEvent("CurrentItemIndexChanged", RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<int>), typeof(ZapScroller));

        public event RoutedPropertyChangedEventHandler<int> CurrentItemIndexChanged
        {
            add { base.AddHandler(CurrentItemIndexChangedEvent, value); }
            remove { base.RemoveHandler(CurrentItemIndexChangedEvent, value); }
        }

        protected virtual void OnCurrentItemIndexChanged(int oldValue, int newValue)
        {
            resetEdgeCommands();
            RoutedPropertyChangedEventArgs<int> args = new RoutedPropertyChangedEventArgs<int>(oldValue, newValue);
            args.RoutedEvent = CurrentItemIndexChangedEvent;
            base.RaiseEvent(args);

            Items.MoveCurrentToPosition(newValue);

            if (newValue == -1 || Items.Count == 0)
            {
                CurrentItem = null;
            }
            else
            {
                CurrentItem = Items[newValue];
            }
        }

        protected virtual void OnPlayTimeIntervalChanged(int oldValue, int newValue)
        {
            if (m_dispatcherTimer == null)
            {
                m_dispatcherTimer = new DispatcherTimer(new TimeSpan(0, 0, 0, 0, PlayTimeInterval), DispatcherPriority.Background, new EventHandler(moveToNextIndex), this.Dispatcher);
            }

            if (m_dispatcherTimer != null)
            {
                if (IsPlaying)
                {
                    m_dispatcherTimer.Stop();
                }

                m_dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, newValue);

                if (IsPlaying)
                {
                    m_dispatcherTimer.Start();
                }
            }
        }

        protected virtual void OnIsPlayingChanged(bool oldValue, bool newValue)
        {
            if (m_dispatcherTimer == null)
            {
                m_dispatcherTimer = new DispatcherTimer(new TimeSpan(0, 0, 0, 0, PlayTimeInterval), DispatcherPriority.Background, new EventHandler(moveToNextIndex), this.Dispatcher);
            }

            if (IsPlaying)
            {
                m_dispatcherTimer.Start();
            }
            else
            {
                m_dispatcherTimer.Stop();
            }
        }

        protected virtual void OnIsShuffledChanged(bool oldValue, bool newValue)
        {
            if (IsShuffled)
            {
                createIndexList();
            }
        }

        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);

            ItemCollection newItems = this.Items;

            if (IsShuffled)
            {
                createIndexList();
            }

            if (newItems != m_internalItemCollection)
            {
                m_internalItemCollection = newItems;
                resetProperties();
            }
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);

            if (IsShuffled)
            {
                createIndexList();
            }

            if (m_internalItemCollection != Items)
            {
                m_internalItemCollection = Items;
            }

            resetProperties();
        }

        #region Implementation

        private static void currentItemIndex_changed(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            ZapScroller zapScroller = (ZapScroller)element;
            zapScroller.OnCurrentItemIndexChanged((int)e.OldValue, (int)e.NewValue);
        }

        private static void playTimeInterval_changed(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            ZapScroller zapScroller = (ZapScroller)element;
            zapScroller.OnPlayTimeIntervalChanged((int)e.OldValue, (int)e.NewValue);
        }

        private static void isPlaying_changed(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            ZapScroller zapScroller = (ZapScroller)element;
            zapScroller.OnIsPlayingChanged((bool)e.OldValue, (bool)e.NewValue);
        }

        private static void isShuffled_changed(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            ZapScroller zapScroller = (ZapScroller)element;
            zapScroller.OnIsShuffledChanged((bool)e.OldValue, (bool)e.NewValue);
        }

        private void resetEdgeCommands()
        {
            m_canFirstChanged();
            m_canLastChanged();
            m_canNextChanged();
            m_canPreviousChanged();
        }

        private void resetCommands()
        {
            resetEdgeCommands();

            int parentItemsCount = this.ItemCount;

            if (parentItemsCount != m_commandItems.Count)
            {
                if (parentItemsCount > m_commandItems.Count)
                {
                    for (int i = m_commandItems.Count; i < parentItemsCount; i++)
                    {
                        m_commandItems.Add(new ZapCommandItem(this, i));
                    }
                }
                else
                {
                    Assert.IsTrue(parentItemsCount < m_commandItems.Count);
                    int delta = m_commandItems.Count - parentItemsCount;
                    for (int i = 0; i < delta; i++)
                    {
                        m_commandItems.RemoveAt(m_commandItems.Count - 1);
                    }
                }
            }

            Assert.AreEqual(Items.Count, m_commandItems.Count);

            for (int i = 0; i < parentItemsCount; i++)
            {
                m_commandItems[i].Content = Items[i];
            }

#if DEBUG
            for (int i = 0; i < m_commandItems.Count; i++)
            {
                Assert.AreEqual(((ZapCommandItem)m_commandItems[i]).Index, i);
            }
#endif
        }

        private void findZapDecorator()
        {
            if (this.Template != null)
            {
                ZapDecorator temp = this.Template.FindName(PART_ZapDecorator, this) as ZapDecorator;
                if (m_zapDecorator != temp)
                {
                    m_zapDecorator = temp;
                    if (m_zapDecorator != null)
                    {
                        Binding binding = new Binding("CurrentItemIndex");
                        binding.Source = this;
                        m_zapDecorator.SetBinding(ZapDecorator.TargetIndexProperty, binding);
                    }
                }
                else
                {
                    Debug.WriteLine("No element with name '" + PART_ZapDecorator + "' in the template.");
                }
            }
            else
            {
                Debug.WriteLine("No template defined for ZapScroller.");
            }
        }

        private void resetProperties()
        {
            if (m_internalItemCollection.Count != ItemCount)
            {
                SetValue(ItemCountPropertyKey, m_internalItemCollection.Count);
            }

            if (CurrentItemIndex >= ItemCount)
            {
                CurrentItemIndex = (ItemCount - 1);
            }
            else if (CurrentItemIndex == -1 && ItemCount > 0)
            {
                CurrentItemIndex = 0;
            }

            if (CurrentItemIndex == 0 && ItemCount > 0)
            {
                CurrentItem = Items[CurrentItemIndex];
            }

            resetCommands();
        }

        /// <summary>
        /// Creates a shuffled play list
        /// </summary>
        private void createIndexList()
        {
            this.Dispatcher.VerifyAccess();

            if (m_indexList == null)
            {
                m_indexList = new List<int>();
            }

            m_indexList.Clear();

            List<int> randomIndexes = new List<int>();
            for (int i = 0; i < ItemCount; i++)
            {
                randomIndexes.Add(i);
            }

            Random rand = new Random();
            for (int i = 0; i < ItemCount; i++)
            {
                int indexOfIndex = rand.Next(randomIndexes.Count);
                m_indexList.Add(randomIndexes[indexOfIndex]);
                randomIndexes.RemoveAt(indexOfIndex);
            }

            m_currentIndexInIndexList = 0;
        }

        private void moveToNextIndex(object sender, EventArgs e)
        {
            this.Dispatcher.VerifyAccess();

            if (IsShuffled)
            {
                if (m_currentIndexInIndexList < m_indexList.Count - 1)
                {
                    CurrentItemIndex = m_indexList[++m_currentIndexInIndexList];
                }
                else
                {
                    m_currentIndexInIndexList = 0;
                    CurrentItemIndex = m_currentIndexInIndexList;
                }
            }
            else if (IsPlaying)
            {
                if (CurrentItemIndex < Items.Count - 1)
                {
                    Next();
                }
                else
                {
                    First();
                }
            }
        }

        private bool canFirst()
        {
            return (ItemCount > 1) && (CurrentItemIndex > 0);
        }

        public bool CanNext()
        {
            return ((CurrentItemIndex >= 0) && CurrentItemIndex < (ItemCount - 1)) || IsShuffled;
        }

        public bool CanPrevious()
        {
            return (CurrentItemIndex > 0) || IsShuffled;
        }

        private bool canLast()
        {
            return (ItemCount > 1) && (CurrentItemIndex < (ItemCount - 1));
        }

        private bool canMore()
        {
            return (ItemCount > 1);
        }

        private ItemCollection m_internalItemCollection;
        private ZapDecorator m_zapDecorator;

        private readonly ICommand _firstCommand, _previousCommand, _nextCommand, _lastCommand, _moreCommand;
        private readonly Action m_canFirstChanged, m_canPreviousChanged, m_canNextChanged, m_canLastChanged, m_canMoreChanged;

        private readonly ObservableCollection<ZapCommandItem> m_commandItems = new ObservableCollection<ZapCommandItem>();
        private readonly ReadOnlyObservableCollection<ZapCommandItem> m_commandItemsRO;

        private List<int> m_indexList; // used in Play or Shuffle modes to keep track of and control displayed items and newly added/deleted itmes
        private int m_currentIndexInIndexList;
        private DispatcherTimer m_dispatcherTimer;

        private DateTime m_lastItemsUpdateTime = DateTime.Now;

        #endregion

        public const string PART_ZapDecorator = "PART_ZapDecorator";

    }
}