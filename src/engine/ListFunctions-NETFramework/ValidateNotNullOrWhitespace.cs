namespace System.Management.Automation
{
    internal sealed class ValidateNotNullOrWhiteSpaceAttribute : ValidateArgumentsAttribute
    {
        private const string NULL_EMPTY_ERROR = "The argument is null or empty. Provide an argument that is not null or empty, and then try the command again.";
        private const string WHITESPACE_ERROR = "The argument is null, empty, or consists of only white-space characters. Provide an argument that contains non white-space characters, and then try the command again.";

        protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
        {
            if (arguments is null)
            {
                throw new ValidationMetadataException(
                    NULL_EMPTY_ERROR);
            }

            if (!LanguagePrimitives.TryConvertTo(arguments, out string s) || s is null)
            {
                return;
            }

            if (s.Equals(string.Empty))
            {
                throw new ValidationMetadataException(
                    NULL_EMPTY_ERROR);
            }

            foreach (char c in s)
            {
                if (!char.IsWhiteSpace(c))
                {
                    return;
                }
            }

            throw new ValidationMetadataException(WHITESPACE_ERROR);
        }
    }
}