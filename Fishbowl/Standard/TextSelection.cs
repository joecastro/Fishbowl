//#define SHARE_MOUSE_CAPTURE

namespace Microsoft.Wpf.Samples.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;
    using Standard;

    public class TextSelection : DependencyObject
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled", typeof(bool), typeof(TextSelection),
                new PropertyMetadata(false, HandleIsEnabledPropertyChanged)
            );

        public static readonly DependencyProperty SettingsProperty =
            DependencyProperty.RegisterAttached(
                "Settings", typeof(TextSelectionSettings), typeof(TextSelection)
            );

        private static readonly DependencyPropertyKey TextSelectionPropertyKey =
            DependencyProperty.RegisterAttachedReadOnly(
                "TextSelection", typeof(TextSelection), typeof(TextSelection),
                new PropertyMetadata(HandleTextSelectionChanged)
            );

        public static readonly DependencyProperty TextSelectionProperty =
            TextSelectionPropertyKey.DependencyProperty;

        private static readonly DependencyProperty CurrentWindowSelectionProperty =
            DependencyProperty.RegisterAttached(
                "CurrentWindowSelection", typeof(DependencyObject), typeof(TextSelection)                
            );

        public static bool GetIsEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsEnabledProperty, value);
        }

        public static TextSelection GetTextSelection(DependencyObject obj)
        {
            return (TextSelection)obj.GetValue(TextSelectionProperty);
        }

        public static TextSelectionSettings GetSettings(DependencyObject obj)
        {
            return (TextSelectionSettings)obj.GetValue(SettingsProperty);
        }

        public static void SetSettings(DependencyObject obj, TextSelectionSettings value)
        {
            obj.SetValue(SettingsProperty, value);
        }

        /// <summary>
        /// True if the selection service is connected to an element
        /// </summary>
        public bool IsConnected
        {
            get { return this.textBlock != null; }
        }

        public TextRange Selection
        {
            get { return this.SelectionInternal.TextRange; }
        }

        /// <summary>
        /// Returns true if the current selection can be copied to the clipboard
        /// </summary>
        protected virtual bool CanCopy
        {
            get
            {                
                return                    
                    !this.Selection.IsEmpty
                    && this.Selection.CanSave(DataFormats.Text);
            }
        }

        /// <summary>
        /// Copies the current selection to the clipboard
        /// </summary>
        /// <param name="e"></param>
        protected virtual bool OnCopy(RoutedEventArgs e)
        {
            if (!CanCopy)
            {
                return false;
            }

            TextRange range = this.Selection;
            string text = range.Text;

            // TODO: It would be more efficient to defer copying to the data object until a paste was requested
            DataObject data = new DataObject();
            data.SetData(DataFormats.Text, text);
            data.SetData(DataFormats.UnicodeText, text);

            using (Stream stream = new MemoryStream())
            {
                TrySetDataObject(data, range, DataFormats.Xaml, stream);
                TrySetDataObject(data, range, DataFormats.XamlPackage, stream);
                TrySetDataObject(data, range, DataFormats.Rtf, stream);
            }

            Clipboard.SetDataObject(data);
            return true;
        }

        protected virtual bool OnSelectAll(EventArgs e)
        {
            if (this.textBlock == null)
            {
                return false;
            }

            this.Selection.Select(this.textBlock.ContentStart, this.textBlock.ContentEnd);
            return true;
        }

        protected virtual void OnPrepareHighlightAdorner(HighlightAdorner adorner)
        {
            // TODO: figure out how to create a binding to "(TextSelection.Settings).HighlightFillProperty" in code

            TextSelectionSettings settings = EnsureSettings();

            BindingBase isFocusedBinding = new Binding() { 
                Path = new PropertyPath(FrameworkElement.IsFocusedProperty),
                Source = this.textBlock,
                Mode = BindingMode.OneWay
            };

            adorner.SetBinding(
                HighlightAdorner.FillProperty,
                new MultiBinding() {
                    Bindings = {
                        isFocusedBinding,
                        new Binding() { 
                            Path = new PropertyPath(TextSelectionSettings.HighlightFillProperty),
                            Source = settings,
                            Mode = BindingMode.OneWay
                        },
                        new Binding() { 
                            Path = new PropertyPath(TextSelectionSettings.InactiveHighlightFillProperty),
                            Source = settings,
                            Mode = BindingMode.OneWay
                        }
                    },
                    Mode = BindingMode.OneWay,
                    Converter = ToggleMultiConverter.Default
                }
                );

            adorner.SetBinding(
                HighlightAdorner.StrokeProperty,
                new MultiBinding()
                {
                    Bindings = {
                        isFocusedBinding,
                        new Binding() { 
                            Path = new PropertyPath(TextSelectionSettings.HighlightStrokeProperty),
                            Source = settings,
                            Mode = BindingMode.OneWay
                        },
                        new Binding() { 
                            Path = new PropertyPath(TextSelectionSettings.InactiveHighlightStrokeProperty),
                            Source = settings,
                            Mode = BindingMode.OneWay
                        }
                    },
                    Mode = BindingMode.OneWay,
                    Converter = ToggleMultiConverter.Default
                }
                );
        }

        protected virtual void OnClearHighlightAdorner(HighlightAdorner adorner)
        {
            BindingOperations.ClearBinding(adorner, HighlightAdorner.FillProperty);
            BindingOperations.ClearBinding(adorner, HighlightAdorner.StrokeProperty);
        }

        private SelectionRange SelectionInternal
        {
            get
            {
                VerifyConnected();

                if (this.selection == null)
                {
                    this.selection = CreateSelectionRange();
                }

                return this.selection;
            }
        }

        private void HandlePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsConnected)
            {
                return;
            }

            this.textBlock.Focus();

            if (TryStartUISelection(true))
            {
                this.mouseDownHyperlink = GetNearestAncestor<Hyperlink>(e.Source as DependencyObject);

                TextPointer position = GetPositionAtPoint(e);
                if (position != null)
                {
                    this.SelectionInternal.Reset(position);
                }
            }
        }

        private void HandlePreviewMouseLeftButtonUp(object sender, MouseEventArgs e)
        {
            if (this.mouseDownHyperlink != null)
            {
                Hyperlink hyperlink = this.mouseDownHyperlink;
                this.mouseDownHyperlink = null;

                // hyperlink.IsMouseOver is insufficient because it isn't always accurate...
                if (this.Selection.IsEmpty)
                {
                    // Mouse up was done on the same hyperlink as mouse down
                    // and because the selection is empty nothing is highlighted
                    // so the user probably intended a mouse click on the hyperlink
                    DependencyProperty prop = TryGetIsHyperlinkPressedProperty();
                    if (prop != null)
                    {
                        hyperlink.SetValue(prop, true);
                    }

                    hyperlink.RaiseEvent(e);
                }
            }

            if (!IsConnected)
            {
                return;
            }

            StopUISelection();
        }

        private void HandlePreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!IsConnected)
            {
                return;
            }

            TextPointer pointer = GetPositionAtPoint(e);

            if (pointer != null)
            {
                this.SelectionInternal.Extend(pointer);
            }
        }

        private void HandlePreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch(e.Key)
            {
                case Key.Escape: 
                {
                    CancelSelection();
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        private void HandleLostMouseCapture(object sender, RoutedEventArgs e)
        {
            if (this.mouseDownHyperlink != null)
            {
                // A child hyperlink tried to steal capture from us
                if (this.textBlock != null && !this.textBlock.IsMouseCaptured)
                {
                    Mouse.Capture(this.textBlock, CaptureMode.SubTree);
                }
            }
            else
            {
                // some unknown 3rd party tried to steal capture from us
                CancelSelection();
            }
        }

        private void HandleLostFocus(object sender, RoutedEventArgs e)
        {
            StopUISelection();
        }

        private static void HandleSelectAllCommand(object sender, ExecutedRoutedEventArgs e)
        {
            TextSelection selection = EnsureFromHostEventArgs(sender);

            if (selection != null)
            {
                e.Handled = selection.OnSelectAll(e);
            }
        }

        private static void HandleCanSelectAllCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private static void HandleCopyCommand(object sender, ExecutedRoutedEventArgs e)
        {
            TextSelection selection = EnsureFromHostEventArgs(sender);
            if (selection != null)
            {
                e.Handled = selection.OnCopy(e);
            }
        }

        private static void HandleCanCopyCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            TextSelection selection = FromHostEventArgs(sender);
            if (selection != null)
            {
                if (selection.CanCopy)
                {
                    e.CanExecute = true;
                    e.Handled = true;
                }
            }
        }

        private static void HandleIsEnabledPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            UIElement uiElementSender = sender as UIElement;

            if (uiElementSender != null)
            {
                TextSelection service = TextSelection.GetTextSelection(sender);

                if ((bool)e.NewValue)
                {
                    if (service == null)
                    {
                        uiElementSender.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, (MouseButtonEventHandler)HandlePreviewMouseLeftButtonDownStatic, true);
                        uiElementSender.AddHandler(FrameworkElement.ContextMenuOpeningEvent, (ContextMenuEventHandler)HandleContextMenuOpeningStatic, true);
                        uiElementSender.CommandBindings.Add(CopyCommandBinding);
                        uiElementSender.CommandBindings.Add(SelectAllCommandBinding);
                    }
                }
                else
                {
                    if (service != null)
                    {
                        // ClearValue is what we want to call but apparently ClearValue doesnt always
                        // trigger a property change callback so we force one by setting the property to null
                        sender.SetValue(TextSelectionPropertyKey, null);
                        sender.ClearValue(TextSelectionPropertyKey);

                        uiElementSender.CommandBindings.Remove(SelectAllCommandBinding);
                        uiElementSender.CommandBindings.Remove(CopyCommandBinding);
                        uiElementSender.RemoveHandler(FrameworkElement.ContextMenuOpeningEvent, (ContextMenuEventHandler)HandleContextMenuOpeningStatic);
                        uiElementSender.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, (MouseButtonEventHandler)HandlePreviewMouseLeftButtonDownStatic);
                    }
                }
            }
        }

        private static void HandlePreviewMouseLeftButtonDownStatic(object sender, MouseButtonEventArgs e)
        {
            TextSelection selection = EnsureFromHostEventArgs(sender);

            if (selection != null)
            {
                selection.HandlePreviewMouseLeftButtonDown(sender, e);
            }
        }

        private static void HandleContextMenuOpeningStatic(object sender, ContextMenuEventArgs e)
        {
            DependencyObject dObjSender = sender as DependencyObject;
            IInputElement inputElementSender = sender as IInputElement;

            if (dObjSender != null && inputElementSender != null)
            {
                ContextMenu contextMenu = (ContextMenu)(dObjSender.GetValue(FrameworkElement.ContextMenuProperty));

                if (contextMenu == null)
                {
                    TextSelectionSettings settings = TextSelection.GetSettings(dObjSender);
                    IEnumerable<RoutedUICommand> commands = null;

                    if (settings != null && settings.HasContextMenuCommands)
                    {
                        commands = settings.ContextMenuCommands;
                    }
                    else
                    {
                        commands = TextSelectionSettings.DefaultContextMenuCommands;
                    }

                    contextMenu = CreateContextMenu(inputElementSender, commands);

                    if (contextMenu != null)
                    {                    
                        // We are repacing a null ContextMenu with our built from scratch ContextMenu.
                        // Mark the event as handled so that the built in context menu service does not
                        // try to open the null Context menu and we can open our built from scratch menu 
                        // ourselves
                        e.Handled = true;

                        RoutedEventHandler handleContextMenuClosed = null;
                        handleContextMenuClosed = (closedSender, closedArgs) =>
                        {
                            inputElementSender.RemoveHandler(FrameworkElement.ContextMenuClosingEvent, handleContextMenuClosed);
                            dObjSender.ClearValue(FrameworkElement.ContextMenuProperty);
                        };

                        inputElementSender.AddHandler(FrameworkElement.ContextMenuClosingEvent, handleContextMenuClosed);
                        dObjSender.SetValue(FrameworkElement.ContextMenuProperty, contextMenu);
                        contextMenu.IsOpen = true;
                    }
                }
            }
        }

        private static void HandleTextSelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            TextBlock textBlock = sender as TextBlock;
            TextSelection oldValue = (TextSelection)e.OldValue;
            TextSelection newValue = (TextSelection)e.NewValue;

            if (!object.ReferenceEquals(oldValue, newValue))
            {
                if (oldValue != null)
                {
                    oldValue.Disconnect();
                }

                if (newValue != null && textBlock != null)
                {
                    // text selection instances cannot be shared by elements.
                    // Because this is a XAML scenario, rather than throw an exception
                    // we will disconnect from the old element first
                    if (newValue.IsConnected)
                    {
                        newValue.Disconnect();
                    }

                    newValue.ConnectTo(textBlock);
                }
            }
        }

        private static void HandleSelectionChanged(object sender, EventArgs e)
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private void VerifyConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Must be connected to an element. Try first calling SetTextSelectionService on an element");
            }
        }

        /// <summary>
        /// Initializes selection services for an element
        /// </summary>
        /// <param name="textBlock">Element to initialize selection for</param>
        private void ConnectTo(TextBlock textBlock)
        {
            Assert.IsNull(this.textBlock);
            Assert.IsNull(this.adorner);
            Assert.IsNull(this.adornerLayer);
            Assert.IsNull(this.view);
            Assert.IsNull(this.selection);

            this.textBlock = textBlock;

            if (this.textBlock != null)
            {
                this.textBlock.AddHandler(UIElement.PreviewKeyDownEvent, (KeyEventHandler)HandlePreviewKeyDown);
                this.textBlock.AddHandler(UIElement.LostFocusEvent, (RoutedEventHandler)HandleLostFocus);

                // TODO: Find out how does one temporarily make textBlock focusable without clobbering the Focusable property?
                this.textBlock.Focusable = true;

                // We use the adorner layer to draw our selection highlights
                this.adornerLayer = AdornerLayer.GetAdornerLayer(this.textBlock);
                if (this.adornerLayer != null)
                {
                    this.adorner = new HighlightAdorner(this.textBlock);
                    this.adorner.DataContext = this;

                    OnPrepareHighlightAdorner(this.adorner);

                    this.adornerLayer.Add(this.adorner);
                }

                this.view = new TextBlockTextView(this.textBlock);
            }
        }

        /// <summary>
        /// Tears doen selection services for a TextBlock
        /// </summary>
        private void Disconnect()
        {
            StopUISelection();

            if (this.textBlock != null)
            {
                this.textBlock.RemoveHandler(UIElement.LostFocusEvent, (RoutedEventHandler)HandleLostFocus);
                this.textBlock.RemoveHandler(UIElement.PreviewKeyDownEvent, (KeyEventHandler)HandlePreviewKeyDown);

                this.textBlock.ClearValue(UIElement.FocusableProperty);

                this.textBlock = null;
            }

            DestroySelectionRange(this.selection);
            this.selection = null;

            if (this.adorner != null)
            {
                this.adorner.HighlightRanges.Clear();

                OnClearHighlightAdorner(this.adorner);

                this.adorner.DataContext = null;
                if (this.adornerLayer != null)
                {
                    this.adornerLayer.Remove(this.adorner);
                    this.adornerLayer = null;
                }

                this.adorner = null;
            }

            if (this.view != null)
            {
                ((IDisposable)(this.view)).Dispose();
                this.view = null;
            }

            this.mouseDownHyperlink = null;

            Assert.IsNull(this.textBlock);
            Assert.IsNull(this.adorner);
            Assert.IsNull(this.adornerLayer);
            Assert.IsNull(this.view);
            Assert.IsNull(this.selection);
            Assert.IsNull(this.mouseDownHyperlink);
        }

        private bool TryStartUISelection(bool inMouseHandler)
        {
            bool result = false;

            if (this.textBlock != null)
            {
                Window window = Window.GetWindow(this.textBlock);
                if (window != null)
                {
                    SetCurrentWindowSelection(window, this.textBlock);
                }

                // We need the mouse to be captured.
                // We dont care who has mouse capture so long there is mouse capture and we continue to get mouse input.
                
                // If we try to start UI selection 
                //  1) From within a mouse handler - We are obviously getting mouse input.
                //      a) Mouse capture is taken - We have mouse capture and are getting mouse input, there is no work to do.
                //      b) Mouse capture is not taken - Take mouse capture to ensure we always get mouse input.
                //  2) From outside a mouse handler - Always take mouse capture because we dont know if we will be able to get mouse input.

                bool mouseCaptured = false;

#if SHARE_MOUSE_CAPTURE
                bool takeMouseCapture = false;
                if (inMouseHandler)
                {
                    mouseCaptured = Mouse.Captured != null;
                    if (!mouseCaptured)
                    {
                        // Case (1.b)
                        takeMouseCapture = true;
                    }
                }
                else 
                {
                    // Case (2)
                    takeMouseCapture = true;
                }
#else
                bool takeMouseCapture = true;
#endif

                if (takeMouseCapture)
                {
                    mouseCaptured = Mouse.Capture(this.textBlock, CaptureMode.SubTree);
                }

                if (mouseCaptured)
                {
                    this.textBlock.AddHandler(UIElement.LostMouseCaptureEvent, (RoutedEventHandler)HandleLostMouseCapture);
                    this.textBlock.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, (MouseButtonEventHandler)HandlePreviewMouseLeftButtonUp, true);
                    this.textBlock.AddHandler(UIElement.PreviewMouseMoveEvent, (MouseEventHandler)HandlePreviewMouseMove, true);
                    result = true;
                }
            }

            return result;
        }

        private void StopUISelection()
        {
            this.textBlock.RemoveHandler(UIElement.PreviewMouseMoveEvent, (MouseEventHandler)HandlePreviewMouseMove);
            this.textBlock.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, (MouseButtonEventHandler)HandlePreviewMouseLeftButtonUp);
            this.textBlock.RemoveHandler(UIElement.LostMouseCaptureEvent, (RoutedEventHandler)HandleLostMouseCapture);

            this.textBlock.ReleaseMouseCapture();
        }

        private void CancelSelection()
        {
            if (this.textBlock != null)
            {
                this.textBlock.ClearValue(TextSelection.TextSelectionPropertyKey);
            }
        }

        private SelectionRange CreateSelectionRange()
        {
            SelectionRange result = new SelectionRange(this.view, this.textBlock.ContentStart);
            result.Changed += HandleSelectionChanged;
            ConnectToAdorner(result);
            return result;
        }

        private void DestroySelectionRange(SelectionRange range)
        {
            if (range != null)
            {
                DisconnectFromAdorner(range);
                range.Changed -= HandleSelectionChanged;
            }
        }

        private bool TrySetDataObject(DataObject data, TextRange range, string dataFormat, Stream scratchSpace)
        {
            Assert.IsTrue(scratchSpace.CanSeek);
            Assert.IsTrue(scratchSpace.CanWrite);
            Assert.IsTrue(scratchSpace.CanRead);

            if (!range.CanSave(dataFormat))
            {
                return false;
            }

            scratchSpace.Position = 0;
            scratchSpace.SetLength(0);
            range.Save(scratchSpace, dataFormat);

            scratchSpace.Position = 0;
            data.SetData(dataFormat, new StreamReader(scratchSpace, Encoding.UTF8).ReadToEnd());
            return true;
        }

        private TextPointer GetPositionAtPoint(MouseEventArgs e)
        {
            VerifyConnected();

            Point point = e.GetPosition(this.textBlock);
            return view.GetPositionFromPoint(point, true);
        }

        private void ConnectToAdorner(SelectionRange range)
        {
            if (this.adorner != null && range != null)
            {
                if (!this.adorner.HighlightRanges.Contains(range.HighlightRange))
                {
                    this.adorner.HighlightRanges.Add(range.HighlightRange);
                }
            }
        }

        private void DisconnectFromAdorner(SelectionRange range)
        {
            if (this.adorner != null && range != null)
            {
                this.adorner.HighlightRanges.Remove(range.HighlightRange);
            }
        }

        private static T GetNearestAncestor<T>(DependencyObject item) where T : DependencyObject
        {
            while (item != null)
            {
                T result = item as T;
                if (result != null)
                {
                    return result;
                }

                DependencyObject parent;
                if (item is Visual || item is System.Windows.Media.Media3D.Visual3D)
                {
                    parent = VisualTreeHelper.GetParent(item);
                }
                else
                {
                    parent = LogicalTreeHelper.GetParent(item);
                }

                item = parent;
            }

            return null;
        }

        private static void SetCurrentWindowSelection(Window window, DependencyObject selectionHost)
        {
            if (window != null)
            {
                DependencyObject lastSelectionHost = (DependencyObject)window.GetValue(CurrentWindowSelectionProperty);
                if (!object.ReferenceEquals(selectionHost, lastSelectionHost))
                {
                    if (lastSelectionHost != null)
                    {
                        TextSelection lastTextSelection = TextSelection.GetTextSelection(lastSelectionHost);
                        if (lastTextSelection != null)
                        {
                            lastTextSelection.CancelSelection();
                        }
                    }
                }

                window.SetValue(CurrentWindowSelectionProperty, selectionHost);
            }
        }

        private TextSelectionSettings EnsureSettings()
        {
            TextSelectionSettings result;

            if (this.textBlock != null)
            {
                result = TextSelection.GetSettings(this.textBlock);
                if (result == null)
                {
                    result = new TextSelectionSettings();
                    TextSelection.SetSettings(this.textBlock, result);
                }
            }
            else
            {
                result = new TextSelectionSettings();
            }

            return result;
        }

        private static ContextMenu CreateContextMenu(IInputElement target, IEnumerable<RoutedUICommand> commands)
        {
            ContextMenu contextMenu = new ContextMenu();

            foreach (RoutedUICommand command in commands)
            {
                contextMenu.Items.Add(
                    new MenuItem()
                    {
                        Command = command,
                        CommandTarget = target
                    }
                    );
            }

            return contextMenu;
        }

        private static TextSelection FromHostEventArgs(object sender)
        {
            DependencyObject obj = sender as DependencyObject;

            if (obj == null)
            {
                return null;
            }

            return (TextSelection)obj.GetValue(TextSelection.TextSelectionProperty);
        }

        private static TextSelection EnsureFromHostEventArgs(object sender)
        {
            DependencyObject obj = sender as DependencyObject;

            if (obj == null)
            {
                return null;
            }

            TextSelection result = (TextSelection)obj.GetValue(TextSelection.TextSelectionProperty);

            if (result == null)
            {
                result = new TextSelection();
                obj.SetValue(TextSelectionPropertyKey, result);
            }

            return result;
        }

        private static DependencyProperty TryGetIsHyperlinkPressedProperty()
        {
            if (isHyperlinkPressedPropertyCache == null)
            {
                FieldInfo fieldInfo = typeof(Hyperlink).GetField(
                    "IsHyperlinkPressedProperty",
                    BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly
                );

                if (fieldInfo != null)
                {
                    isHyperlinkPressedPropertyCache = (DependencyProperty)fieldInfo.GetValue(null);
                }
            }

            return isHyperlinkPressedPropertyCache;
        }

        private TextView view;
        private SelectionRange selection;

        private HighlightAdorner adorner;
        private AdornerLayer adornerLayer;
        private TextBlock textBlock;
        private Hyperlink mouseDownHyperlink;

        private static DependencyProperty isHyperlinkPressedPropertyCache;

        private static readonly CommandBinding CopyCommandBinding = new CommandBinding(ApplicationCommands.Copy, HandleCopyCommand, HandleCanCopyCommand);
        private static readonly CommandBinding SelectAllCommandBinding = new CommandBinding(ApplicationCommands.SelectAll, HandleSelectAllCommand, HandleCanSelectAllCommand);

        private class ToggleMultiConverter : IMultiValueConverter
        {
            public static readonly ToggleMultiConverter Default = new ToggleMultiConverter();

            public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                object result;

                switch (values.Length)
                {
                    case 0:
                    case 1:
                    {
                        result = null;
                        break;
                    }

                    case 2:
                    {
                        result = ((bool)values[0]) ? values[1] : null;
                        break;
                    }

                    default:
                    {
                        bool isEnabled = (values[0] is bool) ? ((bool)values[0]) : false;
                        result = isEnabled ? values[1] : values[2];
                        break;
                    }
                }

                return result;
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
            {
                throw new NotSupportedException();
            }
        }
    }
}