using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Octgn.Communication
{

    public class CorruptDataException : Exception
    {
        public CorruptDataException() {

        }

        public CorruptDataException(string message) : base(message) {

        }
    }
}
