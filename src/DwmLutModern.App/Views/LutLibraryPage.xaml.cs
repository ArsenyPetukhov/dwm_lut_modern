using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DwmLutModern.Models;

namespace DwmLutModern.Views
{
    /// <summary>
    /// LUT library code-behind. Starts a drag when the user drags a LUT card, carrying the
    /// <see cref="LutEntry"/> so the Home page's monitor cards can accept the drop.
    /// </summary>
    public partial class LutLibraryPage : UserControl
    {
        private Point _dragStart;

        public LutLibraryPage()
        {
            InitializeComponent();
        }

        private void EntryList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var position = e.GetPosition(null);
            if (_dragStart == default)
            {
                _dragStart = position;
                return;
            }

            var diff = _dragStart - position;
            if (System.Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                System.Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(EntryList, source) is ListBoxItem item &&
                item.DataContext is LutEntry entry)
            {
                DragDrop.DoDragDrop(item, new DataObject(typeof(LutEntry), entry), DragDropEffects.Copy);
            }

            _dragStart = default;
        }
    }
}
