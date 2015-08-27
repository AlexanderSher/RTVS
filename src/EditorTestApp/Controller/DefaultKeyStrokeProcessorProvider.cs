﻿using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.Languages.Editor.Application.Controller
{
    [Export(typeof(IKeyProcessorProvider))]
    [Name("DefaultKeyProcessor")]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class DefaultKeyProcessorProvider : IKeyProcessorProvider
    {
        [Import]
        private IEditorOperationsFactoryService _editorOperationsProvider { get; set; }

        [Import]
        private ITextUndoHistoryRegistry _undoHistoryRegistry { get; set; }

        /// <summary>
        /// Creates a new key processor provider for the given WPF text view host
        /// </summary>
        /// <param name="wpfTextViewHost">Wpf-based text view host to create key processor for</param>
        /// <returns>A valid key processor</returns>
        public KeyProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            if (wpfTextView == null)
                throw new ArgumentNullException("wpfTextView");

            return new DefaultKeyProcessor(wpfTextView, _editorOperationsProvider.GetEditorOperations(wpfTextView), _undoHistoryRegistry);
        }
    }
}
