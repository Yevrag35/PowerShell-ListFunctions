using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Pools;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

#nullable enable

namespace ListFunctions.Cmdlets.Finds
{
    /// <summary>
    /// Implements a PowerShell cmdlet that finds the zero-based index of the first element in a collection that matches
    /// a specified condition.
    /// </summary>
    /// <remarks>This cmdlet accepts input from the pipeline or via the InputObject parameter and evaluates
    /// each element against the provided script block condition. If no element satisfies the condition, the cmdlet
    /// returns -1. The cmdlet is functionally similar to the .NET <see cref="List{T}.FindIndex(Predicate{T})"/> method, but is designed for use
    /// within PowerShell scripts and pipelines.</remarks>
    [Cmdlet(VerbsCommon.Find, "IndexOf")]
    [Alias("Find-Index", "IndexOf")]
    [OutputType(typeof(int))]
    public sealed class FindIndexCmdlet : ListFunctionCmdletBase
    {
        private ScriptBlockFilter _filter = null!;
        private int _currentIndex;
        private List<object?> _list = null!;

        /// <summary>
        /// Gets or sets the PowerShell script block that defines the condition to evaluate.
        /// </summary>
        /// <remarks>The script block is executed to determine whether a specific action should be
        /// performed. The script block can reference special variables such as $_, $this, $PSItem, and $args[0] within
        /// its scope. This property is mandatory and must be set before use.</remarks>
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("ScriptBlock")]
        [ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
        public ScriptBlock Condition { get; set; } = null!;

        /// <summary>
        /// Gets or sets the collection of input objects to be processed by the cmdlet.
        /// </summary>
        /// <remarks>This property accepts input from the pipeline and can be set to null, an empty array,
        /// or an array containing null or empty elements. The cmdlet processes each object in the collection
        /// individually.</remarks>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [Alias("List")]
        [AllowEmptyCollection, AllowEmptyString, AllowNull]
        public object?[]? InputObject { get; set; }

        [Parameter, Alias("ScriptErrorAction")]
        public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

        protected override void BeginCore()
        {
            _filter = new ScriptBlockFilter(this.Condition, new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction));
            _list = ListPool<object?>.Rent();
        }
        protected override bool ProcessCore()
        {
            if (this.InputObject is null || this.InputObject.Length == 0)
                return true;    // keep going

            for (int i = 0; i < this.InputObject.Length; i++)
            {
                if (_filter.IsTrue(this.InputObject[i]))
                {
                    _currentIndex += i;
                    return false;   // stop processing
                }
            }

            _currentIndex += this.InputObject.Length;
            return true;
        }

        protected override void EndCore(bool wantsToStop)
        {
            int index = wantsToStop ? _currentIndex : -1;

            this.WriteObject(index);
        }

        protected override void Cleanup()
        {
            if (_filter is not null)
            {
                _filter.Dispose();
                _filter = null!;
            }

            if (_list is not null)
            {
                ListPool<object?>.Return(_list);
                _list = null!;
            }
        }
    }
}

