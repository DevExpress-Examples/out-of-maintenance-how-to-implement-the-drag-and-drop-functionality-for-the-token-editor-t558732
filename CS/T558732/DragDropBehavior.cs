using DevExpress.Mvvm.UI;
using DevExpress.Mvvm.UI.Interactivity;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace T558732 {
    class DragDropBehavior : Behavior<TokenEditor> {
        Point dragStartPoint;
        TokenEditorPresenter adornedElement;
        LookUpEditBase ownerEdit { get { return AssociatedObject.OwnerEdit; } }

        protected override void OnAttached() {
            base.OnAttached();
            AssociatedObject.AllowDrop = true;
            AssociatedObject.PreviewMouseLeftButtonDown += AssociatedObject_PreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove += AssociatedObject_PreviewMouseMove;
            AssociatedObject.DragLeave += AssociatedObject_DragLeave;
            AssociatedObject.DragOver += AssociatedObject_DragOver;
            AssociatedObject.Drop += AssociatedObject_Drop;
        }

        void AssociatedObject_DragLeave(object sender, DragEventArgs e) {
            if (!IsOverAssociatedObject(e))
                RemoveAdorner();
        }
        bool IsOverAssociatedObject(DragEventArgs e) {
            var hitTest = VisualTreeHelper.HitTest(AssociatedObject, e.GetPosition(AssociatedObject));
            return (hitTest?.VisualHit != null
                && LayoutTreeHelper.GetVisualParents(hitTest?.VisualHit).OfType<TokenEditor>().FirstOrDefault() == AssociatedObject);
        }

        void AssociatedObject_Drop(object sender, DragEventArgs e) {
            RemoveAdorner();
            if (e.Data.GetDataPresent(typeof(CustomItem))) {
                var item = e.Data.GetData(typeof(CustomItem)) as CustomItem;
                if (ownerEdit.EditValue is List<object>)
                    InsertValue(item.EditValue, GetDropTarget(e));
                else
                    ownerEdit.EditValue = new List<object>() { item.EditValue };
            }
        }

        void InsertValue(object value, Tuple<TokenEditorPresenter, Position> dropTarget) {
            var editValue = ownerEdit.EditValue as List<object>;
            if (editValue == null)
                return;
            var newIndex = editValue.IndexOf(dropTarget.Item1.Item.EditValue);
            if (dropTarget.Item2 == Position.Right)
                newIndex++;
            var currentIndex = editValue.IndexOf(value);
            if (currentIndex != -1 && currentIndex < newIndex)
                newIndex--;
            var result = new List<object>(editValue);
            result.Remove(value);
            result.Insert(newIndex, value);
            ownerEdit.EditValue = result;
        }

        void RemoveAdorner() {
            if (adornedElement == null)
                return;
            var adornerLayer = AdornerLayer.GetAdornerLayer(adornedElement);
            var adorners = adornerLayer?.GetAdorners(adornedElement);
            if (adorners != null) {
                foreach (var item in adorners) {
                    if (item is PositionAdorner) {
                        adornerLayer.Remove(item);
                        return;
                    }
                }
            }
        }

        void AssociatedObject_DragOver(object sender, DragEventArgs e) {
            var dropTarget = GetDropTarget(e);
            if (dropTarget != null)
                AddAdorner(dropTarget.Item1, dropTarget.Item2);
        }

        Tuple<TokenEditorPresenter, Position> GetDropTarget(DragEventArgs e) {
            var tokenEditorPresenter = GetPresenterParent(e.OriginalSource as DependencyObject);
            if (tokenEditorPresenter == null) {
                tokenEditorPresenter = FindNearestPresenter(e.GetPosition(AssociatedObject));
                if (tokenEditorPresenter == null)
                    return null;
            }
            var bounds = tokenEditorPresenter.TransformToVisual(AssociatedObject).TransformBounds(new Rect(tokenEditorPresenter.RenderSize));
            var position = GetPosition(bounds, e.GetPosition(AssociatedObject));
            return Tuple.Create(tokenEditorPresenter, position);
        }

        TokenEditorPresenter FindNearestPresenter(Point p) {
            var panel = LayoutTreeHelper.GetVisualChildren(AssociatedObject).OfType<TokenEditorPanel>().FirstOrDefault();
            if (panel?.Children == null)
                return null;
            var distance = double.MaxValue;
            TokenEditorPresenter presenter = null;
            for (int i = 0; i < panel.Children.Count; i++) {
                var child = panel.Children[i] as TokenEditorPresenter;
                if (child.IsNewTokenEditorPresenter)
                    continue;
                var bounds = child.TransformToVisual(AssociatedObject).TransformBounds(new Rect(child.RenderSize));
                var center = new Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0);
                var length = (center - p).LengthSquared;
                if (distance > length) {
                    distance = length;
                    presenter = child;
                }
            }
            return presenter;
        }

        void AddAdorner(TokenEditorPresenter tokenEditorPresenter, Position position) {
            RemoveAdorner();
            var adornerLayer = AdornerLayer.GetAdornerLayer(tokenEditorPresenter);
            adornerLayer.Add(new PositionAdorner(tokenEditorPresenter, position));
            adornedElement = tokenEditorPresenter;
        }

        Position GetPosition(Rect bounds, Point point) {
            return (point.X < (bounds.X + bounds.Width / 2.0)) ? Position.Left : Position.Right;
        }

        void AssociatedObject_PreviewMouseMove(object sender, MouseEventArgs e) {
            var currentPoint = e.GetPosition(AssociatedObject);
            if (e.LeftButton == MouseButtonState.Pressed
                && (Math.Abs(currentPoint.X - dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance
                || Math.Abs(dragStartPoint.Y - currentPoint.Y) > SystemParameters.MinimumVerticalDragDistance)) {
                var hitTest = VisualTreeHelper.HitTest(AssociatedObject, dragStartPoint);
                var tokenEditorPresenter = GetPresenterParent(hitTest.VisualHit);
                if (tokenEditorPresenter != null)
                    DragDrop.DoDragDrop(AssociatedObject, tokenEditorPresenter.Item, DragDropEffects.Move);
            }
        }

        TokenEditorPresenter GetPresenterParent(DependencyObject child) {
            var tokenEditorPresenter = LayoutTreeHelper.GetVisualParents(child).OfType<TokenEditorPresenter>().FirstOrDefault();
            if (tokenEditorPresenter != null && !tokenEditorPresenter.IsNewTokenEditorPresenter)
                return tokenEditorPresenter;
            return null;
        }

        void AssociatedObject_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            dragStartPoint = e.GetPosition(AssociatedObject);
        }

        protected override void OnDetaching() {
            AssociatedObject.PreviewMouseLeftButtonDown -= AssociatedObject_PreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= AssociatedObject_PreviewMouseMove;
            AssociatedObject.DragLeave -= AssociatedObject_DragLeave;
            AssociatedObject.DragOver -= AssociatedObject_DragOver;
            AssociatedObject.Drop -= AssociatedObject_Drop;
            base.OnDetaching();
        }
    }

    public enum Position {
        Left,
        Right
    }

    public class PositionAdorner : Adorner {
        public PositionAdorner(UIElement adornedElement, Position position) : base(adornedElement) {
            Position = position;
            IsHitTestVisible = true;
        }
        public Position Position { get; set; }
        protected override void OnRender(DrawingContext drawingContext) {
            var adornedElementRect = new Rect(AdornedElement.RenderSize);
            var pen = new Pen(Brushes.Black, 2.0);
            switch (Position) {
                case Position.Left:
                    drawingContext.DrawLine(pen, adornedElementRect.TopLeft, adornedElementRect.BottomLeft);
                    break;
                case Position.Right:
                    drawingContext.DrawLine(pen, adornedElementRect.TopRight, adornedElementRect.BottomRight);
                    break;
            }
        }
    }
}
