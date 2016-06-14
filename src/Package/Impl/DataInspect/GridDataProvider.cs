﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.R.Package.DataInspect.DataSource;
using static System.FormattableString;

namespace Microsoft.VisualStudio.R.Package.DataInspect {
    /// <summary>
    /// grid data provider to control
    /// </summary>
    internal sealed class GridDataProvider : IGridProvider<string> {
        private readonly VariableViewModel _evaluation;

        public GridDataProvider(VariableViewModel evaluation) {
            _evaluation = evaluation;

            RowCount = evaluation.Dimensions[0];
            ColumnCount = evaluation.Dimensions.Count >= 2 ? evaluation.Dimensions[1] : 1;
        }

        public int ColumnCount { get; }

        public int RowCount { get; }

        public Task<IGridData<string>> GetAsync(GridRange gridRange, ISortOrder sortOrder = null) {
            var t = GridDataSource.GetGridDataAsync(_evaluation.Expression, gridRange, sortOrder);
            if (t == null) {
                // May happen when R host is not running
                Trace.Fail(Invariant($"{nameof(VariableViewModel)} returned null grid data"));
                return Task.FromResult<IGridData<string>>(null);
            }
            return t;
        }
    }
}
