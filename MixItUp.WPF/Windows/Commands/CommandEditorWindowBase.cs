using MixItUp.Base;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Commands;
using MixItUp.Base.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace MixItUp.WPF.Windows.Commands
{
    public class CommandEditorWindowBase : LoadingWindowBase
    {
        protected bool AllowClose { get; set; }
        private bool checkingClose;
        private bool dirty;
        private bool ready;
        private string initialSnapshot;
        private readonly HashSet<object> observed = new HashSet<object>(ReferenceEqualityComparer.Instance);
        private readonly List<Action> unsubscribe = new List<Action>();

        protected override async Task OnLoaded()
        {
            await base.OnLoaded();
            this.initialSnapshot = await this.TrySnapshot();
            this.Observe(this.ViewModel);
            this.ready = true;
        }

        protected override bool CanClose() => this.AllowClose || !this.ready;

        protected override async Task OnCloseBlocked()
        {
            if (this.checkingClose) { return; }
            this.checkingClose = true;
            try
            {
                // Text bindings can still hold an edit when the window closes before focus moves.
                if (Keyboard.FocusedElement is TextBox textBox)
                {
                    textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    if (textBox.TemplatedParent is ComboBox comboBox)
                    {
                        comboBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                    }
                }
                await Task.Yield();
                string current = await this.TrySnapshot();
                bool changed = this.initialSnapshot != null ? current != this.initialSnapshot : this.dirty;
                if (!changed || await DialogHelper.ShowConfirmation(MixItUp.Base.Resources.DiscardUnsavedCommandChanges))
                {
                    this.AllowClose = true;
                    this.Close();
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            finally { this.checkingClose = false; }
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (Action action in this.unsubscribe) { action(); }
            this.unsubscribe.Clear();
            this.observed.Clear();
            base.OnClosed(e);
        }

        private async Task<string> TrySnapshot()
        {
            try { return await ((CommandEditorWindowViewModelBase)this.ViewModel).GetEditorSnapshot(); }
            catch { return null; }
        }

        private void Observe(object value)
        {
            if (value == null || value is string || !this.observed.Add(value)) { return; }
            if (value is INotifyCollectionChanged collection)
            {
                NotifyCollectionChangedEventHandler handler = (sender, args) =>
                {
                    this.dirty = true;
                    if (args.NewItems != null) { foreach (object item in args.NewItems) { this.Observe(item); } }
                };
                collection.CollectionChanged += handler;
                this.unsubscribe.Add(() => collection.CollectionChanged -= handler);
            }
            if (value is IEnumerable items)
            {
                foreach (object item in items) { if (item is UIViewModelBase) { this.Observe(item); } }
            }
            if (value is UIViewModelBase model)
            {
                PropertyChangedEventHandler handler = (sender, args) =>
                {
                    if (args.PropertyName == "IsLoading" || args.PropertyName == "IsMinimized" || args.PropertyName == "WindowTitle") { return; }
                    var property = model.GetType().GetProperty(args.PropertyName ?? string.Empty);
                    if (property?.SetMethod?.IsPublic == true) { this.dirty = true; }
                    if (property != null && typeof(UIViewModelBase).IsAssignableFrom(property.PropertyType)) { this.Observe(property.GetValue(model)); }
                };
                model.PropertyChanged += handler;
                this.unsubscribe.Add(() => model.PropertyChanged -= handler);
                foreach (var property in model.GetType().GetProperties().Where(p => p.GetIndexParameters().Length == 0 &&
                    (typeof(UIViewModelBase).IsAssignableFrom(p.PropertyType) || typeof(INotifyCollectionChanged).IsAssignableFrom(p.PropertyType))))
                {
                    this.Observe(property.GetValue(model));
                }
            }
        }
    }
}
