﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.R.Support.Help {
    public interface IPackageIndex: IDisposable {
        /// <summary>
        /// Creates index of packages available in the provided R session
        /// </summary>
        Task BuildIndexAsync();

        /// <summary>
        /// Writes index cache to disk
        /// </summary>
        void WriteToDisk();

        /// <summary>
        /// Returns collection of packages in the current 
        /// </summary>
        IEnumerable<IPackageInfo> Packages { get; }

        /// <summary>
        /// Retrieves R package information by name. If package is not in the index,
        /// attempts to locate the package in the current R session.
        /// </summary>
        Task<IPackageInfo> GetPackageInfoAsync(string packageName);

        /// <summary>
        /// Retrieves information on the package from index. Does not attempt to locate the package
        /// if it is not in the index such as when package was just installed.
        /// </summary>
         IPackageInfo GetPackageInfo(string packageName);
    }
}