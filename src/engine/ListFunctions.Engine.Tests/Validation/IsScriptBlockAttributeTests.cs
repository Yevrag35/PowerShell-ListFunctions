using ListFunctions.Validation;

namespace ListFunctions.Engine.Tests.Validation;

public sealed class IsScriptBlockAttributeTests
{
	[Theory]
	[InlineData("$_")]
	[InlineData("end { $_ }")]
	[InlineData("process { $_ }")]
	public void Validate_AcceptsAScriptBlockWithAStatement(string script)
	{
		var attribute = new IsScriptBlockAttribute();

		ArgumentValidator.Validate(attribute, ScriptBlock.Create(script));
	}

	// ScriptBlock.InvokeWithContext, which runs the script blocks, refuses a script block that has a begin block, or both a
	// process block and an end block.
	[Theory]
	[InlineData("begin { } process { $_ }")]
	[InlineData("process { $_ } end { $_ }")]
	public void Validate_RejectsAScriptBlockThatInvokeWithContextCannotRun(string script)
	{
		var attribute = new IsScriptBlockAttribute();

		Assert.Throws<ValidationMetadataException>(() => ArgumentValidator.Validate(attribute, ScriptBlock.Create(script)));
	}

	[Theory]
	[InlineData("")]
	[InlineData("# A comment")]
	[InlineData("param($x)")]
	[InlineData("end { }")]
	public void Validate_RejectsAScriptBlockWithoutAStatement(string script)
	{
		var attribute = new IsScriptBlockAttribute();

		Assert.Throws<ValidationMetadataException>(() => ArgumentValidator.Validate(attribute, ScriptBlock.Create(script)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(5)]
	public void Validate_AcceptsAnArgumentThatIsNotAScriptBlock(object? argument)
	{
		var attribute = new IsScriptBlockAttribute();

		ArgumentValidator.Validate(attribute, argument);
	}
}
