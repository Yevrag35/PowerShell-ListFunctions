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

            string sc = scriptBlock.ToString();
#if NETCOREAPP
            string newScript = ReplaceDefaultNames().Replace(sc, "$args[0]$1");
#else
            string newScript = Regex.Replace(sc, @"\$(?:(?:_|PSItem|this)(\s|\;|$|\.))", "$args[0]$1", RegexOptions.IgnoreCase);
#endif

            return !string.Equals(sc, newScript, StringComparison.OrdinalIgnoreCase)
                ? ScriptBlock.Create(newScript)
                : scriptBlock;
        }

#if NETCOREAPP
        [GeneratedRegex(@"\$(?:(?:_|PSItem|this)(\s|\;|$|\.))", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex ReplaceDefaultNames();

#endif
    }
}
