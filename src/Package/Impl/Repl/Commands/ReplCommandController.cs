﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Languages.Editor.Controller;
using Microsoft.Languages.Editor.Services;
using Microsoft.R.Components.Controller;
using Microsoft.R.Components.InteractiveWorkflow;
using Microsoft.R.Core.AST.Statements.Definitions;
using Microsoft.R.Editor.Commands;
using Microsoft.R.Editor.Completion;
using Microsoft.R.Editor.Document;
using Microsoft.R.Editor.Formatting;
using Microsoft.R.Editor.Settings;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.R.Package.Commands;
using Microsoft.VisualStudio.R.Package.Expansions;
using Microsoft.VisualStudio.R.Package.Shell;
using Microsoft.VisualStudio.R.Packages.R;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.R.Package.Repl.Commands {
    /// <summary>
    /// Main interactive window command controller
    /// </summary>
    public class ReplCommandController : ViewController {
        private SnippetController _snippetController;

        public ReplCommandController(ITextView textView, ITextBuffer textBuffer)
            : base(textView, textBuffer) {
            ServiceManager.AddService(this, textView);

            // TODO: make this extensible via MEF like commands and controllers in the editor
            _snippetController = new SnippetController(textView, textBuffer);
        }

        public static ReplCommandController Attach(ITextView textView, ITextBuffer textBuffer) {
            ReplCommandController controller = FromTextView(textView);
            if (controller == null) {
                controller = new ReplCommandController(textView, textBuffer);
            }

            return controller;
        }

        public static ReplCommandController FromTextView(ITextView textView) {
            return ServiceManager.GetService<ReplCommandController>(textView);
        }

        public override void BuildCommandSet() {
            if (VsAppShell.Current.CompositionService != null) {
                var factory = new ReplCommandFactory();
                var commands = factory.GetCommands(TextView, TextBuffer);
                AddCommandSet(commands);
            }
        }

        public override CommandStatus Status(Guid group, int id) {
            if ((NonRoutedStatus(group, id, null) & CommandStatus.SupportedAndEnabled) == CommandStatus.SupportedAndEnabled
                && !IsCompletionCommand(group, id)) {
                return CommandStatus.SupportedAndEnabled;
            }

            var status = _snippetController.Status(group, id);
            if(status != CommandStatus.NotSupported) {
                return status;
            }

            return base.Status(group, id);
        }

        public override CommandResult Invoke(Guid group, int id, object inputArg, ref object outputArg) {
            if (group == VSConstants.VSStd2K) {
                RCompletionController controller = RCompletionController.FromTextView(TextView);
                if (controller != null) {
                    if (id == (int)VSConstants.VSStd2KCmdID.RETURN) {
                        return HandleEnter(controller);
                    } else if (id == (int)VSConstants.VSStd2KCmdID.CANCEL) {
                        HandleCancel(controller);
                        // Allow VS to continue processing cancel
                    }
                }
            } else if (group == VSConstants.GUID_VSStandardCommandSet97) {
                if (id == (int)VSConstants.VSStd97CmdID.F1Help) {
                    RCompletionController controller = RCompletionController.FromTextView(TextView);
                    if (controller != null) {
                        // Translate to R help
                        HandleF1Help(controller);
                        return CommandResult.Executed;
                    }
                }
            }

            var status = _snippetController.Status(group, id);
            if (status != CommandStatus.NotSupported) {
                return _snippetController.Invoke(group, id, inputArg, ref outputArg);
            }

            return base.Invoke(group, id, inputArg, ref outputArg);
        }

        private CommandResult HandleEnter(RCompletionController controller) {
            // If completion is up, commit it
            if (controller.HasActiveCompletionSession) {
                // Check for exact match. If applicable span is 'x' and completion is 'x'
                // then we don't complete and rather execute. If span is 'x' while
                // current completion entry is 'X11' then we complete depending on
                // the 'complete on enter' setting.
                try {
                    ICompletionSession session = controller.CompletionSession;
                    CompletionSet set = session.SelectedCompletionSet;
                    ITrackingSpan span = set.ApplicableTo;
                    ITextSnapshot snapshot = span.TextBuffer.CurrentSnapshot;
                    string spanText = snapshot.GetText(span.GetSpan(snapshot));
                    if (spanText != set.SelectionStatus.Completion.InsertionText) {
                        // If selection is does not match typed text,
                        // control completion depending on the editor setting.
                        if (set.SelectionStatus.IsSelected && REditorSettings.CommitOnEnter) {
                            controller.CommitCompletionSession();
                            controller.DismissAllSessions();
                            return CommandResult.Executed;
                        }
                    }
                } catch (Exception) { }
            }

            controller.DismissAllSessions();
            ICompletionBroker broker = VsAppShell.Current.ExportProvider.GetExportedValue<ICompletionBroker>();
            broker.DismissAllSessions(TextView);

            var interactiveWorkflowProvider = VsAppShell.Current.ExportProvider.GetExportedValue<IRInteractiveWorkflowProvider>();
            interactiveWorkflowProvider.GetOrCreate().Operations.ExecuteCurrentExpression(TextView, FormatReplDocument);
            return CommandResult.Executed;
        }

        private static void FormatReplDocument(ITextView textView, ITextBuffer textBuffer, int position) {
            var document = REditorDocument.TryFromTextBuffer(textBuffer);
            if (document != null) {
                var tree = document.EditorTree;
                tree.EnsureTreeReady();
                FormatOperations.FormatNode<IStatement>(textView, textBuffer, position);
            }
        }

        private void HandleCancel(RCompletionController controller) {
            IVsUIShell uiShell = VsAppShell.Current.GetGlobalService<IVsUIShell>(typeof(SVsUIShell));
            Guid gmdSet = RGuidList.RCmdSetGuid;
            object o = new object();
            // Post interrupt command which knows if it can interrupt R or not
            uiShell.PostExecCommand(ref gmdSet, RPackageCommandId.icmdInterruptR, 0, ref o);
        }

        private void HandleF1Help(RCompletionController controller) {
            IVsUIShell uiShell = VsAppShell.Current.GetGlobalService<IVsUIShell>(typeof(SVsUIShell));
            Guid gmdSet = RGuidList.RCmdSetGuid;
            object o = new object();
            // Post interrupt command which knows if it can interrupt R or not
            uiShell.PostExecCommand(ref gmdSet, RPackageCommandId.icmdHelpOnCurrent, 0, ref o);
        }

        /// <summary>
        /// Determines if command is one of the completion commands
        /// </summary>
        private bool IsCompletionCommand(Guid group, int id) {
            ICommand cmd = Find(group, id);
            return cmd is RCompletionCommandHandler;
        }

        /// <summary>
        /// Disposes main controller and removes it from service manager.
        /// </summary>
        protected override void Dispose(bool disposing) {
            if (TextView != null) {
                ServiceManager.RemoveService<ReplCommandController>(TextView);
            }

            base.Dispose(disposing);
        }
    }
}