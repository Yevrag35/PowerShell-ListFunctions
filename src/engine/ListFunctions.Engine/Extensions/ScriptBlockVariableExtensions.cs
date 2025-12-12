using ListFunctions.Modern.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text.RegularExpressions;
using ZLinq;

#nullable enable

namespace ListFunctions.Extensions
{
    /// <summary>
    /// Provides extension methods for processing and transforming PowerShell ScriptBlock variables, enabling
    /// replacement of specific argument references within ScriptBlock scripts.
    /// </summary>
    /// <remarks>This class is intended for use with PowerShell ScriptBlock objects to facilitate manipulation
    /// of variable references, such as replacing references to common automatic variables with argument array
    /// accessors. All methods are static and can be used as extension methods on ScriptBlock instances.</remarks>
    public static partial class ScriptBlockVariableExtensions
    {
        /// <summary>
        /// Returns a new ScriptBlock with argument references replaced by <c>$args[0]</c>, or the 
        /// original ScriptBlock if no replacements are necessary.
        /// </summary>
        /// <param name="scriptBlock">The ScriptBlock to process. Cannot be null.</param>
        /// <returns>A new ScriptBlock with argument references replaced by <c>$args[0]</c> if replacements were made; otherwise, the
        /// original ScriptBlock.</returns>
        public static ScriptBlock ReplaceWithArgsZero(this ScriptBlock scriptBlock)
        {
            Guard.NotNull(scriptBlock);

            string script = scriptBlock.ToString();
            string newScript = ReplaceString(script);

            return !string.Equals(script, newScript, StringComparison.OrdinalIgnoreCase)
                ? ScriptBlock.Create(newScript)
                : scriptBlock;
        }

        private static string ReplaceString(string script)
        {
#if !NETCOREAPP
            return Regex.Replace(
                script,
                @"\$(?:(?:_|PSItem|this)(\s|\)|\""|\;|$|\.|\,|\'|\#))",
                "$args[0]$1",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
#else
            return ReplaceDefaultNames().Replace(script, "$args[0]$1");
        }

        [GeneratedRegex(@"\$(?:(?:_|PSItem|this)(\s|\)|\""|\;|$|\.|\,|\'|\#))", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex ReplaceDefaultNames();
#endif
    }
}
