using ListFunctions.Internal;
using System.Management.Automation;

namespace ListFunctions.Validation
{
    public sealed class IsScriptBlockAttribute : ValidateArgumentsAttribute
    {
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
