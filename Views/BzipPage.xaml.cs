// BzipPage.xaml.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Runtime.InteropServices;
using Windows.Foundation;

namespace REToolBox.Views
{
    public sealed partial class BzipPage : Page
    {
        private bool isDragging = false;
        private Point dragStartPoint;
        private Point cursorOriginalPosition;
        private IntPtr targetWindowHandle = IntPtr.Zero;
        private string targetWindowTitle = "";
        private Point lastValidPosition;
        private Pointer currentPointer;

        public BzipPage()
        {
            this.InitializeComponent();
            this.Loaded += BzipPage_Loaded;
        }

        private void BzipPage_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void CursorContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            StartDrag(e.GetCurrentPoint(null).Position, e);
            e.Handled = true;
        }

        private void CursorContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (isDragging)
            {
                UpdateCursorPosition(e.GetCurrentPoint(null).Position);
            }
        }

        private void CursorContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            EndDrag(e.GetCurrentPoint(null).Position);
        }

        private void CursorContainer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (isDragging)
            {
                CancelDrag();
            }
        }

        private void DragOverlay_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (isDragging)
            {
                UpdateCursorPosition(e.GetCurrentPoint(null).Position);
            }
        }

        private void DragOverlay_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (isDragging)
            {
                EndDrag(e.GetCurrentPoint(null).Position);
            }
        }

        private void StartDrag(Point startPoint, PointerRoutedEventArgs e)
        {
            isDragging = true;
            dragStartPoint = startPoint;
            currentPointer = e.Pointer;

            var transform = CursorContainer.TransformToVisual(MainContainer);
            cursorOriginalPosition = transform.TransformPoint(new Point(0, 0));

            lastValidPosition = cursorOriginalPosition;

            DragOverlay.Visibility = Visibility.Visible;
            CursorContainer.CapturePointer(currentPointer);
        }

        private void UpdateCursorPosition(Point currentPoint)
        {
            if (!isDragging) return;

            var newPosition = new Point(
                cursorOriginalPosition.X + (currentPoint.X - dragStartPoint.X),
                cursorOriginalPosition.Y + (currentPoint.Y - dragStartPoint.Y)
            );

            var boundedPosition = ConstrainToWindow(newPosition);

            Canvas.SetLeft(CursorContainer, boundedPosition.X);
            Canvas.SetTop(CursorContainer, boundedPosition.Y);

            lastValidPosition = boundedPosition;
        }

        private Point ConstrainToWindow(Point position)
        {
            var maxX = MainContainer.ActualWidth - CursorContainer.Width;
            var maxY = MainContainer.ActualHeight - CursorContainer.Height;

            var constrainedX = Math.Max(0, Math.Min(maxX, position.X));
            var constrainedY = Math.Max(0, Math.Min(maxY, position.Y));

            return new Point(constrainedX, constrainedY);
        }

        private void EndDrag(Point endPoint)
        {
            if (!isDragging) return;

            isDragging = false;
            DragOverlay.Visibility = Visibility.Collapsed;

            try
            {
                CursorContainer.ReleasePointerCapture(currentPointer);
            }
            catch { }

            var cursorCenter = new Point(
                lastValidPosition.X + CursorContainer.Width / 2,
                lastValidPosition.Y + CursorContainer.Height / 2
            );

            targetWindowHandle = WindowFromPoint(new POINT((int)cursorCenter.X, (int)cursorCenter.Y));

            if (targetWindowHandle != IntPtr.Zero)
            {
                var title = new System.Text.StringBuilder(256);
                GetWindowText(targetWindowHandle, title, title.Capacity);
                targetWindowTitle = title.ToString();

                if (!string.IsNullOrEmpty(targetWindowTitle))
                {
                    ShowWindowSelection(cursorCenter);
                }
                else
                {
                    ResetCursor();
                }
            }
            else
            {
                ResetCursor();
            }
        }

        private void CancelDrag()
        {
            isDragging = false;
            DragOverlay.Visibility = Visibility.Collapsed;
            ResetCursor();
        }

        private void ResetCursor()
        {
            var centerX = (MainContainer.ActualWidth - CursorContainer.Width) / 2;
            var centerY = (MainContainer.ActualHeight - CursorContainer.Height) / 2;

            Canvas.SetLeft(CursorContainer, centerX);
            Canvas.SetTop(CursorContainer, centerY);

            SelectionIndicator.Visibility = Visibility.Collapsed;
            ToolsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowWindowSelection(Point cursorPosition)
        {
            WindowTitleText.Text = targetWindowTitle;
            SelectionIndicator.Visibility = Visibility.Visible;

            Canvas.SetLeft(SelectionIndicator, cursorPosition.X + 30);
            Canvas.SetTop(SelectionIndicator, cursorPosition.Y - SelectionIndicator.ActualHeight / 2);

            ToolsPanel.Visibility = Visibility.Visible;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ResetCursor();
        }

        private void FullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (targetWindowHandle != IntPtr.Zero)
            {
                SetWindowFullScreen(targetWindowHandle);
                ResetCursor();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (targetWindowHandle != IntPtr.Zero)
            {
                ShowWindow(targetWindowHandle, 6);
                ResetCursor();
            }
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (targetWindowHandle != IntPtr.Zero)
            {
                ShowWindow(targetWindowHandle, 3);
                ResetCursor();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (targetWindowHandle != IntPtr.Zero)
            {
                PostMessage(targetWindowHandle, 0x0010, IntPtr.Zero, IntPtr.Zero);
                ResetCursor();
            }
        }

        private void TopmostButton_Click(object sender, RoutedEventArgs e)
        {
            if (targetWindowHandle != IntPtr.Zero)
            {
                SetWindowPos(targetWindowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                ResetCursor();
            }
        }

        private void SetWindowFullScreen(IntPtr hwnd)
        {
            var screenWidth = (int)Microsoft.UI.Windowing.DisplayArea.Primary.WorkArea.Width;
            var screenHeight = (int)Microsoft.UI.Windowing.DisplayArea.Primary.WorkArea.Height;

            SetWindowPos(hwnd, HWND_TOP, 0, 0, screenWidth, screenHeight, SWP_SHOWWINDOW);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_TOP = new IntPtr(0);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
    }
}