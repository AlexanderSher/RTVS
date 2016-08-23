// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Common.Core;
using Microsoft.Common.Wpf;
using Microsoft.R.Components.ConnectionManager.ViewModel;

namespace Microsoft.R.Components.ConnectionManager.Implementation.ViewModel {
    internal sealed class ConnectionViewModel : BindableBase, IConnectionViewModel {
        private readonly IConnection _connection;
        private string _name;
        private string _path;
        private string _rCommandLineArguments;
        private bool _isConnected;
        private bool _isRemote;
        private bool _hasChanges;
        private bool _canConnect;

        public ConnectionViewModel() {}

        public ConnectionViewModel(IConnection connection) {
            _connection = connection;
            Id = _connection.Id;
            Reset();
        }

        public Uri Id { get; }

        public string Name {
            get { return _name; }
            set {
                SetProperty(ref _name, value);
                UpdateCalculated();
            }
        }

        public string Path {
            get { return _path; }
            set {
                SetProperty(ref _path, value);
                UpdateCalculated();
            }
        }

        public string RCommandLineArguments {
            get { return _rCommandLineArguments; }
            set {
                SetProperty(ref _rCommandLineArguments, value);
                UpdateCalculated();
            }
        }

        public bool IsConnected {
            get { return _isConnected; }
            set { SetProperty(ref _isConnected, value); }
        }

        public bool IsRemote {
            get { return _isRemote; }
            set { SetProperty(ref _isRemote, value); }
        }
        
        public bool CanConnect {
            get { return _canConnect; }
            private set { SetProperty(ref _canConnect, value); }
        }

        public bool HasChanges {
            get { return _hasChanges; }
            private set { SetProperty(ref _hasChanges, value); }
        }

        public void Reset() {
            Name = _connection?.Name;
            Path = _connection?.Path;
            RCommandLineArguments = _connection?.RCommandLineArguments;
        }

        private void UpdateCalculated() {
            HasChanges = !Name.EqualsIgnoreCase(_connection?.Name)
                || !Path.EqualsIgnoreCase(_connection?.Path)
                || !RCommandLineArguments.EqualsIgnoreCase(_connection?.RCommandLineArguments);

            Uri uri;
            CanConnect = string.IsNullOrEmpty(Name) || Uri.TryCreate(Name, UriKind.Absolute, out uri);
        }
    }
}