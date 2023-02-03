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
            const int reps = 5_000_000;
            var values = new HashSet<string>();
            for(var i = 0; i < reps; i++) {
                values.Add(UID.Generate(i));
            }
            Assert.AreEqual(reps, values.Count, "Collision detected");
        }
    }
}
