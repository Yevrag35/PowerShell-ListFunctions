using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace ListFunctions.Cmdlets.Constructs
{
    [Cmdlet(VerbsCommon.New, "SortedSet")]
    [OutputType(typeof(SortedSet<>))]
    public sealed class NewSortedSetCmdlet : ListFunctionCmdletBase
    {
        static readonly Type _setType = typeof(SortedSet<>);

    }
}

