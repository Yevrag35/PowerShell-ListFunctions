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
    public static partial class ScriptBlockExtensions
    {
        public static ScriptBlock ReplaceWithArgsZero(this ScriptBlock scriptBlock)
        {
            Guard.NotNull(scriptBlock, nameof(scriptBlock));

            string script = scriptBlock.ToString();
            string newScript = ReplaceString(script);

            return !string.Equals(script, newScript, StringComparison.OrdinalIgnoreCase)
                ? ScriptBlock.Create(newScript)
                : scriptBlock;
        }

        private static string ReplaceString(string script)
        {
#if !NETCOREAPP
            return Regex.Replace(script, @"\$(?:(?:_|PSItem|this)(\s|\;|$|\.))", "$args[0]$1", RegexOptions.IgnoreCase);
        }
#else
            return ReplaceDefaultNames().Replace(script, "$args[0]$1");
        }

        [GeneratedRegex(@"\$(?:(?:_|PSItem|this)(\s|\;|$|\.))", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex ReplaceDefaultNames();
#endif
    }
}
