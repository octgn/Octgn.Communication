using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IModule : IDisposable
    {
        string Name { get; }
        void Initialize();
        Task<ProcessResult> Process(object obj, CancellationToken cancellationToken = default);
        IEnumerable<Type> IncludedTypes { get; }
    }

    public class Module : IModule
    {
        public string Name { get; set; }

        public Module() {
            Name = GetType().Name + "-" + Guid.NewGuid().ToString().Replace("-", "").ToUpper();
        }

        public Module(string name) {
            Name = name;
        }

        public virtual async Task<ProcessResult> Process(object obj, CancellationToken cancellationToken = default) {
            if (_children == null) return ProcessResult.Unprocessed;

            foreach (var child in _children.Values) {
                var result = await child.Process(obj, cancellationToken);
                if (result.WasProcessed) return result;
            }

            return ProcessResult.Unprocessed;
        }

        #region Children

        private IDictionary<string, IModule> _children;

        public void Attach(IModule child) {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (string.IsNullOrWhiteSpace(child.Name)) throw new InvalidOperationException($"Cannot attach a module without a name.");

            // We create this on demand because we don't want small modules with no children to use lots of memory from dictionaries.
            if (_children == null) _children = new Dictionary<string, IModule>();

            if (_children.ContainsKey(child.Name)) throw new InvalidOperationException($"Module with the name {child.Name} was already attached.");

            if (child is Module module) {
                module._parent = this;
            }

            _children.Add(child.Name, child);

            if (_isInitialized) {
                child.Initialize();
            }
        }

        public IEnumerable<IModule> GetModules() {
            return _children?.Values.ToArray() ?? Enumerable.Empty<IModule>();
        }

        public T GetModule<T>() where T : IModule {
            return _children.Values.OfType<T>().Single();
        }

        public T GetModule<T>(string name) where T : IModule {
            return _children.OfType<T>().Where(c => c.Name == name).Single();
        }

        #endregion Children

        #region Initialization

        private IModule _parent;

        private bool _isInitialized;

        public virtual void Initialize() {
            if (string.IsNullOrWhiteSpace(Name)) throw new InvalidOperationException($"Module name is invalid");
            if (_isInitialized) throw new InvalidOperationException($"{nameof(Initialize)} cannot be called more than once.");
            _isInitialized = true;

            if (_children == null) return;

            foreach (var child in _children.Values) {
                child.Initialize();
            }
        }

        #endregion Initialization

        public virtual IEnumerable<Type> IncludedTypes => (_children == null) ? Enumerable.Empty<Type>() : _children.Values.SelectMany(child => child.IncludedTypes);

        public override string ToString() {
            return $"Module {Name}";
        }

        #region IDisposable Support
        protected bool IsDisposed => _isDisposed;
        private bool _isDisposed = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    var children = _children?.Values;

                    if (children != null) {
                        foreach (var childModule in children) {
                            childModule.Dispose();
                        }
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                _children?.Clear();

                _isDisposed = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ModuleBase() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    public struct ProcessResult
    {
        public bool WasProcessed { get; }
        public object Result { get; }

        public ProcessResult(object result) {
            WasProcessed = true;
            Result = result;
        }

        public static ProcessResult Unprocessed = new ProcessResult();
        public static ProcessResult Processed = new ProcessResult(null);
    }
}
