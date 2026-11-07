using ICSharpCode.AvalonEdit;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Windows;

namespace MixItUp.WPF.Util
{
    public static class AvalonEditBehaviour
    {
        private static readonly HashSet<TextEditor> updatingEditors = new HashSet<TextEditor>();

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached("Text", typeof(string), typeof(AvalonEditBehaviour), new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PropertyChangedCallback));

        private static readonly DependencyProperty TextChangedHookedProperty =
            DependencyProperty.RegisterAttached("TextChangedHooked", typeof(bool), typeof(AvalonEditBehaviour), new PropertyMetadata(false));

        public static string GetText(DependencyObject dp)
        {
            return (string)dp.GetValue(TextProperty);
        }

        public static void SetText(DependencyObject dp, string value)
        {
            dp.SetValue(TextProperty, value);
        }

        private static void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextEditor editor)
            {
                return;
            }

            EnsureTextChangedHook(editor);

            if (editor.Document == null || updatingEditors.Contains(editor))
            {
                return;
            }

            string newText = e.NewValue as string ?? string.Empty;
            if (string.Equals(editor.Document.Text, newText, StringComparison.Ordinal))
            {
                return;
            }

            int caretOffset = editor.CaretOffset;
            editor.Document.Text = newText;
            try
            {
                editor.CaretOffset = Math.Min(caretOffset, editor.Document.TextLength);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Log(ex);
            }
        }

        private static void EnsureTextChangedHook(TextEditor editor)
        {
            if ((bool)editor.GetValue(TextChangedHookedProperty))
            {
                return;
            }

            editor.TextArea.TextView.SetResourceReference(ICSharpCode.AvalonEdit.Rendering.TextView.LinkTextForegroundBrushProperty, "MaterialDesign.Brush.Primary");
            editor.TextChanged += Editor_TextChanged;
            editor.SetValue(TextChangedHookedProperty, true);
        }

        private static void Editor_TextChanged(object sender, EventArgs e)
        {
            if (sender is not TextEditor editor || editor.Document == null)
            {
                return;
            }

            updatingEditors.Add(editor);
            try
            {
                SetText(editor, editor.Document.Text);
            }
            finally
            {
                updatingEditors.Remove(editor);
            }
        }
    }
}
