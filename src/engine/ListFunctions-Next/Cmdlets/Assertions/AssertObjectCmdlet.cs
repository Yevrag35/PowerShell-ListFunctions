using ListFunctions.Modern;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

using AllowsNull = System.Diagnostics.CodeAnalysis.AllowNullAttribute;

#nullable enable

namespace ListFunctions.Cmdlets.Assertions
{
    public abstract class AssertObjectCmdlet : ListFunctionCmdletBase, IDisposable
    {
        private bool _disposed;

        public virtual ScriptBlock? Condition
        {
            get;
            set
            {
                field = value;
                this.HasCondition = !(value is null || string.IsNullOrWhiteSpace(value.ToString()));
            }
        }
        public abstract ActionPreference ScriptBlockErrorAction { get; set; }

        [AllowsNull]
        private protected ScriptBlockFilter? Filter { get; private set; }

        [MemberNotNullWhen(true, nameof(Condition), nameof(Filter))]
        protected private bool HasCondition { get; set; }

        protected sealed override void BeginCore()
        {
            base.BeginCore();

            if (this.HasCondition)
            {
                this.Filter = new ScriptBlockFilter(this.Condition, new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction));
            }

            // # Maybe in the future.
            //try
            //{
            //    this.Begin();
            //}
            //catch
            //{
            //    this.Cleanup();
            //    throw;
            //}
        }

        protected sealed override bool ProcessCore()
        {
            return this.HasCondition
                ? !this.Process(this.Filter)
                : !this.ProcessWhenNoCondition();
        }
        protected abstract bool Process(ScriptBlockFilter filter);
        protected abstract bool ProcessWhenNoCondition();

        protected sealed override void EndCore(bool wantsToStop)
        {
            this.End(wantsToStop);
        }
        protected abstract void End(bool scriptResult);

        protected override void Cleanup()
        {
            this.Dispose();
        }
        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && this.Filter is not null)
                {
                    this.Filter.Dispose();
                    this.Filter = null;
                }

                _disposed = true;
            }
        }
    }
}

