// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Microsoft.Common.Core;
using Microsoft.Common.Core.Shell;
using Microsoft.Common.Wpf.Media;
using Microsoft.R.Components.Controller;
using Microsoft.R.Components.Help;
using Microsoft.R.Components.Settings;
using Microsoft.R.Components.View;
using Microsoft.R.Host.Client;
using Microsoft.R.Components.InteractiveWorkflow;
using Microsoft.R.Wpf;
using Microsoft.VisualStudio.R.Package.Browsers;
using ContentControl = System.Windows.Controls.ContentControl;

namespace Microsoft.VisualStudio.R.Package.Help {
    internal sealed class HelpVisualComponent : IHelpVisualComponent {
        /// <summary>
        /// Holds browser control. When R session is restarted
        /// it is necessary to re-create the browser control since
        /// help server changes port and current links stop working.
        /// However, VS tool window doesn't like its child root
        /// control changing so instead we keed content control 
        /// unchanged and only replace browser that is inside it.
        /// </summary>
        private readonly ContentControl _windowContentControl;
        private readonly IVignetteCodeColorBuilder _codeColorBuilder;
        private readonly IRSettings _settings;
        private readonly IRInteractiveWorkflow _workflow;
        private readonly ICoreShell _shell;
        private IRSession _session;
        private WindowsFormsHost _host;

        public HelpVisualComponent(IVignetteCodeColorBuilder codeColorBuilder, IRInteractiveWorkflowProvider workflowProvider, IRSettings settings) {
            _codeColorBuilder = codeColorBuilder;
            _settings = settings;
            _workflow = workflowProvider.GetOrCreate();
            _shell = _workflow.Shell;
            _session = _workflow.RSession;
            _session.Disconnected += OnRSessionDisconnected;

            _windowContentControl = new ContentControl();
            Control = _windowContentControl;

            var c = new Controller();
            c.AddCommandSet(GetCommands());
            Controller = c;

            CreateBrowser();
        }

        #region IVisualComponent
        public ICommandTarget Controller { get; }

        public FrameworkElement Control { get; }
        public IVisualComponentContainer<IVisualComponent> Container { get; internal set; }

        #endregion

        #region IHelpWindowVisualComponent
        /// <summary>
        /// Browser that displays help content
        /// </summary>
        public WebBrowser Browser { get; private set; }

        public void Navigate(string url) {
            // Filter out localhost help URL from absolute URLs
            // except when the URL is the main landing page.
            if (_settings.HelpBrowserType == HelpBrowserType.Automatic && IsHelpUrl(url)) {
                NavigateTo(url);
            } else {
                var wbs = _shell.ExportProvider.GetExportedValue<IWebBrowserServices>();
                wbs.OpenBrowser(WebBrowserRole.Shiny, url);
            }
        }

        public string VisualTheme { get; set; }
        #endregion

        private void OnRSessionDisconnected(object sender, EventArgs e) {
            // Event fires on a background thread
            _shell.DispatchOnUIThread(CloseBrowser);
        }

        private void CreateBrowser() {
            if (Browser == null) {
                Browser = new WebBrowser {
                    WebBrowserShortcutsEnabled = true,
                    IsWebBrowserContextMenuEnabled = true
                };


                Browser.Navigating += OnNavigating;
                Browser.Navigated += OnNavigated;

                _host = new WindowsFormsHost();
                _windowContentControl.Content = _host;
            }
        }

        public void SetThemeColors() {
            var doc = Browser?.Document;

            RemoveExistingStyles();
            AttachStandardStyles();
            AttachCodeStyles();

            // The body may become null after styles are modified.
            // this happens if browser decides to re-render document.
            if(doc?.Body == null) {
                SetThemeColorsWhenReady();
            }
        }

        /// <summary>
        /// Attaches theme-specific styles to the help page.
        /// </summary>
        private void AttachStandardStyles() {
            if (Browser?.Document == null) {
                return;
            }

            var doc = Browser?.Document?.DomDocument as IHTMLDocument2;
            if (doc != null) {
                string cssText = GetCssText();
                if (!string.IsNullOrEmpty(cssText)) {
                    IHTMLStyleSheet ss = doc.createStyleSheet();
                    if (ss != null) {
                        ss.cssText = cssText;
                    }
                }
            }
        }

        /// <summary>
        /// Attaches code colorization styles to vignettes.
        /// </summary>
        private void AttachCodeStyles() {
            var doc = Browser?.Document?.DomDocument as IHTMLDocument2;
            if (doc != null) {
                var ss = doc.createStyleSheet();
                if (ss != null) {
                    ss.cssText = _codeColorBuilder.GetCodeColorsCss();
                }
            }
        }

        /// <summary>
        /// Removes existing styles from the help page or vignette.
        /// </summary>
        private void RemoveExistingStyles() {
            var doc = Browser?.Document?.DomDocument as dynamic;
            if (doc != null) {
                if (doc.styleSheets.length > 0) {
                    // Remove stylesheets
                    var styleSheets = new List<dynamic>();
                    foreach (IHTMLStyleSheet s in doc.styleSheets) {
                        s.disabled = true;
                    }
                }
                // Remove style blocks
                foreach (var node in doc.head.childNodes) {
                    if (node is IHTMLStyleElement) {
                        doc.head.removeChild(node);
                    }
                }
            }
        }

        /// <summary>
        /// Fetches theme-specific stylesheet from disk
        /// </summary>
        private string GetCssText() {
            string cssfileName = null;

            if (VisualTheme != null) {
                cssfileName = VisualTheme;
            } else {
                var defaultBackground = Colors.ToolWindowBackgroundColor;
                // TODO: We can generate CSS from specific VS colors. For now, just do Dark and Light.
                cssfileName = defaultBackground.GetBrightness() < 0.5 ? "Dark.css" : "Light.css";
            }

            if (!string.IsNullOrEmpty(cssfileName)) {
                string assemblyPath = Assembly.GetExecutingAssembly().GetAssemblyPath();
                string themePath = Path.Combine(Path.GetDirectoryName(assemblyPath), @"Help\Themes\", cssfileName);

                try {
                    using (var sr = new StreamReader(themePath)) {
                        return sr.ReadToEnd();
                    }
                } catch (IOException) {
                    Trace.Fail("Unable to load theme stylesheet {0}", cssfileName);
                }
            }
            return string.Empty;
        }

        private void OnNavigating(object sender, WebBrowserNavigatingEventArgs e) {
            // Disconnect browser from the tool window so it does not
            // flicker when we change page and element styling.
            _host.Child = null;

            if (Browser?.Document?.Window != null) {
                Browser.Document.Window.Unload -= OnWindowUnload;
            }

            string url = e.Url.ToString();
            if (!IsHelpUrl(url)) {
                e.Cancel = true;
                var wbs = _shell.ExportProvider.GetExportedValue<IWebBrowserServices>();
                wbs.OpenBrowser(WebBrowserRole.External, url);
            }
        }

        private void OnNavigated(object sender, WebBrowserNavigatedEventArgs e) {
            // Page may be loaded, but body may still be null of scripts
            // are running. For example, in 3.2.2 code colorization script
            // tends to damage body content so browser may have to to re-create it.
            SetThemeColorsWhenReady();
 
            // Upon navigation we need to ask VS to update UI so 
            // Back/Forward buttons become properly enabled or disabled.
            Container.UpdateCommandStatus(false);
        }

        private void OnWindowUnload(object sender, HtmlElementEventArgs e) {
            // Refresh button clicked. Current document state is 'complete'.
            // We need to delay until it changes to 'loading' and then
            // delay again until it changes again to 'complete'.
            Browser.Document.Window.Unload -= OnWindowUnload;
            // Disconnect browser from the tool window so it does not
            // flicker when we change page and element styling.
            _host.Child = null;
        }

        private void SetThemeColorsWhenReady() {
            var doc = Browser.Document.DomDocument as IHTMLDocument2;
            if (Browser.ReadyState == WebBrowserReadyState.Complete && doc.body != null) {
                SetThemeColors();
                Browser.Document.Window.Unload += OnWindowUnload;
                // Reconnect browser control to the window
                _host.Child = Browser;
            } else {
                // The browser document is not ready yet. Create another idle 
                // time action that will run after few milliseconds.
                IdleTimeAction.Create(() => SetThemeColorsWhenReady(), 10, new object());
            }
        }

        private void NavigateTo(string url) {
            if (Browser == null) {
                CreateBrowser();
            }

            Browser.Navigate(url);
        }

        private static bool IsHelpUrl(string url) {
            Uri uri = new Uri(url);
            // dynamicHelp.R (startDynamicHelp function):
            // # Choose 10 random port numbers between 10000 and 32000
            // ports <- 10000 + 22000*((stats::runif(10) + unclass(Sys.time())/300) %% 1)
            return uri.IsLoopback && uri.Port >= 10000 && uri.Port <= 32000 && !string.IsNullOrEmpty(uri.PathAndQuery);
        }

        private IEnumerable<IAsyncCommand> GetCommands() {
            return new List<IAsyncCommand> {
                new HelpPreviousCommand(this),
                new HelpNextCommand(this),
                new HelpHomeCommand(this),
                new HelpRefreshCommand(this)
            };
        }

        public void Dispose() {
            DisconnectFromSessionEvents();
            CloseBrowser();
        }

        private void DisconnectFromSessionEvents() {
            if (_session != null) {
                _session.Disconnected -= OnRSessionDisconnected;
                _session = null;
            }
        }

        private void CloseBrowser() {
            _windowContentControl.Content = null;

            if (Browser != null) {
                if (Browser.Document != null && Browser.Document.Window != null) {
                    Browser.Document.Window.Unload += OnWindowUnload;
                }
                Browser.Navigating -= OnNavigating;
                Browser.Navigated -= OnNavigated;
                Browser.Dispose();
                Browser = null;
            }
        }
    }
}
