using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Cmdlets.Construct
{
    [Cmdlet(VerbsCommon.New, "Dictionary")]
    [OutputType(typeof(Dictionary<,>))]
    public sealed class NewDictionaryCmdlet : ListFunctionCmdletBase
    {

    }
}
