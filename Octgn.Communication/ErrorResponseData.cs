using System;
using System.Text;

namespace Octgn.Communication
{
    public sealed class ErrorResponseData
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public bool IsCritical { get; set; }

        public ErrorResponseData() { }

        public ErrorResponseData(string code, string message, bool isCritical) {
            Code = code;
            Message = message;
            IsCritical = isCritical;
        }

        public ErrorResponseData(Exception ex, bool isCritical) {
            Code = ex.GetType().Name + "-" + ex.GetHashCode();
            Message = ex.Message;
            IsCritical = isCritical;
        }

        public override string ToString() {
            var header = IsCritical
                ? "CRITICAL"
                : "Error";

            return $"[{header}: {Code}: {Message}]";
        }
    }
}
