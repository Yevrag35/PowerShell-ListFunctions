namespace ListFunctions.Engine.Tests.Validation;

// On .NET 10, Engine has no polyfill, so these tests run PowerShell 7's own attribute and show what Engine's polyfill
// has to do. On .NET Framework 4.8, they run the polyfill. They leave out collections: PowerShell 7's attribute checks
// each element of a collection, and the polyfill checks the collection converted to a string.
public sealed class ValidateNotNullOrWhiteSpaceAttributeTests
{
	[Theory]
	[InlineData("a")]
	[InlineData(" a ")]
	[InlineData(5)]
	public void Validate_AcceptsAValueWithANonWhiteSpaceCharacter(object argument)
	{
		var attribute = new ValidateNotNullOrWhiteSpaceAttribute();

		ArgumentValidator.Validate(attribute, argument);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("\t\r\n")]
	public void Validate_RejectsNullEmptyOrWhiteSpace(string? argument)
	{
		var attribute = new ValidateNotNullOrWhiteSpaceAttribute();

		Assert.Throws<ValidationMetadataException>(() => ArgumentValidator.Validate(attribute, argument));
	}
}
