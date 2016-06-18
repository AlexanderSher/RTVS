﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Common.Core;
using Microsoft.Common.Core.IO;
using Microsoft.Common.Core.OS;
using Microsoft.Common.Core.Shell;
using Microsoft.Languages.Editor.Controller.Command;
using Microsoft.Languages.Editor.Extensions;
using Microsoft.Markdown.Editor.Commands;
using Microsoft.Markdown.Editor.ContentTypes;
using Microsoft.Markdown.Editor.Document;
using Microsoft.Markdown.Editor.Flavor;
using Microsoft.R.Components.Controller;
using Microsoft.R.Components.Extensions;
using Microsoft.R.Components.InteractiveWorkflow;
using Microsoft.R.Components.Script;
using Microsoft.R.Host.Client;
using Microsoft.R.Support.Settings;
using Microsoft.VisualStudio.R.Package.Publishing.Definitions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.R.Package.Publishing.Commands {
    internal abstract class PreviewCommand : ViewCommand {
        private Task _lastCommandTask;
        private string _outputFilePath;
        private readonly Dictionary<MarkdownFlavor, IMarkdownFlavorPublishHandler> _flavorHandlers = new Dictionary<MarkdownFlavor, IMarkdownFlavorPublishHandler>();
        protected readonly IRInteractiveWorkflowProvider _workflowProvider;
        private readonly IProcessServices _pss;
        private readonly IFileSystem _fs;
        private readonly ICoreShell _coreShell;

        public PreviewCommand(ITextView textView, int id, 
            IRInteractiveWorkflowProvider workflowProvider, 
            ICoreShell coreShell, 
            IProcessServices pss, 
            IFileSystem fs)
            : base(textView, new CommandId[] { new CommandId(MdPackageCommandId.MdCmdSetGuid, id) }, false) {
            _workflowProvider = workflowProvider;
            _coreShell = coreShell;
            _pss = pss;
            _fs = fs;

            IEnumerable<Lazy<IMarkdownFlavorPublishHandler>> handlers = _coreShell.ExportProvider.GetExports<IMarkdownFlavorPublishHandler>();
            foreach (var h in handlers) {
                _flavorHandlers[h.Value.Flavor] = h.Value;
            }
        }

        protected abstract string FileExtension { get; }

        protected abstract PublishFormat Format { get; }

        public override CommandStatus Status(Guid group, int id) {
            if (!IsFormatSupported()) {
                return CommandStatus.Invisible;
            }

            if (_lastCommandTask == null || _lastCommandTask.IsCompleted) {
                return CommandStatus.SupportedAndEnabled;
            }
            return CommandStatus.Supported;
        }

        public override CommandResult Invoke(Guid group, int id, object inputArg, ref object outputArg) {
            if (!TaskAvailable()) {
                return CommandResult.Disabled;
            }

            IMarkdownFlavorPublishHandler flavorHandler = GetFlavorHandler(TextView.TextBuffer);
            if (flavorHandler != null) {
                if (!InstallPackages.IsInstalled(flavorHandler.RequiredPackageName, 5000, RToolsSettings.Current.RBasePath)) {
                    _coreShell.ShowErrorMessage(string.Format(CultureInfo.InvariantCulture, Resources.Error_PackageMissing, flavorHandler.RequiredPackageName));
                    return CommandResult.Disabled;
                }

                if(!CheckPrerequisites()) {
                    return CommandResult.Disabled;
                }

                // Save the file
                var document = EditorExtensions.FindInProjectedBuffers<MdEditorDocument>(TextView.TextBuffer, MdContentTypeDefinition.ContentType);
                var tb = document.TextBuffer;
                if (!tb.CanBeSavedInCurrentEncoding()) {
                    if (MessageButtons.No == _coreShell.ShowMessage(Resources.Warning_SaveInUtf8, MessageButtons.YesNo)) {
                        return CommandResult.Executed;
                    }
                    tb.Save(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                } else {
                    tb.Save();
                }

               var inputFilePath = tb.GetFilePath();
                _outputFilePath = Path.ChangeExtension(inputFilePath, FileExtension);

                try {
                    _fs.DeleteFile(_outputFilePath);
                } catch (IOException ex) {
                    _coreShell.ShowErrorMessage(ex.Message);
                    return CommandResult.Executed;
                }

                inputFilePath = inputFilePath.Replace('\\', '/');
                string outputFilePath = _outputFilePath.Replace('\\', '/');

                string arguments = flavorHandler.GetCommandLine(inputFilePath, outputFilePath, Format, tb.GetEncoding());
                var session = _workflowProvider.GetOrCreate().RSession;
                _lastCommandTask = session.ExecuteAsync(arguments).ContinueWith(t => LaunchViewer());
            }
            return CommandResult.Executed;
        }

        protected virtual bool CheckPrerequisites() {
            if (!IOExtensions.ExistsOnPath("pandoc.exe")) {
                _coreShell.ShowErrorMessage(Resources.Error_PandocMissing);
                _pss.Start("http://pandoc.org/installing.html");
                return false;
            }
            return true;
        }

        private void LaunchViewer() {
            if (!string.IsNullOrEmpty(_outputFilePath)) {
                if (_fs.FileExists(_outputFilePath)) {
                    LaunchViewer(_outputFilePath);
                }
            }
            _lastCommandTask = null;
        }

        protected virtual void LaunchViewer(string fileName) {
            _pss.Start(_outputFilePath);
        }

        private bool TaskAvailable() {
            if (_lastCommandTask == null) {
                return true;
            }

            switch (_lastCommandTask.Status) {
                case TaskStatus.Canceled:
                case TaskStatus.Faulted:
                case TaskStatus.RanToCompletion:
                    return true;
            }

            return false;
        }

        private IMarkdownFlavorPublishHandler GetFlavorHandler(ITextBuffer textBuffer) {
            MarkdownFlavor flavor = MdFlavor.FromTextBuffer(textBuffer);
            IMarkdownFlavorPublishHandler value = null;

            if (_flavorHandlers.TryGetValue(flavor, out value)) {
                return value;
            }

            return null; // new MdPublishHandler();
        }

        private bool IsFormatSupported() {
            IMarkdownFlavorPublishHandler flavorHandler = GetFlavorHandler(TextView.TextBuffer);
            if (flavorHandler != null) {
                if (flavorHandler.FormatSupported(Format)) {
                    return true;
                }
            }
            return false;
        }
    }
}
