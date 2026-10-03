using System.Text.RegularExpressions;

#nullable enable

namespace ListFunctions.Extensions;

/// <summary>
/// Provides extension methods that rewrite the variable references in a <see cref="ScriptBlock"/>'s text.
/// </summary>
public static partial class ScriptBlockVariableExtensions
{
	/// <summary>
	/// Returns a <see cref="ScriptBlock"/> in which every reference to <c>$_</c>, <c>$PSItem</c>, or <c>$this</c> reads
	/// <c>$args[0]</c> instead.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method rewrites the script block's text with a case-insensitive regular expression; it doesn't parse the
	/// script. As a result:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// It replaces a reference only when whitespace, <c>)</c>, <c>"</c>, <c>;</c>, <c>.</c>, <c>,</c>, <c>'</c>,
	/// <c>#</c>, or the end of the script follows it. For example, it leaves <c>$_]</c>, <c>$_}</c>, and <c>$_|</c>
	/// unchanged, and it never matches the braced form <c>${_}</c>.
	/// </description></item>
	/// <item><description>
	/// It also replaces matches inside string literals, comments, and nested script blocks, where <c>$_</c> or
	/// <c>$this</c> can refer to a different object.
	/// </description></item>
	/// </list>
	/// <para>
	/// The new script block is created from text with <see cref="ScriptBlock.Create(string)"/>, so it isn't bound to the
	/// session state or module of <paramref name="scriptBlock"/>.
	/// </para>
	/// </remarks>
	/// <param name="scriptBlock">The script block to rewrite. This value must not be <see langword="null"/>.</param>
	/// <returns>
	/// A new <see cref="ScriptBlock"/> with the references replaced if the method replaced any; otherwise,
	/// <paramref name="scriptBlock"/> itself.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="scriptBlock"/> is null.</exception>
	/// <exception cref="ParseException">Thrown when the rewritten text isn't a valid script.</exception>
	public static ScriptBlock ReplaceWithArgsZero(this ScriptBlock scriptBlock)
	{
		Guard.NotNull(scriptBlock);

		string script = scriptBlock.ToString();
		string newScript = ReplaceString(script);

		return !string.Equals(script, newScript, StringComparison.OrdinalIgnoreCase)
			? ScriptBlock.Create(newScript)
			: scriptBlock;
	}

	/// <summary>
	/// Replaces every match of <c>$_</c>, <c>$PSItem</c>, or <c>$this</c> in the script text with <c>$args[0]</c>.
	/// </summary>
	/// <remarks>
	/// The character that follows each reference, which the pattern captures as group 1, stays in place after
	/// <c>$args[0]</c>. On .NET Framework and .NET Standard targets, the method uses a cached, compiled
	/// <see cref="Regex"/>; on .NET targets, it uses the source-generated <c>ReplaceDefaultNames</c> pattern.
	/// </remarks>
	/// <param name="script">The script text to rewrite. This value must not be <see langword="null"/>.</param>
	/// <returns>The rewritten text, or text equal to <paramref name="script"/> if nothing matches.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="script"/> is null.</exception>
	private static string ReplaceString(string script)
	{
#if !NETCOREAPP
		return Regex.Replace(
			script,
			@"\$(?:(?:_|PSItem|this)(\s|\)|\""|\;|$|\.|\,|\'|\#))",
			"$args[0]$1",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);
	}
#else
		return ReplaceDefaultNames().Replace(script, "$args[0]$1");
	}

	/// <summary>
	/// Gets the source-generated, case-insensitive <see cref="Regex"/> that matches <c>$_</c>, <c>$PSItem</c>, and
	/// <c>$this</c> references.
	/// </summary>
	/// <remarks>
	/// Group 1 captures the character that ends each reference, or nothing at the end of the input.
	/// </remarks>
	/// <returns>The cached <see cref="Regex"/> instance.</returns>
	[GeneratedRegex(@"\$(?:(?:_|PSItem|this)(\s|\)|\""|\;|$|\.|\,|\'|\#))", RegexOptions.IgnoreCase, "en-US")]
	private static partial Regex ReplaceDefaultNames();
#endif
}
