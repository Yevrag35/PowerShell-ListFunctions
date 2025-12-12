using ListFunctions.Internal;
using System;
using System.Management.Automation;
using System.Runtime.CompilerServices;

namespace ListFunctions.Modern
{
    /// <summary>
    /// Provides a base class for objects that encapsulate a script block used for comparison operations.
    /// </summary>
    /// <remarks>This class is intended to be inherited by types that require a validated script block for
    /// custom comparison logic. The script block is validated upon construction to ensure it meets the requirements for
    /// use in comparison scenarios.</remarks>
    public abstract class ComparingBase
    {
        /// <summary>
        /// Gets the PowerShell script block that is used for comparison operations.
        /// </summary>
        public ScriptBlock Script { get; }

        /// <summary>
        /// Initializes a new instance of the ComparingBase class with the specified script block, optionally skipping
        /// validation if it has already been performed.
        /// </summary>
        /// <remarks>If preValidated is set to true, the constructor assumes that scriptBlock has already
        /// been validated and skips additional validation checks. Otherwise, the script block is validated before
        /// assignment.</remarks>
        /// <param name="scriptBlock">The script block to be used by the instance. Cannot be null.</param>
        /// <param name="preValidated">true to indicate that the script block has already been validated and validation should be skipped;
        /// otherwise, false to perform validation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="scriptBlock"/> is null.</exception>
        /// <inheritdoc cref="ValidateScriptBlock(ScriptBlock, string)" path="/exception"/>
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
