using ListFunctions.Components;
using ListFunctions.Exceptions;
using ListFunctions.Extensions;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using System.Threading;

#nullable enable

namespace ListFunctions.Cmdlets
{
    /// <summary>
    /// Provides a base class for PowerShell cmdlets that implement list-like functions with custom processing and error
    /// handling logic.
    /// </summary>
    /// <remarks>This abstract class is intended to be inherited by cmdlets that require structured processing
    /// phases (begin, process, end) and custom error management. It enforces a processing workflow and provides utility
    /// methods for error preference retrieval and type conversion. Derived classes should override the core processing
    /// methods to implement specific cmdlet behavior.</remarks>
    public abstract class ListFunctionCmdletBase : PSCmdlet
    {
        protected const string WITH_CUSTOM_EQUALITY = "WithCustomEquality";
        const string PREFERENCE = "Preference";
        protected const string ERROR_ACTION = "ErrorAction";
        protected const string ERROR_ACTION_PREFERENCE = ERROR_ACTION + PREFERENCE;

        private CmdletRunState _state;

        protected sealed override void BeginProcessing()
        {
            try
            {
                this.BeginCore();
            }
            catch (Exception e)
            {
                _state = _state.With(CmdletRunFlags.BeginFailed);
                this.CleanupCore();
                this.ThrowTerminatingError(e.ToRecord(ErrorCategory.InvalidArgument));
            }
        }
        protected sealed override void ProcessRecord()
        {
            if (this.Stopping || this.PipelineStopToken.IsCancellationRequested)
            {
                _state = _state.With(CmdletRunFlags.IsStopping);
                return;
            }

            if (_state.ShouldSkipProcess)
            {
                //this.SetStopping();
                return;
            }

            try
            {
                bool keepGoing = this.ProcessCore();

                if (!keepGoing)
                    _state = _state.With(CmdletRunFlags.FoundMatch);
            }
            catch (Exception e)
            {
                _state = _state.With(CmdletRunFlags.ProcessFailed);
                this.CleanupCore();
                this.ThrowTerminatingError(e.ToRecord(ErrorCategory.NotSpecified));
            }
        }
        protected sealed override void EndProcessing()
        {
            try
            {
                this.EndCore(_state);
            }
            finally
            {
                this.CleanupCore();
            }
        }
        /// <summary>
        /// When overridden in a derived class, performs provider-specific logic required to begin cmdlet processing or
        /// operations.
        /// </summary>
        /// <remarks>Override this method in a subclass to implement cmdlet behavior that should occur at
        /// the start of processing. The base implementation does nothing.</remarks>
        protected virtual void BeginCore()
        {
        }
        /// <summary>
        /// When implemented in a derived class, performs the core processing logic for the cmdlet.
        /// </summary>
        /// <returns><see langword="true"/> to continue processing; <see langword="false"/> to stop processing.</returns>
        protected abstract bool ProcessCore();
        /// <param name="wantsToStop">true to request that the operation is requesting to stop; otherwise, false.</param>
        /// <summary>
        /// Performs custom logic when ending cmdlet processing, optionally indicating whether the operation should stop.
        /// </summary>
        /// <remarks>Override this method in a derived class to implement specific behavior that should
        /// occur when the operation ends. The base implementation does nothing.</remarks>
        /// <param name="state">The current state of the cmdlet run, including flags indicating processing outcomes.</param>
        protected virtual void EndCore(CmdletRunState state)
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
                throw;
            }
        }
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <remarks>Override this method in a derived class to implement custom cleanup logic. This
        /// method is called to allow derived types to release resources or perform other cleanup operations before the
        /// object is disposed or finalized.</remarks>
        protected virtual void Cleanup()
        {
            // Override to implement custom cleanup logic
        }

        /// <summary>
        /// Retrieves the current error action preference to determine how errors are handled during command execution.
        /// </summary>
        /// <remarks>This method checks for an explicitly bound error action parameter before falling back
        /// to the session state's error action preference variable. Use this value to control error handling logic in
        /// derived cmdlets.</remarks>
        /// <returns>An <see cref="ActionPreference"/> value that specifies the error handling behavior. Returns the current error action
        /// preference if set; otherwise, returns <see cref="ActionPreference.Continue"/>.</returns>
        protected ActionPreference GetErrorPreference()
        {
            if (!this.MyInvocation.BoundParameters.TryGetValue(ERROR_ACTION, out object? errorObj))
            {
                errorObj = this.SessionState.PSVariable.GetValue(ERROR_ACTION_PREFERENCE);
            }

            return errorObj is ActionPreference actionPref
                ? actionPref
                : ActionPreference.Continue;
        }

        /// <summary>
        /// Attempts to convert the specified object to the given target type.
        /// </summary>
        /// <remarks>If the conversion fails due to an invalid cast, an error is written and <paramref
        /// name="result"/> is set to null. This method does not throw an exception for conversion failures.</remarks>
        /// <param name="item">The object to convert. This value can be null.</param>
        /// <param name="convertTo">The type to which to attempt to convert the object. Cannot be null.</param>
        /// <param name="result">When this method returns, contains the converted object if the conversion succeeded; otherwise, null. This
        /// parameter is passed uninitialized.</param>
        /// <returns>true if the conversion was successful and <paramref name="result"/> contains the converted value; otherwise,
        /// false.</returns>
        protected bool TryConvertItem(object? item, Type convertTo, [NotNullWhen(true)] out object? result)
        {
            try
            {
                result = LanguagePrimitives.ConvertTo(item, convertTo);
                return result is not null;
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

//        private void SetStopping()
//        {
//#if NET10_0_OR_GREATER
//            object processor = GetPipelineProcessor(this.CommandRuntime);
//            GetStopSource(processor).Cancel();
//            //StopProcessing(processor);
//#endif
//        }
//#if NET10_0_OR_GREATER

//        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_PipelineProcessor")]
//        [return: UnsafeAccessorType("System.Management.Automation.Internal.PipelineProcessor, System.Management.Automation")]
//        private static extern object GetPipelineProcessor([UnsafeAccessorType("System.Management.Automation.MshCommandRuntime, System.Management.Automation")] object runtime);

//        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pipelineStopTokenSource")]
//        private static extern ref readonly CancellationTokenSource GetStopSource([UnsafeAccessorType("System.Management.Automation.Internal.PipelineProcessor, System.Management.Automation")] object pipelineProcessor);

//        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_stopping")]
//        private static extern ref bool GetIsStoppingField([UnsafeAccessorType("System.Management.Automation.Internal.PipelineProcessor, System.Management.Automation")] object pipelineProcessor);

//        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Stop")]
//        private static extern void StopProcessing([UnsafeAccessorType("System.Management.Automation.Internal.PipelineProcessor, System.Management.Automation")] object pipelineProcessor);
//#endif
    }
}
