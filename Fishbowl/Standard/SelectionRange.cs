namespace Microsoft.Wpf.Samples.Documents
{
    using System;
    using System.Windows.Documents;

    public class SelectionRange
    {
        public SelectionRange(TextView view, TextPointer anchor)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            if (anchor == null)
            {
                throw new ArgumentNullException("anchor");
            }

            this.TextRange = new TextRange(anchor, anchor);
            this.TextRange.Changed += HandleTextRangeChanged;
            HandleTextRangeChanged(this.TextRange, EventArgs.Empty);

            this.HighlightRange = new HighlightRange(view, this.TextRange);
        }

        public TextPointer Anchor
        {
            get
            {
                return this.anchor;
            }
        }

        public TextPointer Extent
        {
            get
            {
                return this.extent;
            }
        }

        public event EventHandler Changed
        {
            add { this.TextRange.Changed += value; }
            remove { this.TextRange.Changed -= value; }
        }

        public void Reset(TextPointer newAnchor)
        {
            if (newAnchor == null)
            {
                throw new ArgumentNullException("newAnchor");
            }

            this.TextRange.Select(newAnchor, newAnchor);
        }

        public void Extend(TextPointer newExtent)
        {
            if (newExtent == null)
            {
                throw new ArgumentNullException("newExtent");
            }

            if(this.Anchor.CompareTo(newExtent) <= 0)
            {
                this.isExtendingBackward = false;
                this.TextRange.Select(this.Anchor, newExtent);
            }
            else 
            {
                this.isExtendingBackward = true;
                this.TextRange.Select(newExtent, this.Anchor);
            }
        }

        private void HandleTextRangeChanged(object sender, EventArgs e)
        {
            if (isExtendingBackward)
            {
                this.anchor = this.TextRange.End;
                this.extent = this.TextRange.Start;
            }
            else
            {
                this.anchor = TextRange.Start;
                this.extent = TextRange.End;
            }
        }

        bool isExtendingBackward;

        public readonly TextRange TextRange;
        public readonly HighlightRange HighlightRange;

        private TextPointer anchor;
        private TextPointer extent;
    }
}
