using ListFunctions.Internal;
using System;
using System.Management.Automation;
using System.Runtime.CompilerServices;

namespace ListFunctions.Modern
{
    public abstract class ComparingBase
    {
        public ScriptBlock Script { get; }

        private protected ComparingBase(ScriptBlock scriptBlock, bool preValidated)
        {
            if (!preValidated)
            {
                ValidateScriptBlock(scriptBlock);
            }
            else
            {
                Guard.NotNull(scriptBlock);
            }

            this.Script = scriptBlock;
        }

        /// <exception cref="ArgumentException"/>
        /// <exception cref="ArgumentNullException"/>
        private static void ValidateScriptBlock(ScriptBlock scriptBlock, [CallerArgumentExpression(nameof(scriptBlock))] string? paramName = null)
        {
            if (!scriptBlock.IsProperScriptBlock())
            {
                paramName ??= nameof(scriptBlock);
                throw new ArgumentException($"{nameof(scriptBlock)} is not a script block.", paramName);
            }
        }
    }
}
