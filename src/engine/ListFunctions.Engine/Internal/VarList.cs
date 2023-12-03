using ListFunctions.Modern.Variables;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace ListFunctions.Internal
{
    internal sealed class VarList : List<PSVariable>
    {
        internal bool HasThisVar => this.Count > 0 && PSThisVariable.IsThisVariable(this[0]);

        internal VarList(int capacity)
            : base(capacity)
        {
        }

        internal VarList SetContext(object? thisVarValue)
        {
            this.Clear();
            this.AddRange(PSThisVariableOld.CreateVariables(thisVarValue));
            return this;
        }

        internal VarList SetContext(object? thisVarValue, IEnumerable<PSVariable> additionalVariables)
        {
            _ = this.SetContext(thisVarValue);
            this.AddRange(additionalVariables);
            return this;
        }
    }
}
