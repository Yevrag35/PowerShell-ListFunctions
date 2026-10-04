using ListFunctions.Validation;

namespace ListFunctions.Engine.Tests.Validation;

public sealed class ValidateScriptVariableAttributeTests
{
	[Fact]
	public void Constructor_ThrowsWhenTheNamesAreNull()
	{
		Assert.Throws<ArgumentNullException>(() => new ValidateScriptVariableAttribute(null!));
	}

	[Fact]
	public void Constructor_ThrowsWhenThereAreNoNames()
	{
		Assert.Throws<ArgumentException>(() => new ValidateScriptVariableAttribute());
	}

	// The cmdlets accept these variables in a script block that gets one element.
	[Theory]
	[InlineData("$_ -gt 1")]
	[InlineData("$this -gt 1")]
	[InlineData("$PSItem -gt 1")]
	[InlineData("$args[0] -gt 1")]
	[InlineData("$ARGS[0] -gt 1")]
	public void Validate_AcceptsAScriptBlockThatUsesAnAcceptedVariable(string script)
	{
		var attribute = new ValidateScriptVariableAttribute("_", "this", "psitem", "args[0]");

		ArgumentValidator.Validate(attribute, ScriptBlock.Create(script));
	}

	// Only the script block's own body counts. The $_ in a nested script block, such as the one that ForEach-Object runs,
	// is a different variable.
	[Theory]
	[InlineData("1")]
	[InlineData("$x -gt 1")]
	[InlineData("$args[1] -gt 1")]
	[InlineData("$args[$i] -gt 1")]
	[InlineData("$args")]
	[InlineData("1 | ForEach-Object { $_ }")]
	[InlineData("{ $args[0] }")]
	public void Validate_RejectsAScriptBlockThatUsesNoAcceptedVariable(string script)
	{
		var attribute = new ValidateScriptVariableAttribute("_", "this", "psitem", "args[0]");

		Assert.Throws<ValidationMetadataException>(() => ArgumentValidator.Validate(attribute, ScriptBlock.Create(script)));
	}

	// The constructor separates the $args indexes from the variable names wherever they appear.
	[Theory]
	[InlineData("$y")]
	[InlineData("$right")]
	[InlineData("$args[1]")]
	public void Validate_AcceptsNamesAndIndexesInAnyOrder(string script)
	{
		var attribute = new ValidateScriptVariableAttribute("args[1]", "y", "right");

		ArgumentValidator.Validate(attribute, ScriptBlock.Create(script));
	}

	// $script:x is the script scope's $x, not the $x that a cmdlet defines for the script block.
	[Fact]
	public void Validate_RejectsAScopeQualifiedVariable()
	{
		var attribute = new ValidateScriptVariableAttribute("x", "left", "args[0]");

		Assert.Throws<ValidationMetadataException>(() => ArgumentValidator.Validate(attribute, ScriptBlock.Create("$script:x -eq 1")));
	}

	[Fact]
	public void Validate_ParsesAStringAsAScriptBlock()
	{
		var attribute = new ValidateScriptVariableAttribute("_", "this", "psitem", "args[0]");

		ArgumentValidator.Validate(attribute, "$_ -gt 1");
		Assert.Throws<ValidationMetadataException>(() => ArgumentValidator.Validate(attribute, "$x -gt 1"));
	}

	[Fact]
	public void Validate_ThrowsForAStringThatIsNotAScript()
	{
		var attribute = new ValidateScriptVariableAttribute("_", "this", "psitem", "args[0]");

		Assert.Throws<ParseException>(() => ArgumentValidator.Validate(attribute, "$_ -gt ("));
	}

	[Theory]
	[InlineData(null)]
	[InlineData(5)]
	public void Validate_AcceptsAnArgumentThatIsNotAScriptBlockOrAString(object? argument)
	{
		var attribute = new ValidateScriptVariableAttribute("_", "this", "psitem", "args[0]");

		ArgumentValidator.Validate(attribute, argument);
	}

	[Fact]
	public void Validate_ListsTheAcceptedVariableNamesInTheMessage()
	{
		var attribute = new ValidateScriptVariableAttribute("x", "left", "args[0]");

		var e = Assert.Throws<ValidationMetadataException>(() => ArgumentValidator.Validate(attribute, ScriptBlock.Create("$y")));
		Assert.StartsWith("At least one of the following variables must be included in the script block: ", e.Message);
		Assert.Contains("$x", e.Message);
		Assert.Contains("$left", e.Message);
	}
}
