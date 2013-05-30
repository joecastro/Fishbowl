namespace Microsoft.Wpf.Samples.Documents
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;

    public class TextSelectionSettings : DependencyObject
    {
        static TextSelectionSettings()
        {
            Brush defaultHighlightFill = CreateBrush(Color.FromArgb(96, 0, 0, 255));
            HighlightFillProperty =
                        DependencyProperty.Register(
                            "HighlightFill", typeof(Brush), typeof(TextSelectionSettings),
                            new PropertyMetadata(defaultHighlightFill)
                        );

            Brush defaultInactiveHighlightFill = CreateBrush(Color.FromArgb(64, 0, 0, 255));
            InactiveHighlightFillProperty =
                        DependencyProperty.Register(
                            "InactiveHighlightFill", typeof(Brush), typeof(TextSelectionSettings),
                            new PropertyMetadata(defaultInactiveHighlightFill)
                        );
        }

        public static readonly DependencyProperty HighlightFillProperty;
        public static readonly DependencyProperty HighlightStrokeProperty =
            DependencyProperty.Register("HighlightStroke", typeof(Pen), typeof(TextSelectionSettings));

        public static readonly DependencyProperty InactiveHighlightFillProperty;
        public static readonly DependencyProperty InactiveHighlightStrokeProperty =
            DependencyProperty.Register("InactiveHighlightStroke", typeof(Pen), typeof(TextSelectionSettings));

        public static readonly IEnumerable<RoutedUICommand> DefaultContextMenuCommands =
            new RoutedUICommand[] {
                ApplicationCommands.Copy,
                ApplicationCommands.SelectAll
            };

        public Brush HighlightFill
        {
            get { return (Brush)GetValue(HighlightFillProperty); }
            set { SetValue(HighlightFillProperty, value); }
        }

        public Pen HighlightStroke
        {
            get { return (Pen)GetValue(HighlightStrokeProperty); }
            set { SetValue(HighlightStrokeProperty, value); }
        }

        public Brush InactiveHighlightFill
        {
            get { return (Brush)GetValue(InactiveHighlightFillProperty); }
            set { SetValue(InactiveHighlightFillProperty, value); }
        }

        public Pen InactiveHighlightStroke
        {
            get { return (Pen)GetValue(InactiveHighlightStrokeProperty); }
            set { SetValue(InactiveHighlightStrokeProperty, value); }
        }

        public ObservableCollection<RoutedUICommand> ContextMenuCommands
        {
            get
            {
                if (this.contextMenuCommands == null)
                {
                    this.contextMenuCommands = new ObservableCollection<RoutedUICommand>();
                }

                return this.contextMenuCommands;
            }
        }

        /// <summary>
        /// True of the the ContextMenuCommands property has never been accessed
        /// </summary>
        public bool HasContextMenuCommands
        {
            get { return this.contextMenuCommands != null; }
        }

        private static Brush CreateBrush(Color color)
        {
            Brush result = new SolidColorBrush(color);
            result.Freeze();
            return result;
        }

        private ObservableCollection<RoutedUICommand> contextMenuCommands;
    }
}
