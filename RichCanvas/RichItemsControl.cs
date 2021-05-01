﻿using RichCanvas.Helpers;
using RichCanvas.Gestures;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Linq;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;

namespace RichCanvas
{
    [TemplatePart(Name = DrawingPanelName, Type = typeof(Panel))]
    [TemplatePart(Name = SelectionRectangleName, Type = typeof(Rectangle))]
    public class RichItemsControl : ItemsControl
    {
        public delegate void DrawEndedEventHandler(object context);
        public event DrawEndedEventHandler OnDrawEnded;

        private const string DrawingPanelName = "PART_Panel";
        private const string SelectionRectangleName = "PART_SelectionRectangle";
        private const string CanvasContainerName = "CanvasContainer";

        private RichCanvas _mainPanel;
        private Line _bottomLimitLine;
        private PanningGrid _canvasContainer;
        private bool _isDrawing;
        private Gestures.Drawing _drawingGesture;
        private Selecting _selectingGesture;
        private DispatcherTimer _autoPanTimer;
        private Point _previousMousePosition;

        internal bool HasSelections => _selectingGesture.HasSelections;
        internal RichCanvas ItemsHost => _mainPanel;
        internal PanningGrid ScrollContainer => _canvasContainer;

        public static DependencyProperty MousePositionProperty = DependencyProperty.Register("MousePosition", typeof(Point), typeof(RichItemsControl));
        public Point MousePosition
        {
            get => (Point)GetValue(MousePositionProperty);
            set => SetValue(MousePositionProperty, value);
        }

        public static DependencyProperty SelectionRectangleProperty = DependencyProperty.Register("SelectionRectangle", typeof(Rect), typeof(RichItemsControl));
        public Rect SelectionRectangle
        {
            get => (Rect)GetValue(SelectionRectangleProperty);
            set => SetValue(SelectionRectangleProperty, value);
        }

        public static DependencyProperty IsSelectingProperty = DependencyProperty.Register("IsSelecting", typeof(bool), typeof(RichItemsControl));
        public bool IsSelecting
        {
            get => (bool)GetValue(IsSelectingProperty);
            set => SetValue(IsSelectingProperty, value);
        }

        public static DependencyProperty AppliedTransformProperty = DependencyProperty.Register("AppliedTransform", typeof(TransformGroup), typeof(RichItemsControl));
        public TransformGroup AppliedTransform
        {
            get => (TransformGroup)GetValue(AppliedTransformProperty);
            set => SetValue(AppliedTransformProperty, value);
        }

        public static DependencyProperty DisableAutoPanningProperty = DependencyProperty.Register("DisableAutoPanning", typeof(bool), typeof(RichItemsControl), new FrameworkPropertyMetadata(true, OnDisableAutoPanningChanged));

        public bool DisableAutoPanning
        {
            get => (bool)GetValue(DisableAutoPanningProperty);
            set => SetValue(DisableAutoPanningProperty, value);
        }
        public double TopLimit { get; set; }
        public double RightLimit { get; set; }
        public double BottomLimit { get; set; }
        public double LeftLimit { get; set; }
        public bool IsPanning { get; private set; }

        public bool IsZooming { get; private set; }

        static RichItemsControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RichItemsControl), new FrameworkPropertyMetadata(typeof(RichItemsControl)));
        }
        public RichItemsControl()
        {
            AppliedTransform = new TransformGroup()
            {
                Children = new TransformCollection
                {
                    new ScaleTransform(), new TranslateTransform()
                }
            };
            DragBehavior.ItemsControl = this;
            _selectingGesture = new Selecting
            {
                Context = this
            };
            _drawingGesture = new Gestures.Drawing(this);
        }
        internal void UpdateSelections()
        {
            _selectingGesture.UpdateSelectionsPosition();
            AdjustScroll();
        }

        internal void AdjustScroll()
        {
            ScrollContainer.AdjustScrollVertically();
            ScrollContainer.AdjustScrollHorizontally();
        }
        public override void OnApplyTemplate()
        {
            _mainPanel = GetTemplateChild(DrawingPanelName) as RichCanvas;

            _canvasContainer = GetTemplateChild(CanvasContainerName) as PanningGrid;
            _canvasContainer.Initalize(this);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl)
            {
                IsZooming = true;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl)
            {
                IsZooming = false;
            }
        }

        protected override bool IsItemItsOwnContainerOverride(object item) => item is RichItemContainer;

        protected override DependencyObject GetContainerForItemOverride() => new RichItemContainer
        {
            RenderTransform = new TransformGroup()
            {
                Children = new TransformCollection { new ScaleTransform(), new TranslateTransform() }
            },
            IsHitTestVisible = true
        };

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.Space))
            {
                IsPanning = true;
                Cursor = Cursors.Hand;
            }
            else
            {
                if (!VisualHelper.IsScrollBarParent((DependencyObject)e.OriginalSource))
                {
                    for (int i = 0; i < this.Items.Count; i++)
                    {
                        RichItemContainer container = (RichItemContainer)this.ItemContainerGenerator.ContainerFromIndex(i);
                        // already drawn
                        if (container.Height != 0 && container.Width != 0)
                        {
                            container.IsDrawn = true;
                        }

                        if (!container.IsDrawn)
                        {
                            _drawingGesture.OnMouseDown(container, e);
                            _isDrawing = true;
                            CaptureMouse();
                            break;
                        }
                    }
                    if (!_isDrawing && !DragBehavior.IsDragging && !IsPanning)
                    {
                        IsSelecting = true;
                        _selectingGesture.OnMouseDown(e);
                        CaptureMouse();
                    }
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_isDrawing)
            {
                _isDrawing = false;
                var drawnItem = _drawingGesture.OnMouseUp();
                OnDrawEnded?.Invoke(drawnItem.DataContext);
            }
            else if (!DragBehavior.IsDragging && IsSelecting)
            {
                IsSelecting = false;
            }
            if (IsPanning)
            {
                Cursor = Cursors.Arrow;
                IsPanning = false;
            }
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
            Focus();
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            MousePosition = new Point(e.GetPosition(_mainPanel).X, e.GetPosition(_mainPanel).Y);

            if (_isDrawing)
            {
                _drawingGesture.OnMouseMove(e);
            }
            else if (IsSelecting)
            {
                _selectingGesture.OnMouseMove(e);

                var geom = new RectangleGeometry(new Rect(SelectionRectangle.Left, SelectionRectangle.Top, SelectionRectangle.Width, SelectionRectangle.Height));
                _selectingGesture.UnselectAll();

                VisualTreeHelper.HitTest(_mainPanel, null,
                    new HitTestResultCallback(OnHitTestResultCallback),
                    new GeometryHitTestParameters(geom));
            }
        }

        private static void OnDisableAutoPanningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((RichItemsControl)d).OnDisableAutoPanningChanged((bool)e.NewValue);

        private void OnDisableAutoPanningChanged(bool shouldDisable)
        {
            if (!shouldDisable)
            {
                if (_autoPanTimer == null)
                {
                    _autoPanTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(1), DispatcherPriority.Background, new EventHandler(HandleAutoPanning), Dispatcher);
                    _autoPanTimer.Start();
                }
                else
                {
                    _autoPanTimer.Interval = TimeSpan.FromMilliseconds(1);
                    _autoPanTimer.Start();
                }
            }
        }

        private void HandleAutoPanning(object sender, EventArgs e)
        {
            if (IsMouseOver && Mouse.LeftButton == MouseButtonState.Pressed && Mouse.Captured != null && !IsMouseCapturedByScrollBar() && !IsPanning)
            {
                var mousePosition = Mouse.GetPosition(ScrollContainer);
                if (mousePosition.Y < 0)
                {
                    if (_isDrawing)
                    {
                        if (mousePosition.Y <= _previousMousePosition.Y)
                        {
                            _drawingGesture.UpdateCurrentItem(1);
                        }
                        else
                        {
                            _drawingGesture.UpdateCurrentItem(-1);
                        }
                        if (TopLimit > _drawingGesture.GetCurrentItemTop() - _drawingGesture.GetCurrentItemHeight())
                        {
                            TopLimit = _drawingGesture.GetCurrentItemTop() - _drawingGesture.GetCurrentItemHeight();
                        }
                        if (BottomLimit == 0)
                        {
                            BottomLimit = (_drawingGesture.GetCurrentItemTop() - _drawingGesture.GetCurrentItemHeight()) + _drawingGesture.GetCurrentItemHeight();
                        }
                    }
                    ScrollContainer.PanVertically(1, true);
                }
                else if (mousePosition.Y > ScrollContainer.ViewportHeight)
                {
                    if (_isDrawing)
                    {
                        if (mousePosition.Y >= _previousMousePosition.Y)
                        {
                            _drawingGesture.UpdateCurrentItem(1);
                        }
                        else
                        {
                            _drawingGesture.UpdateCurrentItem(-1);
                        }
                        if (BottomLimit < _drawingGesture.GetCurrentItemTop() + _drawingGesture.GetCurrentItemHeight())
                        {
                            BottomLimit = _drawingGesture.GetCurrentItemTop() + _drawingGesture.GetCurrentItemHeight();
                        }
                        if (TopLimit == 0)
                        {
                            TopLimit = _drawingGesture.GetCurrentItemTop();
                        }
                    }
                    ScrollContainer.PanVertically(-1, true);
                }
                _previousMousePosition = mousePosition;
            }
        }

        private HitTestResultBehavior OnHitTestResultCallback(HitTestResult result)
        {
            if (result.VisualHit is RichItemContainer container)
            {
                container.IsSelected = true;
                _selectingGesture.AddSelection(container);
            }
            return HitTestResultBehavior.Continue;
        }

        private bool IsMouseCapturedByScrollBar()
        {
            return Mouse.Captured.GetType() == typeof(Thumb) || Mouse.Captured.GetType() == typeof(RepeatButton);
        }
    }
}
