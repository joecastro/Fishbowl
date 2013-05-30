#define USE_APPROXIMATE_HIGHLIGHT_RECT

namespace Microsoft.Wpf.Samples.Documents
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Media;
    using Standard;

    /// <summary>
    /// Abstraction that maps TextPointer positions to physical x,y coordinates and vice versa
    /// </summary>
    public abstract class TextView : IDisposable
    {
        /// <summary>
        /// Fired whenever a change occurs in the content that could
        /// invalidate previous information returned by the text view
        /// </summary>
        public abstract event EventHandler<EventArgs> Changed;

        /// <summary>
        /// Gets a text pointer who's character rect contains the given point.
        /// </summary>
        /// <param name="point">Point to hit test</param>
        /// <param name="snapToText">If true returns the pointer nearest to the input point</param>
        /// <returns></returns>
        public abstract TextPointer GetPositionFromPoint(Point point, bool snapToText);

        /// <summary>
        /// Obtains a geometry suitable for higlighting a text range in the view
        /// </summary>
        public abstract Geometry GetHilightGeometry(TextRange range);

        protected virtual void Dispose(bool disposing)
        {
        }

        void IDisposable.Dispose()
        {
            GC.SuppressFinalize(this);
            this.Dispose(true);
        }

        internal static IEnumerable<Rect> GetHighlightRects(TextRange range, IContentHost contentHost)
        {
#if USE_APPROXIMATE_HIGHLIGHT_RECT
            return TextPointerOperations.GetHighlightRectanglesWithContentHost(
                range,
                TextPointerOperations.GetApproximateHighlightRectangles,
                contentHost
                );
#else
            return TextPointerOperations.GetHighlightRectanglesWithContentHost(
                range,
                TextPointerOperations.GetHighlightRectangles,
                contentHost
                );
#endif
        }
    }

    /// <summary>
    /// Text view with default implementations for hit test methods
    /// </summary>
    public abstract class GenericTextView : TextView
    {
        public override TextPointer GetPositionFromPoint(Point point, bool snapToText)
        {
            TextRange contentRange = null;
            

            // Using the IContentHost interface we can quickly narrow down our
            // range to a run within the text block
            IContentHost contentHost = this.ContentHost;
            if (contentHost != null)
            {
                TextElement te = contentHost.InputHitTest(point) as TextElement;
                if (te != null)
                {
                    contentRange = new TextRange(te.ContentStart, te.ContentEnd);
                }
            }

            if(contentRange == null)
            {
                contentRange = new TextRange(Start, End);
            }

            TextPointer result = GetPositionFromPoint(point, contentRange, snapToText);

            Assert.Implies(snapToText, result != null);

            return result;
        }

        public override Geometry GetHilightGeometry(TextRange range)
        {
            Geometry result = Geometry.Empty;

            if (range != null)
            {
                IEnumerable<Rect> rects = GetHighlightRects(range, this.ContentHost);

                IEnumerable<Geometry> rectGeometries = from rect in rects
                                                       select FrozenGeometryFromRect(rect);

                GeometryCollection collection = new GeometryCollection(rectGeometries);
                collection.Freeze();

                result = new GeometryGroup() { Children = collection };
                result.Freeze();
            }

            return result;
        }

        /// <summary>
        /// The start of the range to provide hit testing services for
        /// </summary>
        protected abstract TextPointer Start { get; }

        /// <summary>
        /// The end of the range to provide hit testing services for
        /// </summary>
        protected abstract TextPointer End { get; }

        /// <summary>
        /// IContentHost that hosts the range defined by the Start and End properties
        /// </summary>
        protected virtual IContentHost ContentHost
        {
            get { return null; }
        }

        /// <summary>
        /// Gets a text pointer who's character rect contains the given point.
        /// </summary>
        /// <param name="point">Point to hit test</param>
        /// <param name="range">Range to hit test over</param>
        /// <param name="snapToText">If true returns the pointer nearest to the input point</param>
        /// <returns></returns>
        protected virtual TextPointer GetPositionFromPoint(Point point, TextRange range, bool snapToText)
        {
            TextPointer result = GetPositionFromPointByBisection(point, range);

            if (result == null)
            {
                // GetPositionFromPointByBisection can miss characters on bi-di lines
                // it also does not support snap to nearest
                // so we fallback on the much slower but more correct GetPositionFromPointByLinearScan
                result = GetPositionFromPointByLinearScan(point, range, snapToText);
            }

            Assert.Implies(snapToText, result != null);
            return result;
        }

        /// <remarks>
        /// Often correct for LTR text with fairly uniform height and much much faster than a linear scan
        /// </remarks>
        private static TextPointer GetPositionFromPointByBisection(Point point, TextRange range)
        {
            TextRange currRange = new TextRange(range.Start, range.End);

            int currSpan;
            do
            {
                currSpan = GetSpan(currRange);

                if (currSpan < 1)
                {
                    break;
                }

                TextPointer mid = currRange.Start.GetPositionAtOffset(currSpan / 2);
                mid = mid.GetInsertionPosition(prevCharDirection);
                if (mid == null)
                {
                    break;
                }

                PhysicalCharInfo midCharInfo = new PhysicalCharInfo(mid);
                Rect nearEdge = midCharInfo.GetNearEdge();
                Rect farEdge = midCharInfo.GetFarEdge();
                bool isLeftToRight = nearEdge.X <= farEdge.X;
                Rect midRect = Rect.Union(nearEdge, farEdge);

                if (!midRect.IsEmpty)
                {
                    if (midRect.Top > point.Y)
                    {
                        if (BisectRange(currRange, mid, PhysicalDirection.Up, isLeftToRight))
                        {
                            continue;
                        }
                        break;
                    }

                    if (midRect.Bottom < point.Y)
                    {
                        if (BisectRange(currRange, mid, PhysicalDirection.Down, isLeftToRight))
                        {
                            continue;
                        }
                        break;
                    }

                    if (midRect.Left > point.X)
                    {
                        if (BisectRange(currRange, mid, PhysicalDirection.Left, isLeftToRight))
                        {
                            continue;
                        }
                        break;
                    }

                    if (midRect.Right < point.X)
                    {
                        if (BisectRange(currRange, mid, PhysicalDirection.Right, isLeftToRight))
                        {
                            continue;
                        }
                        break;
                    }
                }

                return midCharInfo.HitTestHorizontal(point);
            }
            while (GetSpan(currRange) < currSpan);

            return null;
        }

        private static bool BisectRange(TextRange range, TextPointer midPoint, PhysicalDirection direction, bool isLeftToRight)
        {
#if DEBUG
            int span = GetSpan(range);
#endif
            switch (direction)
            {
                case PhysicalDirection.Up:
                {
                    TextPointer newEnd = isLeftToRight ? midPoint : midPoint.GetNextInsertionPosition(prevCharDirection);

                    if (newEnd == null)
                    {
                        return false;
                    }
                        
                    range.Select(range.Start, newEnd);
                    break;
                }

                case PhysicalDirection.Down:
                {
                    TextPointer newStart = isLeftToRight ? midPoint.GetNextInsertionPosition(nextCharDirection) : midPoint;
                    if (newStart == null)
                    {
                        return false;
                    }

                    range.Select(newStart, range.End);
                    break;
                }

                case PhysicalDirection.Left:
                {
                    if (isLeftToRight)
                    {
                        range.Select(range.Start, midPoint);
                    }
                    else
                    {
                        TextPointer newStart = midPoint.GetNextInsertionPosition(nextCharDirection);
                        if (newStart == null)
                        {
                            return false;
                        }
                        range.Select(newStart, range.End);
                    }
                    break;
                }

                case PhysicalDirection.Right:
                {
                    if (isLeftToRight)
                    {
                        TextPointer newStart = midPoint.GetNextInsertionPosition(nextCharDirection);
                        if (newStart == null)
                        {
                            return false;
                        }
                        range.Select(newStart, range.End);
                    }
                    else
                    {
                        range.Select(range.Start, midPoint);
                    }
                    break;
                }

                default:
                {
                    throw new InvalidEnumArgumentException("direction", (int)direction, typeof(PhysicalDirection));
                }
            }

#if DEBUG
            // Bisection must not increase the span of the range
            Assert.IsTrue(GetSpan(range) <= span); 
#endif

            return true;
        }

        /// <remarks>
        /// Can be quite slow for very, very long lines of text
        /// </remarks>
        private static TextPointer GetPositionFromPointByLinearScan(Point point, TextRange range, bool snapToText)
        {
            PhysicalCharInfo result = null;

            var lineInfos = TextPointerOperations.GetPhysicalLines(range).ToArray();

            switch(lineInfos.Length)
            {
                case 0:
                {
                    result = snapToText ? new PhysicalCharInfo(range.Start) : null;
                    break;
                }

                case 1:
                {
                    result = TopToBottomLineHitTest(lineInfos.First(), point, snapToText, range.Start);
                    break;
                }

                default:
                {
                    // Optimization, scan the last line to quickly determine if the point is below the bottom line
                    result = BottomToTopLineHitTest(lineInfos.Last(), point, snapToText, range.End);

                    if (result == null)
                    {
                        // scan lines from top to bottom
                        var fwdLineResults = from lineInfo in lineInfos.Take(lineInfos.Length - 1)
                                             select TopToBottomLineHitTest(lineInfo, point, snapToText, range.Start);


                        result = fwdLineResults.FirstOrDefault(x => x != null);
                    }

                    break;
                }
            }

            Assert.Implies(snapToText, result != null);

            return (result != null) ? result.NearPosition : null;
        }

        private static PhysicalCharInfo TopToBottomLineHitTest(PhysicalLineInfo lineInfo, Point point, bool snapToNearest, TextPointer start)
        {
            Rect lineBounds;
            PhysicalCharInfo result = lineInfo.HitTest(point, snapToNearest, out lineBounds);

            if (result == null)
            {
                if (lineBounds.IsEmpty || point.Y < lineBounds.Top)
                {
                    // The point is above the top of the current line
                    // since lines are being scanned from top to bottom 
                    // no other line can contain the point.
                    // Snap to the beginning of the range

                    if (snapToNearest)
                    {
                        result = new PhysicalCharInfo(start);
                    }
                }
            }

            return result;
        }

        private static PhysicalCharInfo BottomToTopLineHitTest(PhysicalLineInfo lineInfo, Point point, bool snapToNearest, TextPointer end)
        {
            Rect lineBounds;
            PhysicalCharInfo result = lineInfo.HitTest(point, snapToNearest, out lineBounds);

            if (result == null)
            {
                if (lineBounds.IsEmpty || point.Y > lineBounds.Bottom)
                {
                    // The point is below the bottom of the current line
                    // since lines are being scanned from bottom to top
                    // no other line can contain the point.
                    // snap to the end of the range

                    if (snapToNearest)
                    {
                        result = new PhysicalCharInfo(end);
                    }
                }
            }

            return result;
        }

        private static int GetSpan(TextRange range)
        {
            return range.Start.GetOffsetToPosition(range.End);
        }

        private static void AddAsNonEmptyFrozen(ICollection<Geometry> collection, Geometry item)
        {
            if (!item.IsEmpty())
            {
                if (!item.IsFrozen)
                {
                    item.Freeze();
                }

                collection.Add(item);
            }
        }

        private static Geometry FrozenGeometryFromRect(Rect rect)
        {
            Geometry result = new RectangleGeometry(rect);
            result.Freeze();
            return result;
        }

        // Bisection happens on whole characters not the TextPointers that sit 
        // between characters.
        // The two constanst below are used to clarify which character a pointer refers to
        private const LogicalDirection nextCharDirection = LogicalDirection.Forward;
        private const LogicalDirection prevCharDirection = LogicalDirection.Backward;

        private enum PhysicalDirection
        {
            Left,
            Right,
            Up,
            Down
        }
    }

    /// <summary>
    /// TextView specialization for TextBlock
    /// </summary>
    public class TextBlockTextView : TextView
    {
        public TextBlockTextView(TextBlock textBlock)
        {
            if (textBlock == null)
            {
                throw new ArgumentNullException("textBlock");
            }

            this.textBlock = textBlock;

            this.range = new TextRange(
                this.textBlock.ContentStart.DocumentStart,
                this.textBlock.ContentEnd.DocumentEnd
            );

            this.textBlock.SizeChanged += HandleRangeChanged;
            this.range.Changed += HandleRangeChanged;
        }

        public override event EventHandler<EventArgs> Changed;

        public override TextPointer GetPositionFromPoint(Point point, bool snapToText)
        {
            return this.textBlock.GetPositionFromPoint(point, snapToText);
        }

        public override Geometry GetHilightGeometry(TextRange range)
        {
            Geometry result = Geometry.Empty;

            if (range != null)
            {
                IEnumerable<Rect> rects = GetHighlightRects(range, this.textBlock);

                IEnumerable<Geometry> rectGeometries = from rect in rects
                                                       select FrozenGeometryFromRect(rect);

                GeometryCollection collection = new GeometryCollection(rectGeometries);
                collection.Freeze();

                result = new GeometryGroup() { Children = collection };
                result.Freeze();
            }

            return result;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (this.range != null)
                {
                    this.range.Changed -= HandleRangeChanged;
                    this.range = null;
                }

                if (this.textBlock != null)
                {
                    this.textBlock.SizeChanged -= HandleRangeChanged;
                    this.textBlock = null;
                }

                this.Changed = null;
            }
        }

        private static Geometry FrozenGeometryFromRect(Rect rect)
        {
            Geometry result = new RectangleGeometry(rect);
            result.Freeze();
            return result;
        }

        private void HandleRangeChanged(object sender, EventArgs e)
        {
            EventHandler<EventArgs> h = Changed;
            if (h != null)
            {
                h(this, EventArgs.Empty);
            }
        }

        private TextRange range;
        private TextBlock textBlock;
    }
}
