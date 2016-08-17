﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.R.Host.Broker.Protocol;
using Microsoft.R.Host.Protocol;
using Newtonsoft.Json;

namespace Microsoft.R.Host.BrokerServices {
    public class SessionsWebService : WebService, ISessionsWebService {
        public SessionsWebService(HttpClient httpClient)
            : base(httpClient) {
        }

        private static readonly Uri getUri = new Uri("/sessions", UriKind.Relative);

        public Task<IEnumerable<SessionInfo>> GetAsync() =>
            HttpGetAsync<IEnumerable<SessionInfo>>(getUri);

        private static readonly UriTemplate putUri = new UriTemplate("/sessions/{name}");

        public Task<SessionInfo> PutAsync(string id, SessionCreateRequest request) =>
            HttpPutAsync<SessionCreateRequest, SessionInfo>(putUri, request, id);
    }
}
