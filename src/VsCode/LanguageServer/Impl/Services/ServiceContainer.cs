﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.Common.Core;
using Microsoft.Common.Core.Imaging;
using Microsoft.Common.Core.Logging;
using Microsoft.Common.Core.Services;
using Microsoft.Common.Core.Shell;
using Microsoft.Common.Core.Tasks;
using Microsoft.Common.Core.Threading;
using Microsoft.R.Components.InteractiveWorkflow;
using Microsoft.R.Components.Settings;
using Microsoft.R.Editor;
using Microsoft.R.LanguageServer.InteractiveWorkflow;

namespace Microsoft.R.LanguageServer.Services {
    internal sealed class ServiceContainer : IServiceContainer {
        private readonly ServiceManager _services = new ServiceManager();

        public ServiceContainer() {
            _services.AddService<IActionLog>(s => new Logger("VSCode-R", Path.GetTempPath(), s))
                .AddService<IMainThread, MainThread>()
                .AddService<ISettingsStorage, SettingsStorage>()
                .AddService<IRSettings, RSettings>()
                .AddService<ITaskService, TaskService>()
                .AddService<IImageService, ImageService>()
                .AddService(new Application())
                .AddService<IRInteractiveWorkflowProvider, RInteractiveWorkflowProvider>()
                .AddService<ICoreShell, CoreShell>()
                .AddService<IREditorSettings, REditorSettings>()
                .AddService<IIdleTimeService, IdleTimeService>()
                .AddEditorServices();

            AddPlatformSpecificServices();
        }

        public T GetService<T>(Type type = null) where T : class => _services.GetService<T>(type);
        public IEnumerable<Type> AllServices => _services.AllServices;
        public IEnumerable<T> GetServices<T>() where T : class => _services.GetServices<T>();

        private void AddPlatformSpecificServices() {
            var thisAssembly = Assembly.GetEntryAssembly().GetAssemblyPath();
            var assemblyLoc = Path.GetDirectoryName(thisAssembly);
            var platformServicesAssemblyPath = Path.Combine(assemblyLoc, GetPlatformServiceProviderAssemblyName());
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(platformServicesAssemblyPath);

            var classType = assembly.GetType("ServiceProvider");
            var mi = classType.GetMethod("ProvideServices", BindingFlags.Static | BindingFlags.Public);
            mi.Invoke(null, new object[] { _services });
        }

        private static string GetPlatformServiceProviderAssemblyName() {
            var suffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".Windows.dll" : ".Linux.dll";
            return "Microsoft.R.Platform" + suffix;
        }
    }
}