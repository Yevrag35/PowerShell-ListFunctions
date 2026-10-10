using ListFunctions.Validation;

namespace ListFunctions.Engine.Tests.Validation;

public sealed class ArgumentToTypeTransformAttributeTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public ArgumentToTypeTransformAttributeTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	[Fact]
	public void Transform_ConvertsNullToObject()
	{
		using RunspaceScope scope = _runspace.Enter();
		var attribute = new ArgumentToTypeTransformAttribute();

		Assert.Equal(typeof(object), attribute.Transform(_runspace.EngineIntrinsics, inputData: null));
	}

	[Fact]
	public void Transform_ReturnsATypeUnchanged()
	{
		using RunspaceScope scope = _runspace.Enter();
		var attribute = new ArgumentToTypeTransformAttribute();

		Assert.Same(typeof(Guid), attribute.Transform(_runspace.EngineIntrinsics, typeof(Guid)));
	}

	// An unquoted argument, such as the [guid] in New-List [guid], reaches the attribute as a string.
	[Theory]
	[InlineData("[guid]", typeof(Guid))]
	[InlineData("guid", typeof(Guid))]
	[InlineData("System.String", typeof(string))]
	[InlineData("System.String, mscorlib", typeof(string))]
	[InlineData("int[]", typeof(int[]))]
	[InlineData("System.Collections.Generic.List[int]", typeof(List<int>))]
	[InlineData("System.Collections.Generic.KeyValuePair[string, int]", typeof(KeyValuePair<string, int>))]
	[InlineData("[System.Collections.Generic.KeyValuePair[string, int]]", typeof(KeyValuePair<string, int>))]
	public void Transform_ResolvesATypeName(string typeName, Type expected)
	{
		using RunspaceScope scope = _runspace.Enter();
		var attribute = new ArgumentToTypeTransformAttribute();

		Assert.Equal(expected, attribute.Transform(_runspace.EngineIntrinsics, typeName));
	}

	// Windows PowerShell 5.1's [type] conversion ignores whatever follows a type name, so it turns
	// 'System.String bad text' into [string]. The attribute rejects it in both editions.
	[Theory]
	[InlineData("NotAType")]
	[InlineData("[NotAType]")]
	[InlineData("System.String bad text")]
	[InlineData("")]
	public void Transform_ThrowsForATypeNameThatDoesNotNameAType(string typeName)
	{
		using RunspaceScope scope = _runspace.Enter();
		var attribute = new ArgumentToTypeTransformAttribute();

		var e = Assert.Throws<ArgumentException>(() => attribute.Transform(_runspace.EngineIntrinsics, typeName));
		Assert.Equal($"'{typeName}' is not a valid .NET or custom-defined type.", e.Message);
	}

	// The type literal in the nested script block { [int] } doesn't count.
	[Theory]
	[InlineData("[guid]")]
	[InlineData("{ [int] }; [guid]")]
	public void Transform_ResolvesTheTypeLiteralInAScriptBlock(string script)
	{
		using RunspaceScope scope = _runspace.Enter();
		var attribute = new ArgumentToTypeTransformAttribute();

		Assert.Equal(typeof(Guid), attribute.Transform(_runspace.EngineIntrinsics, ScriptBlock.Create(script)));
	}

	// A cast, such as [guid]$x, isn't a type literal, and neither is one in a nested script block.
	[Theory]
	[InlineData("'guid'")]
	[InlineData("[guid]$x")]
	[InlineData("{ [guid] }")]
	[InlineData("[NotAType]")]
	public void Transform_ThrowsForAScriptBlockThatDoesNotNameAType(string script)
	{
		using RunspaceScope scope = _runspace.Enter();
		var attribute = new ArgumentToTypeTransformAttribute();

		var e = Assert.Throws<ArgumentException>(() => attribute.Transform(_runspace.EngineIntrinsics, ScriptBlock.Create(script)));
		Assert.Equal($"'{script}' is not a valid .NET or custom-defined type.", e.Message);
	}

	[Fact]
	public void Transform_UnwrapsAPSObjectFirst()
	{
		using RunspaceScope scope = _runspace.Enter();
		var attribute = new ArgumentToTypeTransformAttribute();

		Assert.Equal(typeof(Guid), attribute.Transform(_runspace.EngineIntrinsics, PSObject.AsPSObject(typeof(Guid))));
		Assert.Equal(typeof(Guid), attribute.Transform(_runspace.EngineIntrinsics, PSObject.AsPSObject("[guid]")));
		Assert.Equal(typeof(Guid), attribute.Transform(_runspace.EngineIntrinsics, PSObject.AsPSObject(ScriptBlock.Create("[guid]"))));
	}

	// PowerShell splits an unquoted argument at each comma, and drops the white space after each one, so the type literal
	// [System.Collections.Generic.KeyValuePair[string, int]] reaches the attribute as the strings
	// '[System.Collections.Generic.KeyValuePair[string' and 'int]]'.
	[Theory]
	[InlineData("[System.Collections.Generic.KeyValuePair[string,int]]", typeof(KeyValuePair<string, int>))]
	[InlineData("System.Collections.Generic.KeyValuePair[string,int]", typeof(KeyValuePair<string, int>))]
	[InlineData("[System.Collections.Generic.Dictionary[string,System.Collections.Generic.KeyValuePair[int,string]]]", typeof(Dictionary<string, KeyValuePair<int, string>>))]
	public void Transform_JoinsTheStringsOfAnArrayIntoOneTypeName(string typeLiteral, Type expected)
	{
		using RunspaceScope scope = _runspace.Enter();
		var attribute = new ArgumentToTypeTransformAttribute();
		object[] parts = [.. typeLiteral.Split(',')];

		Assert.Equal(expected, attribute.Transform(_runspace.EngineIntrinsics, parts));
	}

	[Fact]
	public void Transform_UnwrapsEachStringOfAnArray()
	{
		using RunspaceScope scope = _runspace.Enter();
		var attribute = new ArgumentToTypeTransformAttribute();
		object[] parts = [PSObject.AsPSObject("[System.Collections.Generic.KeyValuePair[string"), PSObject.AsPSObject("int]]")];

		Assert.Equal(typeof(KeyValuePair<string, int>), attribute.Transform(_runspace.EngineIntrinsics, parts));
	}

	// New-Dictionary [string],[int] passes the strings '[string]' and '[int]', which name two types, not one.
	[Fact]
	public void Transform_ThrowsForAnArrayOfStringsThatIsNotOneTypeName()
	{
		using RunspaceScope scope = _runspace.Enter();
		var attribute = new ArgumentToTypeTransformAttribute();
		object[] parts = ["[string]", "[int]"];

		var e = Assert.Throws<ArgumentException>(() => attribute.Transform(_runspace.EngineIntrinsics, parts));
		Assert.Equal("'[string],[int]' is not a valid .NET or custom-defined type.", e.Message);
	}

	[Fact]
	public void Transform_ThrowsForAnArrayThatIsEmptyOrHoldsAnythingButStrings()
	{
		using RunspaceScope scope = _runspace.Enter();
		var attribute = new ArgumentToTypeTransformAttribute();
		const string expected = "Cannot convert a value of type 'System.Object[]' to a type. Pass a type, a type name, or a script block that contains a type literal.";
		object[] withType = [typeof(string), "[int]"];

		Assert.Equal(expected, Assert.Throws<ArgumentException>(() => attribute.Transform(_runspace.EngineIntrinsics, withType)).Message);
		Assert.Equal(expected, Assert.Throws<ArgumentException>(() => attribute.Transform(_runspace.EngineIntrinsics, Array.Empty<object>())).Message);
	}

	[Fact]
	public void Transform_ThrowsForAValueOfAnotherType()
	{
		using RunspaceScope scope = _runspace.Enter();
		var attribute = new ArgumentToTypeTransformAttribute();

		var e = Assert.Throws<ArgumentException>(() => attribute.Transform(_runspace.EngineIntrinsics, 5));
		Assert.Equal("Cannot convert a value of type 'System.Int32' to a type. Pass a type, a type name, or a script block that contains a type literal.", e.Message);
	}

	[Theory]
	[InlineData("PSReadLine")]
	[InlineData("psreadline")]
	public void Transform_ReturnsObjectInsteadOfThrowingWhenPSReadLineCalls(string moduleName)
	{
		using RunspaceScope scope = _runspace.Enter();

		Assert.Equal(typeof(object), this.TransformInModule(moduleName, "NotAType"));
		Assert.Equal(typeof(object), this.TransformInModule(moduleName, ScriptBlock.Create("'NotAType'")));
		Assert.Equal(typeof(object), this.TransformInModule(moduleName, new object[] { "[string]", "[int]" }));
		Assert.Equal(typeof(object), this.TransformInModule(moduleName, 5));
	}

	[Fact]
	public void Transform_ThrowsWhenAnotherModuleCalls()
	{
		using RunspaceScope scope = _runspace.Enter();

		var e = Assert.Throws<MethodInvocationException>(() => this.TransformInModule("PSReadLineHelper", "NotAType"));
		Assert.IsType<ArgumentException>(e.InnerException);
	}

	[Fact]
	public void ToString_ReturnsTheNameAsItIsWrittenOnAParameter()
	{
		Assert.Equal("[ArgumentToTypeTransform]", new ArgumentToTypeTransformAttribute().ToString());
	}

	/// <summary>
	/// Calls <see cref="ArgumentToTypeTransformAttribute.Transform(EngineIntrinsics, object)"/> from a new dynamic module with
	/// the specified name, the way PowerShell calls it when a command in that module binds a parameter.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method runs a script in the module, and the script calls a delegate that calls the attribute. While the script
	/// runs, the engine intrinsics return the module's session state, so the attribute finds the module as the calling
	/// module.
	/// </para>
	/// <para>
	/// The current thread must be in the fixture's runspace, through <see cref="RunspaceFixture.Enter"/>.
	/// </para>
	/// </remarks>
	/// <param name="moduleName">The name of the module to create.</param>
	/// <param name="inputData">The argument to convert. This value can be <see langword="null"/>.</param>
	/// <returns>The value that the attribute returns.</returns>
	/// <exception cref="MethodInvocationException">Thrown when the attribute throws. Its InnerException is the exception that the attribute threw.</exception>
	private object? TransformInModule(string moduleName, object? inputData)
	{
		var attribute = new ArgumentToTypeTransformAttribute();
		EngineIntrinsics engineIntrinsics = _runspace.EngineIntrinsics;
		Func<object?> transform = () => attribute.Transform(engineIntrinsics, inputData);
		var script = ScriptBlock.Create("param($Name, $Transform) & (New-Module -Name $Name -ScriptBlock { }) { param($t) $t.Invoke() } $Transform");

		return script.Invoke(moduleName, transform)[0].BaseObject;
	}
}
