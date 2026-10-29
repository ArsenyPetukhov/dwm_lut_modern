using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace DwmLutModern.Views
{
    public partial class DocumentationPage : UserControl
    {
        public DocumentationPage() => InitializeComponent();

        public void OpenTopic(string topic)
        {
            var panel = DocumentScroll.Content as Panel;
            if (panel == null) return;
            foreach (var child in panel.Children)
            {
                if (child is not Expander expander) continue;
                var selected = string.Equals(expander.Tag as string, topic, StringComparison.Ordinal);
                expander.IsExpanded = selected;
                if (selected) Dispatcher.BeginInvoke(new Action(() => expander.BringIntoView()));
            }
        }

        private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { }
            e.Handled = true;
        }
    }
}
