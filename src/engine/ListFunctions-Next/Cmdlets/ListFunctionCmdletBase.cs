using ListFunctions.Exceptions;
using ListFunctions.Extensions;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

#nullable enable

namespace ListFunctions.Cmdlets
{
    public abstract class ListFunctionCmdletBase : PSCmdlet
    {
        protected const string WITH_CUSTOM_EQUALITY = "WithCustomEquality";
        const string PREFERENCE = "Preference";
        protected const string ERROR_ACTION = "ErrorAction";
        protected const string ERROR_ACTION_PREFERENCE = ERROR_ACTION + PREFERENCE;

        private bool _wantsToStop;

        protected sealed override void BeginProcessing()
        {
            try
            {
                this.BeginCore();
            }
            catch
            {
                this.CleanupCore();
                _wantsToStop = true;
                throw;
            }
        }
        protected sealed override void ProcessRecord()
        {
            if (_wantsToStop)
                return;

            try
            {
                _wantsToStop = !this.ProcessCore();
            }
            catch
            {
                try
                {
                    this.StopProcessing();
                }
                catch
                {
                    throw;
                }
                finally
                {
                    this.CleanupCore();
                    _wantsToStop = true;
                }
            }
        }
        protected sealed override void EndProcessing()
        {
            try
            {
                this.EndCore(_wantsToStop);
            }
            finally
            {
                this.CleanupCore();
            }
        }
        protected virtual void BeginCore()
        {
        }
        protected abstract bool ProcessCore();
        protected virtual void EndCore(bool wantsToStop)
        {
        }
        
        private void CleanupCore()
        {
            try
            {
                this.Cleanup();
            }
            catch (Exception e)
            {
                Debug.Fail(e.Message);
            }
        }
        protected virtual void Cleanup()
        {
            // Override to implement custom cleanup logic
        }

        protected ActionPreference GetErrorPreference()
        {
            if (!this.MyInvocation.BoundParameters.TryGetValue(ERROR_ACTION, out object? errorObj))
            {
                errorObj = this.SessionState.PSVariable.GetValue(ERROR_ACTION_PREFERENCE);
            }

            return errorObj is ActionPreference actionPref
                ? actionPref
                : default;
        }

        protected bool TryConvertItem(object? item, Type convertTo, [NotNullWhen(true)] out object? result)
        {
            try
            {
                result = LanguagePrimitives.ConvertTo(item, convertTo);
                return !(result is null);
            }
            catch (PSInvalidCastException e)
            {
                this.WriteConversionError(e, item, convertTo);
                result = null;
                return false;
            }
        }

        private void WriteConversionError(PSInvalidCastException thrownException, object? item, Type convertToType)
        {
            string errorId = thrownException.GetType().GetTypeName();
            ErrorCategory cat = ErrorCategory.InvalidType;

            var castEx = new LFInvalidCastException(thrownException, convertToType, item);

            this.WriteError(new ErrorRecord(
                exception: castEx,
                errorId: errorId,
                errorCategory: cat,
                targetObject: item));
        }
    }
}
