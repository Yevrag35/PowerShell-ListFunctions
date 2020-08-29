$code = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;

using Comparer = System.Collections.Comparer;

namespace ListFunctions
{
    public class ScriptBlockComparer<T> : IComparer<T>, IEqualityComparer<T>
    {
        private IComparer _comparer;

        public ScriptBlock CompareScript { get; set; }
        public ScriptBlock EqualityTester { get; set; }
        public ScriptBlock HashCodeScript { get; set; }

        public ScriptBlockComparer() : this(false) { }
        public ScriptBlockComparer(bool isCaseSensitive)
        {
            if (isCaseSensitive)
            {
                _comparer = Comparer.Default;
            }
            else
            {
                _comparer = CaseInsensitiveComparer.Default;
            }
        }

        public IComparer GetDefaultComparer()
        {
            return _comparer;
        }

        public int Compare(T x, T y)
        {
            if (this.CompareScript == null)
            {
                return _comparer.Compare(x, y);
            }

            int answer = 0;
            try {
                foreach (PSObject pso in this.CompareScript.Invoke(x, y))
                {
                    if (pso == null)
                    {
                        continue;
                    }

                    int? maybe = pso.ImmediateBaseObject as int?;
                    if (maybe.HasValue && (maybe.Value == 0 || maybe.Value == -1 || maybe.Value == 1))
                    {
                        answer = maybe.Value;
                        break;
                    }
                }
            }
            catch {}
            return answer;
        }

        public bool Equals(T x, T y)
        {
            if (this.EqualityTester == null)
            {
                return x.Equals(y);
            }

            bool answer = false;
            foreach (PSObject pso in this.EqualityTester.Invoke(x, y))
            {
                bool? maybe = pso.ImmediateBaseObject as bool?;
                if (maybe.HasValue && maybe.Value)
                {
                    answer = true;
                    break;
                }
            }
            return answer;
        }
        public int GetHashCode(T item)
        {
            if (this.HashCodeScript == null)
            {
                return item.GetHashCode();
            }

            foreach (PSObject pso in this.HashCodeScript.Invoke(item))
            {
                int? maybe = pso.ImmediateBaseObject as int?;
                if (maybe.HasValue)
                {
                    return maybe.Value;
                }
            }
            return item.GetHashCode();
        }
    }
}
"@

$atArgs = @{
    TypeDefinition = $code
    Language       = "CSharp"
}
if ($PSVersionTable.PSVersion.Major -le 5) {

    $atArgs.ReferencedAssemblies = @(
        'System',
        'System.Collections',
        'System.Management.Automation'
    )
}
else {
    $atArgs.ReferencedAssemblies = @(
        "System", 
        "System.Collections",
        "System.Collections.NonGeneric",
        "System.Console",
        "System.Management.Automation",
        "System.Runtime.Extensions"
    )
}

Add-Type @atArgs