using NUnit.Framework;
using Octgn.Communication.Utility;
using System.Collections.Generic;

namespace Octgn.Communication.Test.Utility
{
    [TestFixture]
    public class UIDTests
    {
        [TestCase]
        public void NoDuplicates() {
            var values = new HashSet<string>();
            for(var i = 0; i < 5000000; i++) {
                values.Add(UID.Generate(i));
            }
        }
    }
}
