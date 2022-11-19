using ListFunctions.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;

namespace ListFunctions
{
    public sealed class ScriptBlockEquality
    {
        private readonly ScriptBlock _scriptBlock;

        public ScriptBlockEquality(ScriptBlock scriptBlock)
        {
            _scriptBlock = scriptBlock;
        }

        public bool Any(IEnumerable<object?>? collection)
        {
            if (collection is null)
                return false;

            return collection.Any(obj => this.IsTrue(obj));
        }

        public bool All(IEnumerable? collection)
        {
            if (collection is null)
                return false;

            bool result = true;

            foreach (object? item in collection)
            {
                if (!this.IsTrue(item))
                {
                    result = false;
                    break;
                }
            }

            return result;
        }

        public bool IsTrue(object? value)
        {
            var varContext = new List<PSVariable>()
            {
                new PSVariable("_", value)
            };

            Collection<PSObject> results = _scriptBlock.InvokeWithContext(null, varContext);

            return results.GetFirstValue(x => Convert.ToBoolean(x));
        }

        public static ScriptBlockEquality Create(ScriptBlock scriptBlock)
        {
            return new ScriptBlockEquality(scriptBlock);
        }
    }
}
