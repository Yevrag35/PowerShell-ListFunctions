using ListFunctions.Extensions;

namespace ListFunctions.Modern;

/// <summary>
/// Provides the base class for types that turn a PowerShell script block into comparison logic.
/// </summary>
/// <remarks>
/// <para>
/// A derived type validates its script block when it's constructed, unless the caller has already validated it. A
/// valid script block is one that PowerShell can invoke as a single block that contains at least one statement: the
/// <c>process</c> block when there is one, and otherwise the <c>end</c> block. A script block that has a <c>begin</c>
/// block, a <c>clean</c> block, or both a <c>process</c> block and an <c>end</c> block isn't valid.
/// </para>
/// <para>
/// Only types in this assembly can derive from this class.
/// </para>
/// </remarks>
public abstract class ComparingBase
{
	/// <summary>
	/// Gets the script block that holds the comparison logic.
	/// </summary>
	/// <value>The script block passed to the constructor. It's never <see langword="null"/>.</value>
	public ScriptBlock Script { get; }

	/// <summary>
	/// Initializes a new <see cref="ComparingBase"/> instance with the specified script block, and validates the script
	/// block unless <paramref name="preValidated"/> is <see langword="true"/>.
	/// </summary>
	/// <remarks>
	/// When <paramref name="preValidated"/> is <see langword="true"/>, the constructor checks only that
	/// <paramref name="scriptBlock"/> isn't <see langword="null"/>. Otherwise, it also checks that the script block is
	/// valid, as the class remarks describe.
	/// </remarks>
	/// <param name="scriptBlock">The script block that holds the comparison logic. This value must not be <see langword="null"/>.</param>
	/// <param name="preValidated"><see langword="true"/> if the caller has already validated <paramref name="scriptBlock"/>; otherwise, <see langword="false"/>.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="scriptBlock"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="preValidated"/> is false and <paramref name="scriptBlock"/> has no statements to run, or has a <c>begin</c> block, a <c>clean</c> block, or both a <c>process</c> block and an <c>end</c> block.</exception>
	private protected ComparingBase(ScriptBlock scriptBlock, bool preValidated)
	{
		if (!preValidated)
		{
			ValidateScriptBlock(scriptBlock);
		}
		else
		{
			ArgumentNullException.ThrowIfNull(scriptBlock);
		}

		this.Script = scriptBlock;
	}

	/// <summary>
	/// Checks that the specified script block isn't <see langword="null"/> and that PowerShell can invoke it as a single
	/// block that contains at least one statement.
	/// </summary>
	/// <remarks>
	/// The block that runs is the <c>process</c> block when there is one, and otherwise the <c>end</c> block. A script block
	/// that has a <c>begin</c> block, a <c>clean</c> block, or both a <c>process</c> block and an <c>end</c> block fails.
	/// </remarks>
	/// <param name="scriptBlock">The script block to validate.</param>
	/// <param name="paramName">The name of the caller's argument, which the compiler supplies.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="scriptBlock"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="scriptBlock"/> has no statements to run, or has a <c>begin</c> block, a <c>clean</c> block, or both a <c>process</c> block and an <c>end</c> block.</exception>
	private static void ValidateScriptBlock(ScriptBlock scriptBlock, [CallerArgumentExpression(nameof(scriptBlock))] string? paramName = null)
	{
		if (!scriptBlock.IsProperScriptBlock())
		{
			paramName ??= nameof(scriptBlock);
			throw new ArgumentException(ScriptBlockExtensions.ImproperScriptBlockMessage, paramName);
		}
	}
}
