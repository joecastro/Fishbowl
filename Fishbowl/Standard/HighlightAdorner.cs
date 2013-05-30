namespace Microsoft.Wpf.Samples.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Documents;
    using System.Windows.Media;
    using Standard;

    public class HighlightRange
    {
        public HighlightRange(TextView view, TextRange range)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            if (range == null)
            {
                throw new ArgumentNullException("range");
            }

            this.View = view;
            this.View.Changed += HandleChanged;

            this.Range = range;
            this.Range.Changed += HandleChanged;
        }

        public Geometry GetGeometry()
        {
            if (this.cachedGeometry == null)
            {
                Geometry g = this.View.GetHilightGeometry(this.Range);

                if (!g.IsFrozen)
                {
                    g.Freeze();
                }

                this.cachedGeometry = g;
            }

            return this.cachedGeometry;
        }

        public event EventHandler<EventArgs> Changed;

        private void HandleChanged(object sender, EventArgs e)
        {
            this.cachedGeometry = null;

            EventHandler<EventArgs> h = Changed;
            if (h != null)
            {
                h(this, EventArgs.Empty);
            }
        }

        private Geometry cachedGeometry;
        private readonly TextRange Range;
        private readonly TextView View;
    }

    public class HighlightAdorner : Adorner
    {
        public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
            "Fill", typeof(Brush), typeof(HighlightAdorner),
            new FrameworkPropertyMetadata(
                null, FrameworkPropertyMetadataOptions.AffectsRender
                )
                );

        public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
            "Stroke", typeof(Pen), typeof(HighlightAdorner),
            new FrameworkPropertyMetadata(
                null, FrameworkPropertyMetadataOptions.AffectsRender
                )
                );

        static HighlightAdorner()
        {
            UIElement.IsHitTestVisibleProperty.OverrideMetadata(
                typeof(HighlightAdorner),
                new UIPropertyMetadata(false));
        }

        public HighlightAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
        }

        public ICollection<HighlightRange> HighlightRanges
        {
            get
            {
                if (this.ranges == null)
                {
                    this.ranges = new ObservableCollection<HighlightRange>();
                    this.ranges.CollectionChanged += OnRangesCollectionChanged;
                }

                return this.ranges;
            }
        }

        public Brush Fill
        {
            get { return (Brush)this.GetValue(FillProperty); }
            set { this.SetValue(FillProperty, value); }
        }

        public Pen Stroke
        {
            get { return (Pen)this.GetValue(StrokeProperty); }
            set { this.SetValue(StrokeProperty, value); }
        }

        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            return null;
        }

        protected override GeometryHitTestResult HitTestCore(GeometryHitTestParameters hitTestParameters)
        {
            return null;
        }

        private void OnTextRangeChanged(object sender, System.EventArgs e)
        {
            this.InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            // Todo this does not scale well with several text ranges
            // A better solution may be to add a visual child for each range 
            //  and have just the child re-render in response to text change events for that range
            if (this.ranges != null)
            {
                foreach (HighlightRange range in this.ranges)
                {
                    Geometry g = range.GetGeometry().GetOutlinedPathGeometry();
                    if (!g.IsFrozen)
                    {
                        g.Freeze();
                    }

                    drawingContext.DrawGeometry(Fill, Stroke, g);
                }
            }
        }

        private static readonly Pen blackPen = new Pen(Brushes.Black, 0.5);

        private void OnRangesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            this.InvalidateVisual();

            IList<HighlightRange> collection = sender as IList<HighlightRange>;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                {
                    if (e.NewItems != null)
                    {
                        foreach (HighlightRange range in e.NewItems)
                        {
                            RegisterTextRange(range);
                        }
                    }

                    if (e.OldItems != null)
                    {
                        foreach (HighlightRange range in e.OldItems)
                        {
                            UnRegisterTextRange(range);
                        }
                    }

                    break;
                }

                case NotifyCollectionChangedAction.Reset:
                {
                    if (collection != null)
                    {
                        foreach (HighlightRange range in collection)
                        {
                            UnRegisterTextRange(range);
                        }
                    }

                    break;
                }

                case NotifyCollectionChangedAction.Move:
                {
                    break;
                }

                default:
                {
                    Assert.Fail(string.Format("Unknown enum value '{1}' for {0}", (int)e.Action, typeof(NotifyCollectionChangedAction)));
                    break;
                }
            }
        }

        private void RegisterTextRange(HighlightRange range)
        {
            if (range != null)
            {
                range.Changed += OnTextRangeChanged;
                this.InvalidateVisual();
            }
        }

        private void UnRegisterTextRange(HighlightRange range)
        {
            if (range != null)
            {
                range.Changed -= OnTextRangeChanged;
                this.InvalidateVisual();
            }
        }

        private ObservableCollection<HighlightRange> ranges;
    }
}