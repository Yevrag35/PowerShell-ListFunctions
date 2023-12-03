using System.Management.Automation;

namespace ListFunctions.Cmdlets
{
    public abstract class ListFunctionCmdletBase : PSCmdlet
    {
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
    }
}
