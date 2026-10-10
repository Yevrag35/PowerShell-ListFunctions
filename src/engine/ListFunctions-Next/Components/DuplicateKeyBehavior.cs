using ListFunctions.Cmdlets.Constructs;
using ListFunctions.Modern;

namespace ListFunctions.Components;

/// <summary>
/// Specifies what <see cref="ConvertToDictionaryCmdlet"/> does when an input object produces a key
/// that is already in the dictionary.
/// </summary>
/// <remarks>
/// None of the options stops the cmdlet. Each one handles the duplicate and moves on to the next input object.
/// </remarks>
public enum DuplicateKeyBehavior
{
	/// <summary>
	/// Writes a non-terminating error for the duplicate and keeps the existing value. This is the default.
	/// </summary>
	Error,
	/// <summary>
	/// Writes a warning and keeps the existing value.
	/// </summary>
	Skip,
	/// <summary>
	/// Collects every value for the key in an <see cref="ObjectList"/>.
	/// </summary>
	/// <remarks>
	/// The first duplicate replaces the existing value with an <see cref="ObjectList"/> that holds both values, and
	/// later duplicates are appended to it. A key that never repeats keeps its single value. With this option, the
	/// dictionary's value type is always <see cref="object"/>, and a requested value type other than
	/// <see cref="object"/> is ignored with a warning.
	/// </remarks>
	Concatenate,
}