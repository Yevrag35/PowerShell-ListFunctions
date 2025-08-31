using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace ListFunctions.Legacy
{
    internal class ComparingContext
    {
        internal const string VAR_X = "x";
        internal const string VAR_Y = "y";

        private readonly List<PSVariable> _list;
        private readonly Dictionary<string, PSVariable> _dict;

        internal int Count => _list.Count;

        internal PSVariable this[string name] => _dict[name];
        internal PSVariable this[int index] => _list[index];

        internal ComparingContext()
        {
            var first = GetBlankVariable(VAR_X);
            var second = GetBlankVariable(VAR_Y);
            _list = new List<PSVariable>(2)
            {
                first, second
            };

            _dict = new Dictionary<string, PSVariable>(2, StringComparer.CurrentCultureIgnoreCase)
            {
                { first.Name, first },
                { second.Name, second }
            };
        }

        internal List<PSVariable> GetList() => _list;

        private static PSVariable GetBlankVariable(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                throw new ArgumentNullException(nameof(variableName));

            return new PSVariable(variableName, null);
        }
    }
}