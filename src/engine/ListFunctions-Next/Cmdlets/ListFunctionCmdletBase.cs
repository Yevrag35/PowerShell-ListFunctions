using ListFunctions.Exceptions;
using ListFunctions.Extensions;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

namespace ListFunctions.Cmdlets
{
    public abstract class ListFunctionCmdletBase : PSCmdlet
    {
        protected const string WITH_CUSTOM_EQUALITY = "WithCustomEquality";
        const string PREFERENCE = "Preference";
        protected const string ERROR_ACTION = "ErrorAction";
        protected const string ERROR_ACTION_PREFERENCE = ERROR_ACTION + PREFERENCE;

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
