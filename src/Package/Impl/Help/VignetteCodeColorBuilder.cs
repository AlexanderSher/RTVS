﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.Composition;
using System.Text;
using Microsoft.R.Components.InteractiveWorkflow;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using static System.FormattableString;

namespace Microsoft.VisualStudio.R.Package.Help {
    [Export(typeof(IVignetteCodeColorBuilder))]
    internal sealed class VignetteCodeColorBuilder: IVignetteCodeColorBuilder {
        private class CssCodeProperty {
            public string CssClassName;
            public string ClassificationTypeName;

            public CssCodeProperty(string cssClassName, string ctName) {
                CssClassName = cssClassName;
                ClassificationTypeName = ctName;
            }
        }

        private static readonly CssCodeProperty[] _cssPropertyMap = new CssCodeProperty[] {
            new CssCodeProperty("kw", PredefinedClassificationTypeNames.Keyword),
            new CssCodeProperty("fl", PredefinedClassificationTypeNames.Number),
            new CssCodeProperty("st", PredefinedClassificationTypeNames.String),
            new CssCodeProperty("vs", PredefinedClassificationTypeNames.Literal),
            new CssCodeProperty("ss", PredefinedClassificationTypeNames.String),
            new CssCodeProperty("co", PredefinedClassificationTypeNames.Comment),
            new CssCodeProperty("fu", PredefinedClassificationTypeNames.Keyword),
            new CssCodeProperty("va", PredefinedClassificationTypeNames.Identifier),
            new CssCodeProperty("ot", PredefinedClassificationTypeNames.Other),
            new CssCodeProperty("cn", PredefinedClassificationTypeNames.Number),
            new CssCodeProperty("cf", PredefinedClassificationTypeNames.Keyword),
            new CssCodeProperty("op", PredefinedClassificationTypeNames.Operator),
            new CssCodeProperty("pp", PredefinedClassificationTypeNames.PreprocessorKeyword),
        };

        private readonly IRInteractiveWorkflowProvider _workflowProvider;
        private readonly IClassificationFormatMapService _formatMapService;
        private readonly IClassificationTypeRegistryService _classificationRegistryService;

        [ImportingConstructor]
        public VignetteCodeColorBuilder(
            IRInteractiveWorkflowProvider workflowProvider,
            IClassificationFormatMapService cfms, 
            IClassificationTypeRegistryService ctrs) {
            _workflowProvider = workflowProvider;
            _formatMapService = cfms;
            _classificationRegistryService = ctrs;
        }

        public string GetCodeColorsCss() {
            var sb = new StringBuilder();
            var workflow = _workflowProvider.GetOrCreate();
            if (workflow.ActiveWindow?.TextView != null) {
                var map = _formatMapService.GetClassificationFormatMap(workflow.ActiveWindow.TextView);
                foreach (var cssCodeProp in _cssPropertyMap) {
                    var props = map.GetTextProperties(_classificationRegistryService.GetClassificationType(cssCodeProp.ClassificationTypeName));

                    sb.AppendLine(Invariant($"code > span.{cssCodeProp.CssClassName} {{"));
                    sb.AppendLine(Invariant($"color: {CssColorFromBrush(props.ForegroundBrush)};"));
                    sb.AppendLine("}");
                    sb.AppendLine(string.Empty);
                }
            }
            return sb.ToString();
        }

        private string CssColorFromBrush(System.Windows.Media.Brush brush) {
            var sb = brush as System.Windows.Media.SolidColorBrush;
            return Invariant($"rgb({sb.Color.R}, {sb.Color.G}, {sb.Color.B})");
        }
    }
}
