namespace ListFunctions.Extensions;

public static class TypeExtensions
{
	[DebuggerStepThrough]
	public static string GetTypeName(this Type type)
	{
		return type.FullName ?? type.Name;
	}
}
