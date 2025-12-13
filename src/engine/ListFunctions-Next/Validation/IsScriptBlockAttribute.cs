using ListFunctions.Internal;
using System;
using System.Management.Automation;

namespace ListFunctions.Validation
{
    /// <summary>
    /// Specifies that a property or field must be assigned a valid script block value during validation.
    /// </summary>
    /// <remarks>Apply this attribute to properties or fields to ensure that only proper script block values
    /// are accepted. If the value is not a valid script block, validation will fail and an exception will be thrown.
    /// This attribute is typically used in PowerShell cmdlet or parameter classes to enforce script block
    /// constraints.</remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class IsScriptBlockAttribute : ValidateArgumentsAttribute
    {
        /// <inheritdoc/>
        protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
        {
            if (arguments is ScriptBlock block && !block.IsProperScriptBlock())
            {
                throw new ValidationMetadataException(
                    $"{nameof(block)} is not a proper script block.");
            }
        }
    }
}
