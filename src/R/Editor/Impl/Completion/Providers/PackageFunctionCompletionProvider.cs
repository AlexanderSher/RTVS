﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using Microsoft.Common.Core.Shell;
using Microsoft.Languages.Editor.Imaging;
using Microsoft.R.Core.AST;
using Microsoft.R.Core.Tokens;
using Microsoft.R.Editor.Completion.Definitions;
using Microsoft.R.Editor.Snippets;
using Microsoft.R.Support.Help;
using Microsoft.R.Support.Help.Packages;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.R.Editor.Completion.Providers {
    /// <summary>
    /// Provides list of functions from installed packages
    /// </summary>
    [Export(typeof(IRCompletionListProvider))]
    [Export(typeof(IRHelpSearchTermProvider))]
    public class PackageFunctionCompletionProvider : IRCompletionListProvider, IRHelpSearchTermProvider {
        private const int _asyncWaitTimeout = 1000;
        private readonly ILoadedPackagesProvider _loadedPackagesProvider;
        private readonly ISnippetInformationSourceProvider _snippetInformationSource;
        private readonly ICoreShell _shell;
        private readonly IPackageIndex _packageIndex;

        [ImportingConstructor]
        public PackageFunctionCompletionProvider(ILoadedPackagesProvider loadedPackagesProvider, [Import(AllowDefault = true)] ISnippetInformationSourceProvider snippetInformationSource, IPackageIndex packageIndex, ICoreShell shell) {
            _loadedPackagesProvider = loadedPackagesProvider;
            _snippetInformationSource = snippetInformationSource;
            _shell = shell;
            _packageIndex = packageIndex;
        }

        #region IRCompletionListProvider
        public bool AllowSorting { get; } = true;

        public IReadOnlyCollection<RCompletion> GetEntries(RCompletionContext context) {
            List<RCompletion> completions = new List<RCompletion>();
            ImageSource functionGlyph = GlyphService.GetGlyph(StandardGlyphGroup.GlyphGroupMethod, StandardGlyphItem.GlyphItemPublic, _shell);
            ImageSource constantGlyph = GlyphService.GetGlyph(StandardGlyphGroup.GlyphGroupConstant, StandardGlyphItem.GlyphItemPublic, _shell);
            ImageSource snippetGlyph = GlyphService.GetGlyph(StandardGlyphGroup.GlyphCSharpExpansion, StandardGlyphItem.GlyphItemPublic, _shell);
            var infoSource = _snippetInformationSource?.InformationSource;

            // TODO: this is different in the console window where 
            // packages may have been loaded from the command line. 
            // We need an extensibility point here.
            IEnumerable<IPackageInfo> packages = GetPackages(context);

            // Get list of functions in the package
            foreach (IPackageInfo pkg in packages) {
                Debug.Assert(pkg != null);

                IEnumerable<INamedItemInfo> functions = pkg.Functions;
                if (functions != null) {
                    foreach (INamedItemInfo function in functions) {
                        bool isSnippet = false;
                        // Snippets are suppressed if user typed namespace
                        if (!context.IsInNameSpace() && infoSource != null) {
                            isSnippet = infoSource.IsSnippet(function.Name);
                        }
                        if (!isSnippet) {
                            ImageSource glyph = function.ItemType == NamedItemType.Constant ? constantGlyph : functionGlyph;
                            var completion = new RCompletion(function.Name, CompletionUtilities.BacktickName(function.Name), function.Description, glyph);
                            completions.Add(completion);
                        }
                    }
                }
            }

            return completions;
        }
        #endregion

        #region IRHelpSearchTermProvider
        public IReadOnlyCollection<string> GetEntries() {
            var list = new List<string>();
            foreach (IPackageInfo pkg in _packageIndex.Packages) {
                list.AddRange(pkg.Functions.Select(x => x.Name));
            }
            return list;
        }
        #endregion

        private IEnumerable<IPackageInfo> GetPackages(RCompletionContext context) {
            if (context.IsInNameSpace()) {
                return GetSpecificPackage(context);
            }

            return GetAllFilePackages(context);
        }

        /// <summary>
        /// Retrieves name of the package in 'package::' statement
        /// so intellisense can show list of functions available
        /// in the specific package.
        /// </summary>
        private IEnumerable<IPackageInfo> GetSpecificPackage(RCompletionContext context) {
            List<IPackageInfo> packages = new List<IPackageInfo>();
            ITextSnapshot snapshot = context.TextBuffer.CurrentSnapshot;
            int colons = 0;

            for (int i = context.Position - 1; i >= 0; i--, colons++) {
                char ch = snapshot[i];
                if (ch != ':') {
                    break;
                }
            }

            if (colons > 1 && colons < 4) {
                string packageName = string.Empty;
                int start = 0;
                int end = context.Position - colons;

                for (int i = end - 1; i >= 0; i--) {
                    char ch = snapshot[i];
                    if (!RTokenizer.IsIdentifierCharacter(ch)) {
                        start = i + 1;
                        break;
                    }
                }

                if (start < end) {
                    packageName = snapshot.GetText(Span.FromBounds(start, end));
                    if (packageName.Length > 0) {
                        context.InternalFunctions = colons == 3;
                        var package = GetPackageByName(packageName);
                        if (package != null) {
                            packages.Add(package);
                        }
                    }
                }
            }

            return packages;
        }

        /// <summary>
        /// Retrieves list of packages declared in the file via 'library' statements
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private IEnumerable<IPackageInfo> GetAllFilePackages(RCompletionContext context) {
            _loadedPackagesProvider?.Initialize();

            IEnumerable<string> loadedPackages = _loadedPackagesProvider?.GetPackageNames() ?? Enumerable.Empty<string>();
            IEnumerable<string> filePackageNames = context.AstRoot.GetFilePackageNames();
            IEnumerable<string> allPackageNames = PackageIndex.PreloadedPackages.Union(filePackageNames).Union(loadedPackages);

            return allPackageNames
                .Select(packageName => GetPackageByName(packageName))
                // May be null if user mistyped package name in the library()
                // statement or package is not installed.
                .Where(p => p != null)
                .ToList();
        }

        private IPackageInfo GetPackageByName(string packageName) {
            var t = _packageIndex.GetPackageInfoAsync(packageName);
            t.Wait(_asyncWaitTimeout);
            return t.IsCompleted ? t.Result : null;
        }
    }
}
