Imports DevExpress.Mvvm.UI
Imports DevExpress.Mvvm.UI.Interactivity
Imports DevExpress.Xpf.Editors
Imports DevExpress.Xpf.Editors.Internal
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows
Imports System.Windows.Documents
Imports System.Windows.Input
Imports System.Windows.Media

Namespace T558732
    Friend Class DragDropBehavior
        Inherits Behavior(Of TokenEditor)

        Private dragStartPoint As Point
        Private adornedElement As TokenEditorPresenter
        Private ReadOnly Property ownerEdit() As LookUpEditBase
            Get
                Return AssociatedObject.OwnerEdit
            End Get
        End Property

        Protected Overrides Sub OnAttached()
            MyBase.OnAttached()
            AssociatedObject.AllowDrop = True
            AddHandler AssociatedObject.PreviewMouseLeftButtonDown, AddressOf AssociatedObject_PreviewMouseLeftButtonDown
            AddHandler AssociatedObject.PreviewMouseMove, AddressOf AssociatedObject_PreviewMouseMove
            AddHandler AssociatedObject.DragLeave, AddressOf AssociatedObject_DragLeave
            AddHandler AssociatedObject.DragOver, AddressOf AssociatedObject_DragOver
            AddHandler AssociatedObject.Drop, AddressOf AssociatedObject_Drop
        End Sub

        Private Sub AssociatedObject_DragLeave(ByVal sender As Object, ByVal e As DragEventArgs)
            If Not IsOverAssociatedObject(e) Then
                RemoveAdorner()
            End If
        End Sub
        Private Function IsOverAssociatedObject(ByVal e As DragEventArgs) As Boolean
            Dim hitTest = VisualTreeHelper.HitTest(AssociatedObject, e.GetPosition(AssociatedObject))
            Return (hitTest?.VisualHit IsNot Nothing AndAlso LayoutTreeHelper.GetVisualParents(hitTest?.VisualHit).OfType(Of TokenEditor)().FirstOrDefault() Is AssociatedObject)
        End Function

        Private Sub AssociatedObject_Drop(ByVal sender As Object, ByVal e As DragEventArgs)
            RemoveAdorner()
            If e.Data.GetDataPresent(GetType(CustomItem)) Then
                Dim item = TryCast(e.Data.GetData(GetType(CustomItem)), CustomItem)
                If TypeOf ownerEdit.EditValue Is List(Of Object) Then
                    InsertValue(item.EditValue, GetDropTarget(e))
                Else
                    ownerEdit.SetCurrentValue(BaseEdit.EditValueProperty, New List(Of Object)() From {item.EditValue})
                End If
            End If
        End Sub

        Private Sub InsertValue(ByVal value As Object, ByVal dropTarget As Tuple(Of TokenEditorPresenter, Position))
            Dim editValue = TryCast(ownerEdit.EditValue, List(Of Object))
            If editValue Is Nothing Then
                Return
            End If
            Dim newIndex = editValue.IndexOf(dropTarget.Item1.Item.EditValue)
            If dropTarget.Item2 = Position.Right Then
                newIndex += 1
            End If
            Dim currentIndex = editValue.IndexOf(value)
            If currentIndex <> -1 AndAlso currentIndex < newIndex Then
                newIndex -= 1
            End If
            Dim result = New List(Of Object)(editValue)
            result.Remove(value)
            result.Insert(newIndex, value)
            ownerEdit.SetCurrentValue(BaseEdit.EditValueProperty, result)
        End Sub

        Private Sub RemoveAdorner()
            If adornedElement Is Nothing Then
                Return
            End If
            Dim adornerLayer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(adornedElement)
            Dim adorners = adornerLayer?.GetAdorners(adornedElement)
            If adorners IsNot Nothing Then
                For Each item In adorners
                    If TypeOf item Is PositionAdorner Then
                        adornerLayer.Remove(item)
                        Return
                    End If
                Next item
            End If
        End Sub

        Private Sub AssociatedObject_DragOver(ByVal sender As Object, ByVal e As DragEventArgs)
            Dim dropTarget = GetDropTarget(e)
            If dropTarget IsNot Nothing Then
                AddAdorner(dropTarget.Item1, dropTarget.Item2)
            End If
        End Sub

        Private Function GetDropTarget(ByVal e As DragEventArgs) As Tuple(Of TokenEditorPresenter, Position)
            Dim tokenEditorPresenter = GetPresenterParent(TryCast(e.OriginalSource, DependencyObject))
            If tokenEditorPresenter Is Nothing Then
                tokenEditorPresenter = FindNearestPresenter(e.GetPosition(AssociatedObject))
                If tokenEditorPresenter Is Nothing Then
                    Return Nothing
                End If
            End If
            Dim bounds = tokenEditorPresenter.TransformToVisual(AssociatedObject).TransformBounds(New Rect(tokenEditorPresenter.RenderSize))
            Dim position = GetPosition(bounds, e.GetPosition(AssociatedObject))
            Return Tuple.Create(tokenEditorPresenter, position)
        End Function

        Private Function FindNearestPresenter(ByVal p As Point) As TokenEditorPresenter
            Dim panel = LayoutTreeHelper.GetVisualChildren(AssociatedObject).OfType(Of TokenEditorPanel)().FirstOrDefault()
            If panel?.Children Is Nothing Then
                Return Nothing
            End If
            Dim distance = Double.MaxValue
            Dim presenter As TokenEditorPresenter = Nothing
            For i As Integer = 0 To panel.Children.Count - 1
                Dim child = TryCast(panel.Children(i), TokenEditorPresenter)
                If child.IsNewTokenEditorPresenter Then
                    Continue For
                End If
                Dim bounds = child.TransformToVisual(AssociatedObject).TransformBounds(New Rect(child.RenderSize))
                Dim center = New Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0)
                Dim length = (center - p).LengthSquared
                If distance > length Then
                    distance = length
                    presenter = child
                End If
            Next i
            Return presenter
        End Function

        Private Sub AddAdorner(ByVal tokenEditorPresenter As TokenEditorPresenter, ByVal position As Position)
            RemoveAdorner()
            Dim adornerLayer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(tokenEditorPresenter)
            adornerLayer.Add(New PositionAdorner(tokenEditorPresenter, position))
            adornedElement = tokenEditorPresenter
        End Sub

        Private Function GetPosition(ByVal bounds As Rect, ByVal point As Point) As Position
            Return If(point.X < (bounds.X + bounds.Width / 2.0), Position.Left, Position.Right)
        End Function

        Private Sub AssociatedObject_PreviewMouseMove(ByVal sender As Object, ByVal e As MouseEventArgs)
            Dim currentPoint = e.GetPosition(AssociatedObject)
            If e.LeftButton = MouseButtonState.Pressed AndAlso (Math.Abs(currentPoint.X - dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance OrElse Math.Abs(dragStartPoint.Y - currentPoint.Y) > SystemParameters.MinimumVerticalDragDistance) Then
                Dim hitTest = VisualTreeHelper.HitTest(AssociatedObject, dragStartPoint)
                Dim tokenEditorPresenter = GetPresenterParent(hitTest.VisualHit)
                If tokenEditorPresenter IsNot Nothing Then
                    DragDrop.DoDragDrop(AssociatedObject, tokenEditorPresenter.Item, DragDropEffects.Move)
                End If
            End If
        End Sub

        Private Function GetPresenterParent(ByVal child As DependencyObject) As TokenEditorPresenter
            Dim tokenEditorPresenter = LayoutTreeHelper.GetVisualParents(child).OfType(Of TokenEditorPresenter)().FirstOrDefault()
            If tokenEditorPresenter IsNot Nothing AndAlso Not tokenEditorPresenter.IsNewTokenEditorPresenter Then
                Return tokenEditorPresenter
            End If
            Return Nothing
        End Function

        Private Sub AssociatedObject_PreviewMouseLeftButtonDown(ByVal sender As Object, ByVal e As System.Windows.Input.MouseButtonEventArgs)
            dragStartPoint = e.GetPosition(AssociatedObject)
        End Sub

        Protected Overrides Sub OnDetaching()
            RemoveHandler AssociatedObject.PreviewMouseLeftButtonDown, AddressOf AssociatedObject_PreviewMouseLeftButtonDown
            RemoveHandler AssociatedObject.PreviewMouseMove, AddressOf AssociatedObject_PreviewMouseMove
            RemoveHandler AssociatedObject.DragLeave, AddressOf AssociatedObject_DragLeave
            RemoveHandler AssociatedObject.DragOver, AddressOf AssociatedObject_DragOver
            RemoveHandler AssociatedObject.Drop, AddressOf AssociatedObject_Drop
            MyBase.OnDetaching()
        End Sub
    End Class

    Public Enum Position
        Left
        Right
    End Enum

    Public Class PositionAdorner
        Inherits Adorner

        Public Sub New(ByVal adornedElement As UIElement, ByVal position As Position)
            MyBase.New(adornedElement)
            Me.Position = position
            IsHitTestVisible = True
        End Sub
        Public Property Position() As Position
        Protected Overrides Sub OnRender(ByVal drawingContext As DrawingContext)
            Dim adornedElementRect = New Rect(AdornedElement.RenderSize)
            Dim pen = New Pen(Brushes.Black, 2.0)
            Select Case Position
                Case Position.Left
                    drawingContext.DrawLine(pen, adornedElementRect.TopLeft, adornedElementRect.BottomLeft)
                Case Position.Right
                    drawingContext.DrawLine(pen, adornedElementRect.TopRight, adornedElementRect.BottomRight)
            End Select
        End Sub
    End Class
End Namespace
