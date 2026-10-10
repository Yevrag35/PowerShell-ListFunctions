using System.Management.Automation.Internal;
using ListFunctions.Extensions;

namespace ListFunctions.Engine.Tests.Extensions;

public sealed class PSObjectExtensionsTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public PSObjectExtensionsTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	[Fact]
	public void GetBaseObject_UnwrapsEveryLayer()
	{
		object value = new();
		var wrapped = new PSObject(new PSObject(value));

		Assert.Same(value, wrapped.GetBaseObject());
	}

	[Theory]
	[InlineData("[pscustomobject]@{ Name = 'a' }")]
	[InlineData("New-Object PSObject -Property @{ Name = 'a' }")]
	public void GetBaseObject_ReturnsACustomObjectAsItself(string script)
	{
		using RunspaceScope scope = _runspace.Enter();
		PSObject custom = ScriptBlock.Create(script).Invoke()[0];

		Assert.Same(custom, custom.GetBaseObject());
	}

	[Fact]
	public void GetBaseObject_StopsAtANestedCustomObject()
	{
		using RunspaceScope scope = _runspace.Enter();
		PSObject custom = ScriptBlock.Create("[pscustomobject]@{ Name = 'a' }").Invoke()[0];

		Assert.Same(custom, new PSObject(custom).GetBaseObject());
	}

	[Fact]
	public void GetBaseObject_ReturnsAutomationNullAsItself()
	{
		Assert.Same(AutomationNull.Value, AutomationNull.Value.GetBaseObject());
	}

	// Deserializing an object whose type PowerShell doesn't rehydrate gives a property bag: a PSObject with no base object.
	[Theory]
	[InlineData("[pscustomobject]@{ Name = 'a' }")]
	[InlineData("[System.Collections.Generic.KeyValuePair[string, int]]::new('a', 1)")]
	public void GetBaseObject_ReturnsADeserializedPropertyBagAsItself(string script)
	{
		using RunspaceScope scope = _runspace.Enter();
		PSObject original = ScriptBlock.Create(script).Invoke()[0];
		var bag = Assert.IsType<PSObject>(PSSerializer.Deserialize(PSSerializer.Serialize(original)));

		Assert.Same(bag, bag.GetBaseObject());
	}

	// A deserialized string that has a note property is a PSObject whose base object is the string, not a property bag.
	[Fact]
	public void GetBaseObject_UnwrapsADeserializedValue()
	{
		using RunspaceScope scope = _runspace.Enter();
		PSObject original = ScriptBlock.Create("Add-Member -InputObject 'a' -NotePropertyName Note -NotePropertyValue 1 -PassThru").Invoke()[0];
		var deserialized = Assert.IsType<PSObject>(PSSerializer.Deserialize(PSSerializer.Serialize(original)));

		Assert.Equal("a", deserialized.GetBaseObject());
	}
}
