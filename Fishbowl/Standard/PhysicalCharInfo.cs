namespace Microsoft.Wpf.Samples.Documents
{
    using System;
    using System.Windows;
    using System.Windows.Documents;

    /// <summary>
    /// Exposes physical hit testing information for logical pointer positions
    /// </summary>
    public class PhysicalCharInfo
    {
        /// <summary>
        /// Create a PhysicalCharInfo for the character between position and the next forward insertion position
        /// </summary>
        /// <param name="position"></param>
        public PhysicalCharInfo(TextPointer position)
        {
            if (position == null)
            {
                throw new ArgumentNullException("position");
            }

            this.NearPosition = null;
            this.FarPosition = null;

            Initialize(position, position.GetNextInsertionPosition(LogicalDirection.Forward) ?? position);
        }

        internal PhysicalCharInfo(TextPointer position, TextPointer nextFwdInsertionPosition)
        {
            this.NearPosition = null;
            this.FarPosition = null;

            Initialize(position, nextFwdInsertionPosition);
        }

        private void Initialize(TextPointer position, TextPointer nextFwdInsertionPosition)
        {
            this.NearPosition = SetDirection(position, LogicalDirection.Forward);
            this.FarPosition = SetDirection(nextFwdInsertionPosition, LogicalDirection.Backward);
        }

        /// <summary>
        /// Test to see if the point falls inside the pointer or its neighbour's bounding rects
        /// </summary>
        /// <param name="point"></param>
        /// <param name="curr"></param>
        /// <returns>The pointer or neighbout that contains point or null</returns>
        internal TextPointer HitTestHorizontal(Point point)
        {
            double charLeftEdge;
            double charRightEdge;
            TextPointer leftPointer;
            TextPointer rightPointer;

            double nearX = GetNearEdge().X;
            double farX = GetFarEdge().X;

            // Convert from logical terms to physical terms
            if (nearX <= farX)
            {
                // Left to right
                charLeftEdge = nearX;
                charRightEdge = farX;
                leftPointer = this.NearPosition;
                rightPointer = this.FarPosition;
            }
            else
            {
                // Right to left
                charLeftEdge = farX;
                charRightEdge = nearX;
                leftPointer = this.FarPosition;
                rightPointer = this.NearPosition;
            }

            double charXCenter = (charLeftEdge + charRightEdge) / 2.0;

            if (charLeftEdge <= point.X && point.X < charXCenter)
            {
                return leftPointer;
            }
            else if (charXCenter <= point.X && point.X < charRightEdge)
            {
                return rightPointer;
            }

            return null;
        }

        // The method can be quite expensive performance wise so as a hint to the user we expose it as a method
        public Rect GetBounds()
        {
            return Rect.Union(GetNearEdge(), GetFarEdge());
        }

        // The method can be quite expensive performance wise so as a hint to the user we expose it as a method
        public Rect GetNearEdge()
        {
            return this.NearPosition.GetCharacterRect(LogicalDirection.Forward);
        }

        // The method can be quite expensive performance wise so as a hint to the user we expose it as a method
        public Rect GetFarEdge()
        {
            return this.FarPosition.GetCharacterRect(LogicalDirection.Backward);
        }

        private static TextPointer SetDirection(TextPointer pointer, LogicalDirection direction)
        {
            return (pointer.LogicalDirection == direction) ? pointer : pointer.GetPositionAtOffset(0, direction);
        }

        public TextPointer NearPosition
        {
            get;
            private set;
        }

        public TextPointer FarPosition
        {
            get;
            private set;
        }
    }
}
