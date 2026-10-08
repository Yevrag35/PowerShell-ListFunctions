using ListFunctions.Validation;

namespace ListFunctions.Engine.Tests.Validation;

public sealed class ValidateScriptVariableAttributeTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public ValidateScriptVariableAttributeTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

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

	// PowerShell binds the elements to the parameters of a param() block in the order they're declared, so the first
	// parameter receives the element. The variables that the cmdlet defines hold it too.
	[Theory]
	[InlineData("param($n) $n -gt 1")]
	[InlineData("param($n, $m) $n -gt 1")]
	[InlineData("param([int] $n) $n -gt 1")]
	[InlineData("param($n) $_ -gt 1")]
	public void Validate_AcceptsAScriptBlockThatUsesTheParameterThatReceivesTheElement(string script)
	{
		var attribute = new ValidateScriptVariableAttribute("_", "this", "psitem", "args[0]");

		ArgumentValidator.Validate(attribute, ScriptBlock.Create(script));
	}

	// Declaring a parameter doesn't use it, and neither does a default value. After a param() block, $args holds only the
	// elements that no parameter receives, and a parameter named like one of the cmdlet's variables takes its place.
	[Theory]
	[InlineData("param($n) 1")]
	[InlineData("param($n = $_) 1")]
	[InlineData("param($n) $args[0] -gt 1")]
	[InlineData("param($n, $m) $m -gt 1")]
	[InlineData("param($n, $this) $this -gt 1")]
	public void Validate_RejectsAScriptBlockThatDoesNotUseTheParameterThatReceivesTheElement(string script)
	{
		var attribute = new ValidateScriptVariableAttribute("_", "this", "psitem", "args[0]");

		Assert.Throws<ValidationMetadataException>(() => ArgumentValidator.Validate(attribute, ScriptBlock.Create(script)));
	}

	// With one parameter, the parameter receives the left operand, and $args[0] holds the right one.
	[Theory]
	[InlineData("param($a, $b) $a.CompareTo($b)")]
	[InlineData("param($a) $a.CompareTo($args[0])")]
	[InlineData("param($a, $b) $x.CompareTo($y)")]
	public void Validate_AcceptsAComparerThatUsesTheVariablesThatReceiveTheOperands(string script)
	{
		var left = new ValidateScriptVariableAttribute("x", "left", "args[0]");
		var right = new ValidateScriptVariableAttribute("y", "right", "args[1]");
		ScriptBlock block = ScriptBlock.Create(script);

		ArgumentValidator.Validate(left, block);
		ArgumentValidator.Validate(right, block);
	}

	// The last script block used to pass, because the check found $x and $y in their declarations.
	[Theory]
	[InlineData("param($a) $a.CompareTo($args[1])")]
	[InlineData("param($a, $b) $a -eq $a")]
	[InlineData("param($x, $y) 0")]
	public void Validate_RejectsAComparerThatDoesNotUseTheRightOperand(string script)
	{
		var right = new ValidateScriptVariableAttribute("y", "right", "args[1]");

		Assert.Throws<ValidationMetadataException>(() => ArgumentValidator.Validate(right, ScriptBlock.Create(script)));
	}

	// A function's syntax tree is a FunctionDefinitionAst, and a search from it skips the function's body. A function
	// declares its parameters in parentheses or in a param() block.
	[Theory]
	[InlineData("function Test-It { $_ -gt 1 }")]
	[InlineData("filter Test-It { $_ -gt 1 }")]
	[InlineData("function Test-It($n) { $n -gt 1 }")]
	[InlineData("function Test-It { param($n) $n -gt 1 }")]
	public void Validate_AcceptsAFunctionThatUsesTheElement(string definition)
	{
		using RunspaceScope scope = _runspace.Enter();
		ScriptBlock function = FunctionScriptBlock.Create(definition);
		var attribute = new ValidateScriptVariableAttribute("_", "this", "psitem", "args[0]");

		Assert.IsType<FunctionDefinitionAst>(function.Ast);
		ArgumentValidator.Validate(attribute, function);
	}

	[Theory]
	[InlineData("function Test-It { 1 }")]
	[InlineData("function Test-It($n) { $args[0] -gt 1 }")]
	public void Validate_RejectsAFunctionThatDoesNotUseTheElement(string definition)
	{
		using RunspaceScope scope = _runspace.Enter();
		ScriptBlock function = FunctionScriptBlock.Create(definition);
		var attribute = new ValidateScriptVariableAttribute("_", "this", "psitem", "args[0]");

		Assert.Throws<ValidationMetadataException>(() => ArgumentValidator.Validate(attribute, function));
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

	// The message lists what this script block could use: the parameter that receives the element, as it's written, in
	// place of $args[1], or the $args index that holds the element after the parameters. A name that the param() block
	// declares appears only as a parameter.
	[Theory]
	[InlineData("$z", "$y, $right, $args[1]")]
	[InlineData("param($a, $b) $a -eq $a", "$y, $right, $b")]
	[InlineData("param([int] $a, [int] ${the right}) $a", "$y, $right, ${the right}")]
	[InlineData("param($a) $a.CompareTo($args[1])", "$y, $right, $args[0]")]
	[InlineData("param($a, $y) $a", "$right, $y")]
	public void Validate_ListsTheVariablesThatTheScriptBlockCanUseInTheMessage(string script, string variables)
	{
		var right = new ValidateScriptVariableAttribute("y", "right", "args[1]");

		var e = Assert.Throws<ValidationMetadataException>(() => ArgumentValidator.Validate(right, ScriptBlock.Create(script)));
		Assert.Equal($"The script block must use at least one of these variables: {variables}.", e.Message);
	}
}
