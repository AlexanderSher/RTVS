// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETSTANDARD1_6
using static System.AttributeTargets;

namespace System.Diagnostics.CodeAnalysis {
    [AttributeUsage(Class | Struct | Constructor | Method | Property | Event, Inherited = false)]
    internal sealed class ExcludeFromCodeCoverageAttribute : Attribute { }
}
#endif
