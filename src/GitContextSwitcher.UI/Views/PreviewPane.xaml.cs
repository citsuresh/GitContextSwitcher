using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GitContextSwitcher.UI.ViewModels;

namespace GitContextSwitcher.UI.Views
{
    public partial class PreviewPane : System.Windows.Controls.UserControl
    {
        private int _lastAppliedGeneration = 0;
        public PreviewPane()
        {
            InitializeComponent();
            this.DataContextChanged += PreviewPane_DataContextChanged;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Hide when close clicked - consumer can remove from visual tree or collapse parent
                this.Visibility = Visibility.Collapsed;
                if (this.DataContext is PreviewViewModel pvm)
                {
                    pvm.FilePreview = null;
                }
            }
            catch { }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is ViewModels.PreviewViewModel vm)
                {
                    if (e.OriginalSource is System.Windows.Controls.TreeView tv)
                    {
                        if (tv.SelectedItem is ViewModels.PreviewViewModel.FileTreeNode node)
                        {
                            vm.SelectedNode = node;
                            // Try to update preview diff viewer when selection changes
                            try
                            {
                                var viewer = this.FindName("PreviewDiffViewer") as object;
                                if (viewer != null)
                                {
                                    // Defer to the viewmodel to compute side-by-side model via DiffPlex on selection change
                                    // Use dispatcher to ensure UI thread
                                    if (!Dispatcher.CheckAccess()) Dispatcher.Invoke(() => { /* nothing, ViewModel will update */ });
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        private void PreviewPane_DataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (e.OldValue is INotifyPropertyChanged oldVm)
                {
                    oldVm.PropertyChanged -= Vm_PropertyChanged;
                }
                if (e.NewValue is INotifyPropertyChanged newVm)
                {
                    newVm.PropertyChanged += Vm_PropertyChanged;
                }
                // Reset last applied generation whenever data context changes
                try { System.Threading.Interlocked.Exchange(ref _lastAppliedGeneration, 0); } catch { }
            }
            catch { }
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                // Trigger diff rebuild only once per completed load cycle by listening to DiffGeneration changes.
                if (e.PropertyName == nameof(PreviewViewModel.DiffGeneration))
                {
                    // Capture current texts and generation on UI thread then build/apply diff on background thread
                    try
                    {
                        string oldText = string.Empty;
                        string newText = string.Empty;
                        var generation = 0;

                        if (!Dispatcher.CheckAccess())
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (this.DataContext is PreviewViewModel v)
                                {
                                    oldText = v.DiffOld ?? string.Empty;
                                    newText = v.DiffNew ?? string.Empty;
                                    generation = v.DiffGeneration;
                                }
                            });
                        }
                        else
                        {
                            if (this.DataContext is PreviewViewModel v)
                            {
                                oldText = v.DiffOld ?? string.Empty;
                                newText = v.DiffNew ?? string.Empty;
                                generation = v.DiffGeneration;
                            }
                        }

                        _ = Task.Run(() =>
                        {
                            try
                            {
                                var builder = new DiffPlex.DiffBuilder.SideBySideDiffBuilder(new DiffPlex.Differ());
                                var model = builder.BuildDiffModel(oldText, newText);

                                // Apply model on UI thread but ensure only the newest generation is applied once.
                                if (!Dispatcher.CheckAccess())
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        try
                                        {
                                            var currentGen = (this.DataContext as PreviewViewModel)?.DiffGeneration ?? 0;
                                            if (currentGen != generation) return; // stale

                                            // Atomically bump last applied generation only if newer
                                            int prev;
                                            do
                                            {
                                                prev = _lastAppliedGeneration;
                                                if (generation <= prev) return; // another newer already applied
                                            }
                                            while (System.Threading.Interlocked.CompareExchange(ref _lastAppliedGeneration, generation, prev) != prev);

                                            ApplyModelToPreviewViewer(model);
                                        }
                                        catch { }
                                    });
                                }
                                else
                                {
                                    try
                                    {
                                        var currentGen = (this.DataContext as PreviewViewModel)?.DiffGeneration ?? 0;
                                        if (currentGen != generation) return;

                                        int prev;
                                        do
                                        {
                                            prev = _lastAppliedGeneration;
                                            if (generation <= prev) return;
                                        }
                                        while (System.Threading.Interlocked.CompareExchange(ref _lastAppliedGeneration, generation, prev) != prev);

                                        ApplyModelToPreviewViewer(model);
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }


        private void ApplyModelToPreviewViewer(DiffPlex.DiffBuilder.Model.SideBySideDiffModel model)
        {
            try
            {
                var obj = this.FindName("PreviewDiffViewer");
                if (obj == null) return;
                var dv = obj;
                var dvType = dv.GetType();
                var modelProp = dvType.GetProperty("Model") ?? dvType.GetProperty("DiffModel") ?? dvType.GetProperty("SideBySideModel");
                if (modelProp != null && modelProp.PropertyType.IsAssignableFrom(typeof(DiffPlex.DiffBuilder.Model.SideBySideDiffModel)))
                {
                    modelProp.SetValue(dv, model);
                }
                else
                {
                    var dcProp = dvType.GetProperty("DataContext");
                    if (dcProp != null) dcProp.SetValue(dv, model);
                }
            }
            catch { }
        }
    }
}
