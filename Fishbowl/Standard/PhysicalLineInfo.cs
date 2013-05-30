namespace Microsoft.Wpf.Samples.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Documents;
    using Standard;

    /// <summary>
    /// Exposes physical hit testing information for text ranges within a line
    /// </summary>
    public class PhysicalLineInfo
    {
        internal PhysicalLineInfo(TextRange lineRange)
        {
            if (lineRange == null)
            {
                throw new ArgumentNullException("lineRange");
            }

            TextPointerOperations.DebugAssertIsSingleLine(lineRange);

            var charPositions = TextPointerOperations.GetInsertionPositions(lineRange, LogicalDirection.Forward);
            var charPositionPairs = PrevNext<TextPointer>.AsPrevNext(charPositions);
            this.charHitTestInfo = from pair in charPositionPairs
                                   select new PhysicalCharInfo(pair.Prev, pair.Next);
        }

        /// <summary>
        /// Gets the pointer who's character
        /// </summary>
        /// <param name="point"></param>
        /// <param name="snapToText"></param>
        /// <returns></returns>
        public PhysicalCharInfo HitTest(Point point, bool snapToText)
        {
            Rect bounds;
            return HitTest(point, snapToText, out bounds);
        }

        public PhysicalCharInfo HitTest(Point point, bool snapToText, out Rect bounds)
        {
            PhysicalCharInfo result = null;

            bounds = Rect.Empty;

            double nearestDistance = double.PositiveInfinity;
            PhysicalCharInfo nearest = null;
            PhysicalCharInfo xResult = null;
            bool yResult = false;

            PhysicalCharInfo prevChar = null;
            foreach (var currChar in this.Characters)
            {
                Rect charBounds = currChar.GetBounds();
                bounds.Union(charBounds);

                if (!yResult)
                {
                    yResult = !bounds.IsEmpty && bounds.Top <= point.Y && point.Y < bounds.Bottom;
                    // If true we have found a line that overlaps the hit-test point on the y-axis
                }

                if (xResult == null)
                {
                    TextPointer candidate = currChar.HitTestHorizontal(point);
                    if (candidate != null)
                    {
                        if (candidate.GetOffsetToPosition(currChar.NearPosition) == 0)
                        {
                            xResult = currChar;
                        }
                        else
                        {
                            Assert.IsNotNull(prevChar);
                            Assert.AreEqual(0, candidate.GetOffsetToPosition(prevChar.NearPosition));

                            xResult = prevChar;
                        }
                    }
                }

                if (yResult && xResult != null)
                {
                    // We have found a character that is in range of the hit-test point on the x-axis and y-axis
                    result = xResult;
                    break;
                }
                else
                {
                    // Even if the the hit test point is outside this lines vertical range we will keep searching because
                    // We may find a character further down the line that extends the lines vertical range and makes the 
                    // hit test result valid.
                }

                if (snapToText)
                {
                    double distance = Math.Abs(charBounds.X - point.X);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = currChar;
                    }
                }

                prevChar = currChar;
            }

            if (result == null)
            {
                if (snapToText && yResult)
                {
                    result = nearest;
                }
            }
            else
            {
                Assert.IsTrue(bounds.Contains(point));
            }

            return result;
        }

        /// <summary>
        /// Layout information for the characters on the line
        /// </summary>
        public IEnumerable<PhysicalCharInfo> Characters
        {
            get { return this.charHitTestInfo; }
        }

        /// <summary>
        /// The tight bounding box of the line
        /// </summary>
        /// <returns>The tight bounding box of the line</returns>
        public Rect GetBounds()
        {
            return this.charHitTestInfo.Aggregate(
                Rect.Empty,
                (rect, charInfo) => Union(rect, charInfo.GetBounds())
            );
        }

        private static Rect Union(Rect a, Rect b)
        {
            if (a.IsEmpty)
            {
                return b;
            }

            if (b.IsEmpty)
            {
                return a;
            }

            return Rect.Union(a, b);
        }

        private readonly IEnumerable<PhysicalCharInfo> charHitTestInfo;
    }
}
