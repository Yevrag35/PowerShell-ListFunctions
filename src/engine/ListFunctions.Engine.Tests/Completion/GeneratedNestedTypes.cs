namespace ListFunctions.Engine.Tests.Completion;

/// <summary>
/// Provides an extension block, so that the test assembly holds public types whose names PowerShell can't read as type
/// names.
/// </summary>
/// <remarks>
/// For an extension block, the compiler generates public nested types whose names start with <c>&lt;G&gt;$</c> and
/// <c>&lt;M&gt;$</c>. PowerShell's completion of a type literal offers them after this class's full name and a plus sign.
/// </remarks>
public static class GeneratedNestedTypes
{
	/// <summary>
	/// Provides a property for <see cref="string"/>, which makes the compiler generate the nested types.
	/// </summary>
	/// <param name="text">The string that the property extends.</param>
	extension(string text)
	{
		/// <summary>
		/// Gets a value that indicates whether <paramref name="text"/> is empty.
		/// </summary>
		/// <value><see langword="true"/> if <paramref name="text"/> is empty; otherwise, <see langword="false"/>.</value>
		public bool IsEmptyText => text.Length == 0;
	}
}
