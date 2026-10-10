namespace ListFunctions.Internal;

/// <summary>
/// Specifies the outcome of an attempt to add an entry through a <see cref="DictionaryWrapper"/>.
/// </summary>
internal enum DictionaryAddResult
{
	/// <summary>
	/// The entry was added under a new key.
	/// </summary>
	Added,
	/// <summary>
	/// The key or the value couldn't be converted, so the dictionary didn't change.
	/// </summary>
	NotConverted,
	/// <summary>
	/// The dictionary refused the entry, and the exception went to <see cref="DictionaryWrapper.AddFailed"/>, so the
	/// dictionary didn't change.
	/// </summary>
	Failed,
	/// <summary>
	/// The key was already in the dictionary, and its value was kept.
	/// </summary>
	KeyExists,
	/// <summary>
	/// The key was already in the dictionary, and the value was added to the key's
	/// <see cref="ListFunctions.Modern.ObjectList"/>.
	/// </summary>
	Appended,
}
