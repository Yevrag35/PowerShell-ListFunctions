using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

namespace ListFunctions.Modern
{
    /// <summary>
    /// Defines a contract for computing a hash code for a given object using a PowerShell script block, with optional
    /// support for injecting additional PowerShell variables into the script.
    /// </summary>
    public interface IHashBlock
    {
        /// <summary>
        /// Calculates the hash code for the specified object by executing a PowerShell script block,
        /// optionally injecting additional contextual variables.
        /// </summary>
        /// <param name="obj">The object for which to compute the hash code. Cannot be null.</param>
        /// <param name="additionalVariables">An optional collection of PowerShell variables to include in the hash code calculation. May be null.</param>
        /// <returns>An integer hash code representing the specified object and any additional variables.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is null.</exception>
        int GetHashCode([DisallowNull] object obj, IEnumerable<PSVariable>? additionalVariables);
    }

    public interface IHashCodeBlock
    {
        Type HashesType { get; }
        int GetHashCode(object obj, IEnumerable<PSVariable> additionalVariables);
    }
}
