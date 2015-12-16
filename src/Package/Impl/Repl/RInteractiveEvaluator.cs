using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Common.Core;
using Microsoft.R.Core.Parser;
using Microsoft.R.Host.Client;
using Microsoft.R.Support.Settings;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.R.Package.History;
using Microsoft.VisualStudio.R.Package.Plots;
using Microsoft.VisualStudio.R.Package.Shell;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.R.Package.Repl {
    internal sealed class RInteractiveEvaluator : IInteractiveEvaluator {
        private static bool useReparentPlot = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RTVS_USE_NEW_GFX"));

        private readonly IntPtr _plotWindowHandle;

        public IRHistory History { get; }
        public IRSession Session { get; }

        public RInteractiveEvaluator(IRSession session, IRHistory history) {
            History = history;
            Session = session;
            Session.Output += SessionOnOutput;
            Session.Disconnected += SessionOnDisconnected;

            if (useReparentPlot) {
                // Cache handle here since it must be done on UI thread
                _plotWindowHandle = RPlotWindowHost.RPlotWindowContainerHandle;
            } else {
                _plotWindowHandle = IntPtr.Zero;
            }
        }

        public void Dispose() {
            Session.Output -= SessionOnOutput;
            Session.Disconnected -= SessionOnDisconnected;
        }

        public async Task<ExecutionResult> InitializeAsync() {
            try {
                await Session.StartHostAsync(_plotWindowHandle);
                return ExecutionResult.Success;
            } catch (RHostBinaryMissingException) {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
                VsAppShell.Current.ShowErrorMessage(Resources.Error_Microsoft_R_Host_Missing);
                return ExecutionResult.Failure;
            } catch (Exception) {
                return ExecutionResult.Failure;
            }
        }

        public async Task<ExecutionResult> ResetAsync(bool initialize = true) {
            try {
                if (Session.IsHostRunning) {
                    CurrentWindow.WriteError(Resources.MicrosoftRHostStopping + Environment.NewLine);
                    await Session.StopHostAsync();
                }

                if (!initialize) {
                    return ExecutionResult.Success;
                }

                CurrentWindow.WriteError(Resources.MicrosoftRHostStarting + Environment.NewLine);
                return await InitializeAsync();
            } catch (Exception ex) {
                Trace.Fail($"Exception in RInteractiveEvaluator.ResetAsync\n{ex}");
                return ExecutionResult.Failure;
            }
        }

        public bool CanExecuteCode(string text) {
            if (!text.StartsWith("?", StringComparison.Ordinal)) {
                var ast = RParser.Parse(text);
                if (ast.Errors.Count > 0) {
                    // if we have any errors other than an incomplete statement send the
                    // bad code to R.  Otherwise continue reading input.
                    foreach (var error in ast.Errors) {
                        if (error.ErrorType != ParseErrorType.CloseCurlyBraceExpected &&
                            error.ErrorType != ParseErrorType.CloseBraceExpected &&
                            error.ErrorType != ParseErrorType.CloseSquareBracketExpected &&
                            error.ErrorType != ParseErrorType.FunctionBodyExpected &&
                            error.ErrorType != ParseErrorType.RightOperandExpected) {
                            return true;
                        }
                    }

                    return false;
                }
            }
            return true;
        }

        public async Task<ExecutionResult> ExecuteCodeAsync(string text) {
            var request = await Session.BeginInteractionAsync();

            if (text.Length >= request.MaxLength) {
                CurrentWindow.WriteErrorLine(string.Format(Resources.InputIsTooLong, request.MaxLength));
                request.Dispose();
                return ExecutionResult.Failure;
            }

            if (!CheckConvertableToDefaultCodepage(text)) {
                CurrentWindow.WriteErrorLine(Resources.Error_ReplUnicodeCoversion);
                request.Dispose();
                return ExecutionResult.Failure;
            }

            try {
                await request.RespondAsync(text);
                return ExecutionResult.Success;
            } catch (RException) {
                // It was already reported via RSession.Error and printed out; just return failure.
                return ExecutionResult.Failure;
            } catch (OperationCanceledException) {
                // Cancellation reason was already reported via RSession.Error and printed out; just return failure.
                return ExecutionResult.Failure;
            } catch (Exception ex) {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
                VsAppShell.Current.ShowErrorMessage(ex.ToString());
                return ExecutionResult.Failure;
            } finally {
                History.AddToHistory(text);
            }
        }

        public string FormatClipboard() {
            // keep the clipboard content as is
            return null;
        }

        public void AbortExecution() {
            Session.CancelAllAsync().DoNotWait();
        }

        public string GetPrompt() {
            if (CurrentWindow.CurrentLanguageBuffer.CurrentSnapshot.LineCount > 1) {
                // TODO: We should support dynamically getting the prompt at runtime
                // if the user changes it
                return "+ ";
            }
            return Session.Prompt;
        }

        public IInteractiveWindow CurrentWindow { get; set; }

        private void SessionOnOutput(object sender, ROutputEventArgs args) {
            if (args.OutputType == OutputType.Output) {
                Write(args.Message);
            } else {
                WriteError(args.Message);
            }
        }

        private void SessionOnDisconnected(object sender, EventArgs args) {
            if (CurrentWindow == null || !CurrentWindow.IsResetting) {
                WriteLine(Resources.MicrosoftRHostStopped);
            }
        }

        private void Write(string message) {
            if (CurrentWindow != null) {
                VsAppShell.Current.DispatchOnUIThread(() => CurrentWindow.Write(message));
            }
        }

        private void WriteError(string message) {
            if (CurrentWindow != null) {
                VsAppShell.Current.DispatchOnUIThread(() => CurrentWindow.WriteError(message));
            }
        }

        private void WriteLine(string message) {
            if (CurrentWindow != null) {
                VsAppShell.Current.DispatchOnUIThread(() => CurrentWindow.WriteLine(message));
            }
        }

        /// <summary>
        /// Check if given Unicode text is convertable without loss to the default
        /// OS codepage. R is not Unicode so host process converts incoming UTF-8
        /// to 8-bit characters via Windows CP. If locale for non-Unicode programs 
        /// is set correctly, user can type in their language.
        /// </summary>
        private bool CheckConvertableToDefaultCodepage(string s) {
            // Convert to Windows CP and back and see if the result
            // of the conversion matches original text.
            byte[] srcBytes = Encoding.Unicode.GetBytes(s);
            byte[] dstBytes = Encoding.Convert(Encoding.Unicode, Encoding.GetEncoding(0), srcBytes);
            byte[] resultBytes = Encoding.Convert(Encoding.GetEncoding(0), Encoding.Unicode, dstBytes);

            return srcBytes.SequenceEqual(resultBytes);
        }
    }
}