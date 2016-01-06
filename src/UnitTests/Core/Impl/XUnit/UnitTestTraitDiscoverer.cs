using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.UnitTests.Core.XUnit {
    public sealed class UnitTestTraitDiscoverer : ITraitDiscoverer {
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute) {
            yield return new KeyValuePair<string, string>("UnitTests", null);
        }
    }
}