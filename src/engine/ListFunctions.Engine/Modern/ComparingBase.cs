using ListFunctions.Internal;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;

namespace ListFunctions.Modern
{
    public abstract class ComparingBase<T>
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
                Guard.NotNull(scriptBlock, nameof(scriptBlock));
            }

            this.Script = scriptBlock;
        }

        /// <exception cref="ArgumentException"/>
        /// <exception cref="ArgumentNullException"/>
        private static void ValidateScriptBlock(ScriptBlock scriptBlock)
        {
            if (!scriptBlock.IsProperScriptBlock())
            {
                throw new ArgumentException($"{nameof(scriptBlock)} is not a script block.");
            }
        }
    }
}
