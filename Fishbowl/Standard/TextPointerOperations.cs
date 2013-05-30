#define APPROXIMATE_HIGHLIGHT_RECTS
#define MERGE_HIGHLIGHT_RECTS

namespace Microsoft.Wpf.Samples.Documents
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Documents;
    using Standard;

    /* A BRIEF BACKGROUND on TextPointers (Ifeanyi Echeruo)
     * =================================================================================
     * 
     * TextPointers are positions between elements, sort of like the way a caret sits between characters in a text box.
     * 
     * TextPointer terms and concepts
     * 
     * Logical Direction: Forward and Backword
     *  TextPointers have a concept of direction Forward and Backword.
     *  Pointers in the Forward direction refer to then next element in reading order.
     *  Pointers in the Backward direction refer to then previous element in reading order.
     *  Reading order goes either from left to right (eg Latin text) or right to left (eg Arabic text)
     *  
     * Insertion Positions
     *  Note TextPointers are positions betweeen *elements* and not neccesarily positions between *characters*
     *  <Run>Twö</Run> has seven TextPointers
     *  Here they are represented by asterix (*)
     *  *<Run>*T*w*o*̈*</Run>*
     *  
     *  Note that elements such as <Run> are surrounded by TextPointers.
     *  Also each Unicode unit is surrounded by TextPointers (e.g the combined latin o and diaeresis)
     *  I dont know all the rules for what constitutes an insertion position but the mental model
     *  I use is if a caret in a text box would have skipped over it then it is not an insertion position
     * 
     * Elements and Units
     *  Implicitly from the definitions above TextPointers border DependencyObjects (as far as I know only TextElements) or
     *  Unicode units (UTF-16)
     *  TextPointer offsets in API's like GetOffsetFromPosition() are meaured in these units
     * 
     * Physical Bounding box: Near Edge, Far Edge
     *  TextPointers can return information about the characters surrounding them
     *  TextPointers can return the position and height of the characters bounding box
     *  More specifically they return the position and height of the box's near edge and far edge.
     *  The near edge of the box is the edge that would be occupied by a caret
     *  The far edge of the box is the edge opposite the near edge.
     *  Note that if a TextPointer is between two identical characters of different font size the near edge
     *  and the far edge will be of different heights
     *  
     * Line Start\End Positions
     *  Every position at the start of a line is the position at the end of the previous line depending 
     *  on its logical direction. 
     *  (Line start positon = start of a line), 
     *  (Line start position + Next Backward Insertion Position = End of previous line)
     *  
     * 
     * Non-obvious but important corner cases
     *  TextPointers at the egdes of a document border only one element.
     *      consider the document consisting only the text "A line"
     *      The TextPointer to the left of 'A' only has one element which is to its right.
     *      It has no backward facing far edge. 
     *      The TextPointer on the right of 'e' similarly has no forward facing far edge
     * 
     *  There may be several consecutive TextPointer positions at the start\end of a line that will return true
     *      for this.IsAtLineStartPosition. Some of them are special in that their near backward edge will be at 
     *      the end of one line but their near forward edge will be at the start of another line.
     *  
     *  The same text pointer may not occupy the same physical position.
     *      A text pointer at the end of a line or at a bidi-boundary may have several different
     *      physical locations. Consider the below physical layout which has 3 examples of where
     *      this happens.
     *      
     *         These are digits1234in a left to right line
     *         هذه هي الأرقام1234في اليسار إلى اليمين خط
     *         
     *      The first example a shared TextPointer at end of the first line and the begining of the second line. 
     *      When facing backward the TextPointer refers to the right edge of the first line.
     *      When facing forward the TextPointer refers to the left edge of the second line.
     *      Same pointer two different locations.
     *      
     *      To understand the second example you have to know that on the second line
     *          The Arabic script should be read from *right to left* 
     *          The Latin digits should be read from *left to right*
     *          Weird things happen at the points of transition to and from the Arabic and Latin digits.
     *  
     *      (Aside: 
     *          Arabic numbers refer to the Hindu\Arabic numbering system not the latin digits 0 - 9.
     *          These days Arabic script usually use Hindi digit shapes for numbers and those are read from right to left.
     *      )
     *      
     *      Consider the TextPointer on the left edge of the Latin digit 1.
     *      
     *      Its forward facing near edge is the left edge of 1
     *      Its forward facing far edge is the right edge of 1
     *      Its backward facing near edge is the right edge of the Latin digit 4.
     *      Its backward facing far edge is the right edge of the Arabic م next to 4.
     *
     *      A similar situation exists for the TextPointer on the right edge of the Latin digit 4
     *      
     *      Its forward facing near edge is the left edge of the Arabic ف next to the Latin digit 1
     *      Its forward facing far edge is the right edge of ف
     *      Its backward facing near edge is the right edge of the Latin digit 4.
     *      Its backward facing far edge is the left edge of 4
     * 
     * Misc
     *  The TextPointer API sometimes refers to TextPointers a positions (eg TextPointer.GetPositionFromOffset())
     *  They are the same thing
     *  
     */

    /// <summary>
    /// Utility class for non trivial stateless text pointer operations
    /// </summary>
    public static class TextPointerOperations
    {
        /// <summary>
        /// Returns all the insertion positions contained in range
        /// </summary>
        /// <param name="range">Range to get insertion positions for</param>
        /// <param name="direction">Direction to traverse for insertion positions</param>
        /// <returns>All the insertion positions contained in range</returns>
        public static IEnumerable<TextPointer> GetInsertionPositions(TextRange range, LogicalDirection direction)
        {
            if (range == null)
            {
                throw new ArgumentNullException("range");
            }

            TextPointer start;            
            Predicate<TextPointer> isInRange;

            switch (direction)
            {
                case LogicalDirection.Forward:
                {
                    start = range.Start.GetInsertionPosition(LogicalDirection.Forward);
                    isInRange = x => x.CompareTo(range.End) <= 0;
                    break;
                }

                case LogicalDirection.Backward:
                {
                    start = range.End.GetInsertionPosition(LogicalDirection.Backward);
                    isInRange = x => x.CompareTo(range.Start) >= 0;
                    break;
                }

                default:
                {
                    throw new InvalidEnumArgumentException("direction", (int)direction, typeof(LogicalDirection));
                }
            }

            for (
                TextPointer i = start;
                (i != null) && isInRange(i);
                i = i.GetNextInsertionPosition(direction)
                )
            {
                yield return i;
            }
        }

        /// <summary>
        /// Gets the set of line ranges contained by range
        /// </summary>
        /// <param name="range">Range to obtain lines from</param>
        /// <returns>The set of line ranges contained by range</returns>
        public static IEnumerable<TextRange> GetLineRanges(TextRange range)
        {
            if (range == null)
            {
                throw new ArgumentNullException("range");
            }

            var lineBreaks = GetLineStartPositionsInRange(range, LogicalDirection.Forward);

            var lineBreakEnumerator = lineBreaks.GetEnumerator();
            if (lineBreakEnumerator != null)
            {
                try
                {
                    if (lineBreakEnumerator.MoveNext())
                    {
                        // Head
                        TextPointer firstLineBreak = lineBreakEnumerator.Current;
                        if (range.Start.CompareTo(firstLineBreak) < 0)
                        {
                            TextRange result = GetRangeToEndOfLine(range.Start);
                            //DebugAssertIsSingleLine(result);

                            yield return result;
                        }

                        // Body
                        TextPointer prev = firstLineBreak;
                        while (lineBreakEnumerator.MoveNext())
                        {
                            TextPointer next = lineBreakEnumerator.Current;
                            TextRange result = GetRangeToEndOfLine(prev);
                            //DebugAssertIsSingleLine(result);

                            yield return result;
                            prev = next;
                        }

                        // Tail
                        TextPointer lastLineBreak = prev;
                        if (range.End.CompareTo(lastLineBreak) > 0)
                        {
                            TextRange result = new TextRange(lastLineBreak, range.End);
                            //DebugAssertIsSingleLine(result);

                            yield return result;
                        }
                    }
                    else
                    {
                        // Single line without line breaks corner case
                        TextRange result = new TextRange(range.Start, range.End);
                        //DebugAssertIsSingleLine(result);

                        yield return result;
                    }
                }
                finally
                {
                    lineBreakEnumerator.Dispose();
                }
            }
        }

        /// <summary>
        /// Gets the position at the end of the line containing pointer
        /// </summary>
        /// <param name="pointer">Position on the line being queried</param>
        /// <returns>The position at the end of the line containing pointer</returns>
        public static TextPointer GetLineEndPosition(TextPointer pointer)
        {
            if (pointer == null)
            {
                throw new ArgumentNullException("pointer");
            }

            TextPointer result;

            int actualCount;
            TextPointer endOfLine = pointer.GetLineStartPosition(1, out actualCount);

            if (actualCount != 1)
            {
                result = pointer.DocumentEnd;
            }
            else
            {
                // There may be several consecutive line boundary positions
                // only one of them is really useful to us.
                // It's the line start position whose forward and backward character rects are on
                // different lines.
                // The fastest way to find such a position without testing character rects it to 
                // walk backward searching for the first position that is no a line start position.
                do
                {
                    endOfLine = endOfLine.GetPositionAtOffset(-1);
                }
                while (endOfLine != null && endOfLine.IsAtLineStartPosition);

                result = endOfLine ?? pointer.DocumentEnd;
            }

            return result;
        }

        /// <summary>
        /// Get a TextRange from lineStart to the end of the line
        /// </summary>
        /// <param name="lineStart">Start of the text range</param>
        /// <returns>A TextRange from lineStart to the end of the line</returns>
        public static TextRange GetRangeToEndOfLine(TextPointer lineStart)
        {
            if (lineStart == null)
            {
                throw new ArgumentNullException("lineStart");
            }

            return new TextRange(lineStart, GetLineEndPosition(lineStart));
        }

        /// <summary>
        /// Get the number of lines contained in the range
        /// </summary>
        /// <param name="range">Range to count</param>
        /// <returns>The number of lines contained in the range</returns>
        public static int GetLineCount(TextRange range)
        {
            if (range == null)
            {
                throw new ArgumentNullException("range");
            }

            return GetLineRanges(range).Count();
        }

        /// <summary>
        /// Gets the physical line infoormation contained by range
        /// </summary>
        /// <param name="range">range to obtain physical line information for</param>
        /// <returns>The physical line info contained by range</returns>
        public static IEnumerable<PhysicalLineInfo> GetPhysicalLines(TextRange range)
        {
            if (range == null)
            {
                throw new ArgumentNullException("range");
            }

            return from lineRange in GetLineRanges(range)
                   select new PhysicalLineInfo(lineRange);
        }

        /// <summary>
        /// Gets a set of rectangles suitable for highlighting a TextRange
        /// </summary>
        /// <param name="range">Range to obtain highlight rects for</param>
        /// <returns>A set of rectangles suitable for highlighting a TextRange</returns>
        public static IEnumerable<Rect> GetHighlightRectangles(TextRange range)
        {
            if (range == null)
            {
                throw new ArgumentNullException("range");
            }

            return GetHighlightRectangleFromLines(range, GetLineHighlightRectangles);
        }

        /// <remarks>
        /// Uses an optimization that ignores bidi lines by assuming 
        /// the first text pointer and last text pointer on a line correspond to 
        /// the near and far vertical edges of that line.
        /// </remarks>
        public static IEnumerable<Rect> GetApproximateHighlightRectangles(TextRange range)
        {
            if (range == null)
            {
                throw new ArgumentNullException("range");
            }

            return GetHighlightRectangleFromLines(range, GetApproximatLineHighlightRectangles);
        }

        private static IEnumerable<TextPointer> GetLineStartPositionsInRange(TextRange range, LogicalDirection direction)
        {
            if (range == null)
            {
                throw new ArgumentNullException("range");
            }

            int requestedLineCount;
            TextPointer start;
            Predicate<TextPointer> isInRange;
            Func<TextPointer, TextPointer, bool> hasAdvanced;

            switch (direction)
            {
                case LogicalDirection.Forward:
                {
                    requestedLineCount = 1;
                    start = range.Start;
                    isInRange = x => x.CompareTo(range.End) <= 0;
                    hasAdvanced = (prevPos, nextPos) => nextPos.CompareTo(prevPos) > 0;
                    break;
                }

                case LogicalDirection.Backward:
                {
                    requestedLineCount = -1;
                    start = range.End;
                    isInRange = x => x.CompareTo(range.Start) >= 0;
                    hasAdvanced = (prevPos, nextPos) => nextPos.CompareTo(prevPos) < 0;
                    break;
                }

                default:
                {
                    throw new InvalidEnumArgumentException("direction", (int)direction, typeof(LogicalDirection));
                }
            }

            int expectedActualLineCount = Math.Abs(requestedLineCount);

            if (start.IsAtLineStartPosition)
            {
                yield return start;
            }

            TextPointer prev = start;

            while (true)
            {
                int actualLineCount;
                TextPointer next = prev.GetLineStartPosition(requestedLineCount, out actualLineCount);

                if (!isInRange(next))
                {
                    // We have gone passed the end of the range
                    break;
                }

                if (actualLineCount < expectedActualLineCount)
                {
                    // No more lines
                    break;
                }

                if (!hasAdvanced(prev, next))
                {
                    // BUG?
                    // next < prev, We did not actually advance so exit to prevent an infinite loop
                    break;
                }

                yield return next;

                prev = next;
            }
        }

        /// <summary>
        /// Gets a set of rectangles suitable for highlighting a TextRange
        /// </summary>
        /// <param name="range">Range to obtain highlight rects for</param>
        /// <param name="contentHost">Optional IContent host used to performance</param>
        /// <returns>A set of rectangles suitable for highlighting a TextRange</returns>
        public static IEnumerable<Rect> GetHighlightRectanglesWithContentHost(
            TextRange range,
            Func<TextRange, IEnumerable<Rect>> getRectanglesCore,
            IContentHost contentHost
            )
        {
            if (getRectanglesCore == null)
            {
                throw new ArgumentNullException("getRectanglesCore");
            }

            IEnumerable<Rect> result;

            if (contentHost == null)
            {
                result = getRectanglesCore(range);
            }
            else
            {
                result = new Rect[] { };
                const LogicalDirection fwd = LogicalDirection.Forward;
                TextPointer current = range.Start;

                TextElement te = current.GetAdjacentElement(fwd) as TextElement;
                while (te != null && te.ContentEnd.CompareTo(range.End) >= 0)
                {
                    if (current.CompareTo(te.ElementStart) < 0)
                    {
                        TextRange leadingRange = new TextRange(current, te.ElementStart);
                        IEnumerable<Rect> leadingRects = getRectanglesCore(leadingRange);
                        result = result.Concat(leadingRects);
                        current = te.ElementStart;
                    }

                    if (contentHost != null)
                    {
                        result = result.Concat(contentHost.GetRectangles(te));
                    }

                    current = te.ElementEnd;
                    te = current.GetAdjacentElement(fwd) as TextElement;
                }

                TextRange trailingRange = new TextRange(current, range.End);
                IEnumerable<Rect> trailingRects = getRectanglesCore(trailingRange);
                result = result.Concat(trailingRects);
            }

            return result;
        }


        private static IEnumerable<Rect> GetHighlightRectangleFromLines(
            TextRange range,
            Func<TextRange, IEnumerable<Rect>> getHilightRectFromLineRange
            )
        {
            Assert.IsNotNull(range);
            Assert.IsNotNull(getHilightRectFromLineRange);

            IEnumerable<Rect[]> rectsPerLine = 
                from lineRange in TextPointerOperations.GetLineRanges(range)
                select getHilightRectFromLineRange(lineRange).ToArray();

            Rect[][] rectsPerLineArray = rectsPerLine.ToArray();
            AlignLineTops(rectsPerLineArray);

            return MergeConsecutiveAdjacentRects(rectsPerLineArray.SelectMany(x => x));
        }

        private static IEnumerable<Rect> GetLineHighlightRectangles(TextRange range)
        {
            DebugAssertIsSingleLine(range);

            var charRects = 
                from charInfo in new PhysicalLineInfo(range).Characters
                select charInfo.GetBounds();

            return MergeConsecutiveAdjacentRects(charRects);
        }

        private static IEnumerable<Rect> GetApproximatLineHighlightRectangles(TextRange range)
        {
            //DebugAssertIsSingleLine(range);

            Rect result;
            if (!range.IsEmpty && (!range.Start.IsAtLineStartPosition || !range.End.IsAtLineStartPosition))
            {
                result = Rect.Union(
                    range.Start.GetCharacterRect(LogicalDirection.Forward),
                    range.End.GetCharacterRect(LogicalDirection.Backward)
                );
            }
            else
            {
                // The normal case generates incorrect results if range.Start and range.End
                // refer to the ambiguous start\end of line TextPointer
                result = range.Start.GetCharacterRect(LogicalDirection.Forward);
            }

            yield return result;
        }

        /// <summary>
        /// Ensure the top of each line is equal to the bottom of the previous line
        /// </summary>
        /// <param name="rectsPerLineArray"></param>
        private static void AlignLineTops(Rect[][] rectsPerLineArray)
        {
            double prevMinBottom = double.PositiveInfinity;

            foreach (Rect[] lineRects in rectsPerLineArray)
            {
                double minBottom = double.PositiveInfinity;

                for (int i = 0; i < lineRects.Length; i++)
                {
                    if (!lineRects[i].IsEmpty)
                    {
                        double bottom = lineRects[i].Bottom;
                        minBottom = Math.Min(minBottom, bottom);
                        if (prevMinBottom < lineRects[i].Y)
                        {
                            lineRects[i].Y = prevMinBottom;
                            lineRects[i].Height = bottom - prevMinBottom;
                        }
                    }
                }

                prevMinBottom = minBottom;
            }
        }


        /// <remarks>
        /// A few large rects is much better for performance than a lot of small rects
        /// Replace consecutive rects with a single rect that occupies the same outline
        /// </remarks>
        private static IEnumerable<Rect> MergeConsecutiveAdjacentRects(IEnumerable<Rect> rects)
        {
#if MERGE_HIGHLIGHT_RECTS
            Rect lineRect = Rect.Empty;
            foreach (Rect rect in rects)
            {
                Rect newLineRect;
                if (TryExtendRect(lineRect, rect, out newLineRect))
                {
                    lineRect = newLineRect;
                }
                else
                {
                    if (!lineRect.IsEmpty)
                    {
                        yield return lineRect;
                    }

                    lineRect = rect;
                }
            }

            if (!lineRect.IsEmpty)
            {
                yield return lineRect;
            }
#else
            return rects;
#endif
        }

        /// <summary>
        /// Computes the union of a and b if and only if 
        /// b is adjacent to a (they both have a side that touches the other rect)
        /// the union of 'a' and 'b' makes 'a' wider or taller (but not both)
        /// </summary>
        /// <param name="a">Rectangle to extend</param>
        /// <param name="b">Rectangle to extend by</param>
        /// <param name="result">Union of a and b if b extends a</param>
        /// <returns>True if the operation succeded</returns>
        private static bool TryExtendRect(Rect a, Rect b, out Rect result)
        {
            if (a.IsEmpty)
            {
                result = b;
                return true;
            }

            if (b.IsEmpty)
            {
                result = a;
                return true;
            }

            if (AreEqual(a.Top, b.Top) && AreEqual(a.Bottom, b.Bottom))
            {
                // a Can be extended to the left or right

                if (AreEqual(a.Right, b.Left))
                {
                    // a Can be extended to the right
                    result = new Rect(a.Left, a.Top, Math.Max(0.0, b.Right - a.Left), a.Height);
                    return true;
                }

                if (AreEqual(a.Left, b.Right))
                {
                    // a Can be extended to the left
                    result = new Rect(b.Left, a.Top, Math.Max(0.0, a.Right - b.Left), a.Height);
                    return true;
                }
            }
            else
            {
                if (AreEqual(a.Left, b.Left) && AreEqual(a.Right, b.Right))
                {
                    // a Can be extended up or down

                    if (AreEqual(a.Bottom, b.Top))
                    {
                        // a Can be extended down
                        result = new Rect(a.Left, a.Top, a.Width, Math.Max(0.0, b.Bottom - a.Top));
                        return true;
                    }

                    if (AreEqual(a.Top, b.Bottom))
                    {
                        // a Can be extended up
                        result = new Rect(a.Left, b.Top, a.Width, Math.Max(0.0, a.Bottom - b.Top));
                        return true;
                    }
                }
            }

            result = Rect.Empty;
            return false;
        }

        private static bool AreEqual(double a, double b)
        {
            const double epsilon = 0.000001;

            double diff = a - b;
            return (diff >= 0.0) ? (diff < epsilon) : (-diff < epsilon);
        }

        [Conditional("DEBUG")]
        public static void DebugAssertIsSingleLine(TextRange range)
        {
            // If range.Start and range.End are on the same line the GetLineStartPosition(0)
            // should return the same value for both range.Start and range.End
            TextPointer lineStartFromRangeStart = range.Start.GetLineStartPosition(0);
            TextPointer lineStartFromRangeEnd = range.End.GetLineStartPosition(0);

            if (lineStartFromRangeStart != null && lineStartFromRangeEnd != null)
            {
                // Range must not span more than 1 line
                Assert.AreEqual(0, lineStartFromRangeStart.GetOffsetToPosition(lineStartFromRangeEnd));
            }
        }
    }

    // Exposes a IEnumerable as a sequence of previous, next pairs
    internal struct PrevNext<T>
    {
        public PrevNext(T prev, T next)
        {
            this.Prev = prev;
            this.Next = next;
        }

        public static IEnumerable<PrevNext<T>> AsPrevNext(IEnumerable<T> enumerable)
        {
            if (enumerable == null)
            {
                throw new ArgumentNullException("enumerable");
            }

            var enumerator = enumerable.GetEnumerator();
            if (enumerator != null)
            {
                try
                {
                    if (enumerator.MoveNext())
                    {
                        T prev = enumerator.Current;
                        while (enumerator.MoveNext())
                        {
                            T next = enumerator.Current;
                            yield return new PrevNext<T>(prev, next);
                            prev = next;
                        }
                    }
                }
                finally
                {
                    enumerator.Dispose();
                }
            }
        }

        public readonly T Prev;
        public readonly T Next;
    }
}
