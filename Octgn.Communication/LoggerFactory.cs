using System;

namespace Octgn.Communication
{

    public static class LoggerFactory
    {
        public static Func<Context,ILogger> DefaultMethod { get; set; }

        public static ILogger Create(string name) {
            var context = new Context {
                Name = name
            };
            return DefaultMethod?.Invoke(context) ?? new NullLogger(context);
        }

        public static ILogger Create(Type type) => Create(type.Name);

        public class Context
        {
            public string Name { get; set; }
        }
    }
}
