namespace ListFunctions
{
    /// <summary>
    /// Specifies the behavior to use when a duplicate key is encountered during an operation that adds or merges
    /// key-value pairs.
    /// </summary>
    /// <remarks>Use this enumeration to control how duplicate keys are handled in scenarios such as merging
    /// dictionaries or adding items to a collection that enforces unique keys. The available options allow you to
    /// choose whether to throw an error, skip the duplicate, or concatenate the values associated with the duplicate
    /// key.</remarks>
    public enum DuplicateKeyBehavior
    {
        /// <summary>
        /// The default option. Indicates that an exception should be thrown when a duplicate key is encountered.
        /// </summary>
        Error,
        /// <summary>
        /// Indicates that the duplicate key should be skipped.
        /// </summary>
        Skip,
        /// <summary>
        /// Indicates that the values associated with the duplicate key should be concatenated.
        /// </summary>
        /// <remarks>
        /// When this option is selected, the values corresponding to the duplicate key will be combined into a single
        /// collection value. Specified value types are ignored.
        /// </remarks>
        Concatenate,
    }
}

