using System;
using System.Windows;
using System.Windows.Controls;

namespace RichCanvasToa
{
    /// <summary>
    /// ItemsHost of <see cref="RichItemsControl"/>
    /// </summary>
    public class RichCanvas : Panel
    {
        internal RichItemsControl? ItemsOwner { get; set; }
        internal RichItemContainer? BottomElement { get; set; }
        internal RichItemContainer? RightElement { get; set; }
        internal RichItemContainer? TopElement { get; set; }
        internal RichItemContainer? LeftElement { get; set; }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size constraint)
        {
            if (ItemsOwner != null && (ItemsOwner.IsDrawing || ItemsOwner.IsSelecting || ItemsOwner.IsDragging))
            {
                return default;
            }

            foreach (UIElement child in InternalChildren)
            {
                var container = (RichItemContainer)child;
                container.Measure(constraint);
            }

            return default;
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size arrangeSize)
        {
            foreach (UIElement child in InternalChildren)
            {
                if (child is RichItemContainer container)
                {
                    child.Arrange(new Rect(new Point(container.Left, container.Top), child.DesiredSize));
                    var firstElement = InternalChildren[0] as RichItemContainer;
                    if (container.IsValid())
                    {
                        container.CalculateBoundingBox();
                        BottomElement = firstElement;
                        RightElement = firstElement;
                        TopElement = firstElement;
                        LeftElement = firstElement;
                    }
                }
            }

            ItemsOwner?.ScrollContainer?.SetCurrentScroll();
            return arrangeSize;
        }
    }
}
