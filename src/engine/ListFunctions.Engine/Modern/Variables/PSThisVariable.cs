using ListFunctions.Extensions;

namespace ListFunctions.Modern.Variables;

/// <summary>
/// Represents the automatic variables <c>$_</c>, <c>$this</c>, and <c>$PSItem</c> that a script block uses to refer to
/// the current object.
/// </summary>
/// <remarks>
/// <para>
/// All three variables hold the same value. The instance creates its <see cref="PSVariable"/> objects once and reuses
/// them, so setting a new value doesn't allocate new variables.
/// </para>
/// <para>
/// Instances aren't thread-safe. <see cref="SetValue(object)"/> changes the variables that earlier calls to
/// <see cref="InsertIntoList(List{PSVariable})"/> put into other lists.
/// </para>
/// </remarks>
internal sealed class PSThisVariable : ICloneable
{
	/// <summary>
	/// The name of the <c>$_</c> variable.
	/// </summary>
	internal const string Underscore = "_";
	/// <summary>
	/// The name of the <c>$this</c> variable.
	/// </summary>
	internal const string This = "this";
	/// <summary>
	/// The name of the <c>$PSItem</c> variable.
	/// </summary>
	internal const string PSItem = "psitem";
	/// <summary>
	/// The name that script block validation accepts for the first positional argument, <c>$args[0]</c>.
	/// </summary>
	/// <remarks>
	/// This class doesn't define a variable with this name. The name exists for validation attributes that list the
	/// variables a script block may use.
	/// </remarks>
	internal const string FirstArg = "args[0]";
	/// <summary>
	/// The name that script block validation accepts for the second positional argument, <c>$args[1]</c>.
	/// </summary>
	/// <remarks>
	/// This class doesn't define a variable with this name. The name exists for validation attributes that list the
	/// variables a script block may use.
	/// </remarks>
	internal const string SecondArg = "args[1]";

#if NET10_0_OR_GREATER
	private InlineArray3<PSVariable> _variables;
#else
	private PSVariable[]? _variables;
#endif
	/// <summary>
	/// Gets the value of the variables.
	/// </summary>
	/// <value>The current object, or <see langword="null"/> when no value has been set.</value>
	public object? ObjValue { get; private set; }
	/// <summary>
	/// Initializes a new <see cref="PSThisVariable"/> instance with a <see langword="null"/> value.
	/// </summary>
	public PSThisVariable()
	{
	}
	/// <summary>
	/// Initializes a new <see cref="PSThisVariable"/> instance with a copy of the value of the specified instance.
	/// </summary>
	/// <remarks>
	/// The value is cloned when it supports cloning, such as an <see cref="ICloneable"/> or a <see cref="PSObject"/>.
	/// Otherwise, the new instance shares the value. The variables themselves aren't copied; the new instance creates
	/// its own when it first needs them.
	/// </remarks>
	/// <param name="other">The instance to copy.</param>
	private PSThisVariable(PSThisVariable other)
	{
		this.ObjValue = other.ObjValue.CloneIf();
	}

	/// <summary>
	/// Creates a copy of this instance.
	/// </summary>
	/// <remarks>
	/// The copy has its own variables. Its value is a clone of <see cref="ObjValue"/> when the value supports cloning,
	/// such as an <see cref="ICloneable"/> or a <see cref="PSObject"/>; otherwise, both instances share the value.
	/// </remarks>
	/// <returns>A new <see cref="PSThisVariable"/> with the same value as this instance.</returns>
	public PSThisVariable Clone()
	{
		return new(this);
	}
	/// <summary>
	/// Creates a copy of this instance.
	/// </summary>
	/// <returns>A new <see cref="PSThisVariable"/>, returned as an <see cref="object"/>.</returns>
	[DebuggerStepThrough]
	object ICloneable.Clone()
	{
		return this.Clone();
	}

	/// <summary>
	/// Inserts the <c>$_</c>, <c>$this</c>, and <c>$PSItem</c> variables at the start of the specified list.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The variables hold <see cref="ObjValue"/>. The method inserts the same <see cref="PSVariable"/> instances on
	/// every call, so clear the list between invocations, or it collects duplicates.
	/// </para>
	/// <para>
	/// The order of the three variables isn't guaranteed.
	/// </para>
	/// </remarks>
	/// <param name="list">The list to insert the variables into. This value must not be <see langword="null"/>.</param>
	public void InsertIntoList(List<PSVariable> list)
	{
		list.InsertRange(0, InitializeArray(ref _variables, this.ObjValue));
	}

	/// <summary>
	/// Sets the value of the <c>$_</c>, <c>$this</c>, and <c>$PSItem</c> variables.
	/// </summary>
	/// <remarks>
	/// The new value also appears in any list that already holds these variables.
	/// </remarks>
	/// <param name="value">The current object, or <see langword="null"/>.</param>
	public void SetValue(object? value)
	{
		this.ObjValue = value;
		_ = InitializeArray(ref _variables, value);
	}

#if NET10_0_OR_GREATER
	/// <summary>
	/// Creates the three variables, or sets the value of the existing ones, and returns them.
	/// </summary>
	/// <remarks>
	/// The method creates new variables when <paramref name="array"/> holds a <see langword="null"/> element, as it does
	/// before the first call. Otherwise, it updates the existing variables and sorts them into the order <c>psitem</c>,
	/// <c>this</c>, <c>_</c>. New variables aren't sorted, so they keep the order <c>_</c>, <c>this</c>, <c>psitem</c>.
	/// </remarks>
	/// <param name="array">The inline array of variables to fill or reuse.</param>
	/// <param name="value">The value to assign to every variable.</param>
	/// <returns>A span over the variables in <paramref name="array"/>.</returns>
	private static Span<PSVariable> InitializeArray(ref InlineArray3<PSVariable> array, object? value)
	{
		foreach (PSVariable v in array)
		{
			if (v is null)
			{
				array[0] = new(Underscore, value);
				array[1] = new(This, value);
				array[2] = new(PSItem, value);
				return array;
			}

			v.Value = value;
		}

		((Span<PSVariable>)array).Sort(VariableComparer.Shared);
		return array;
	}
#else
	/// <summary>
	/// Creates the three variables, or sets the value of the existing ones, and returns them.
	/// </summary>
	/// <remarks>
	/// The method creates a new array when <paramref name="array"/> is <see langword="null"/>, doesn't hold exactly three
	/// variables, or holds a <see langword="null"/> element. Otherwise, it updates the existing variables and sorts
	/// them into the order <c>psitem</c>, <c>this</c>, <c>_</c>. A new array isn't sorted, so it keeps the order
	/// <c>_</c>, <c>this</c>, <c>psitem</c>.
	/// </remarks>
	/// <param name="array">The array of variables to reuse, or <see langword="null"/> to create one.</param>
	/// <param name="value">The value to assign to every variable.</param>
	/// <returns>The array of variables, which is also stored in <paramref name="array"/>.</returns>
	private static PSVariable[] InitializeArray([NotNull] ref PSVariable[]? array, object? value)
	{
		if (array is null)
		{
			array =
			[
				new(Underscore, value),
				new(This, value),
				new(PSItem, value),
			];

			return array;
		}

		if (array.Length != 3)
		{
			array = null;
			return InitializeArray(ref array, value);
		}

		for (int i = 0; i < array.Length; i++)
		{
			PSVariable v = array[i];
			if (v is null)
			{
				array = null;
				return InitializeArray(ref array, value);
			}

			v.Value = value;
		}

		Array.Sort(array, VariableComparer.Shared);
		return array;
	}
#endif

	/// <summary>
	/// Orders variables so that <c>$_</c>, <c>$this</c>, and <c>$PSItem</c> come after every other variable.
	/// </summary>
	/// <remarks>
	/// Other variables sort among themselves by ordinal name, followed by <c>psitem</c>, <c>this</c>, and <c>_</c>, in
	/// that order. A <see langword="null"/> variable sorts first.
	/// </remarks>
	private sealed class VariableComparer : IComparer<PSVariable>
	{
		/// <summary>
		/// The shared instance of the comparer.
		/// </summary>
		internal static readonly VariableComparer Shared = new();

		/// <summary>
		/// Compares two variables by name and returns a value that indicates their relative order.
		/// </summary>
		/// <param name="x">The first variable to compare, or <see langword="null"/>.</param>
		/// <param name="y">The second variable to compare, or <see langword="null"/>.</param>
		/// <returns>
		/// A negative number if <paramref name="x"/> sorts before <paramref name="y"/>, 0 if they sort the same, or a
		/// positive number if <paramref name="x"/> sorts after <paramref name="y"/>.
		/// </returns>
		public int Compare(PSVariable? x, PSVariable? y)
		{
			if (ReferenceEquals(x, y)) return 0;
			if (x is null) return -1;
			if (y is null) return 1;

			return x.Name switch
			{
				Underscore => Underscore.Equals(y.Name) ? 0 : 1,
				This => This.Equals(y.Name, StringComparison.OrdinalIgnoreCase)
										? 0
										: Underscore.Equals(y.Name) ? -1 : 1,
				PSItem => PSItem.Equals(y.Name, StringComparison.OrdinalIgnoreCase)
										? 0
										: (Underscore.Equals(y.Name) || This.Equals(y.Name, StringComparison.OrdinalIgnoreCase)) ? -1 : 1,
				_ => Underscore.Equals(y.Name) || This.Equals(y.Name, StringComparison.OrdinalIgnoreCase) || PSItem.Equals(y.Name, StringComparison.OrdinalIgnoreCase)
										? -1
										: string.CompareOrdinal(x.Name, y.Name),
			};
		}
	}
}
