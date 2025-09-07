using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;

using AllowsNull = System.Diagnostics.CodeAnalysis.AllowNullAttribute;
using PSAllowNull = System.Management.Automation.AllowNullAttribute;

#nullable enable

namespace ListFunctions.Cmdlets.Assertions
{
    public abstract class AssertObjectCmdlet : ListFunctionCmdletBase, IDisposable
    {
        private ScriptBlock? _condition;
        private bool _disposed;
        private bool _stopRequested;

        [MaybeNull, AllowsNull]
        public virtual ScriptBlock Condition
        {
            get => _condition;
            set
            {
                _condition = value;
                this.HasCondition = !(value is null || string.IsNullOrWhiteSpace(value.ToString()));
            }
        }
        public abstract ActionPreference ScriptBlockErrorAction { get; set; }

        [AllowsNull]
        private protected ScriptBlockFilter? Filter { get; private set; }
#if NETCOREAPP
        [MemberNotNullWhen(true, nameof(Condition), nameof(Filter))]
#endif
        protected private bool HasCondition { get; set; }

        protected sealed override void BeginCore()
        {
            base.BeginCore();

            if (this.HasCondition)
            {
                this.Filter = new ScriptBlockFilter(_condition!, new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction));
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
                ? !this.Process(this.Filter!)
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
                if (disposing && !(this.Filter is null))
                {
                    this.Filter.Dispose();
                    this.Filter = null;
                }

                _disposed = true;
            }
        }
    }
}

