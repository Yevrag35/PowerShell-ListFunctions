$code = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;

using Comparer = System.Collections.Comparer;

namespace ListFunctions
{
    public class ScriptBlockComparer<T> : IComparer<T>, IEqualityComparer<T>
    {
        //private IComparer _comparer;
        private IComparer<T> _comparer;
        private IEqualityComparer<T> _equalityComparer;

        public ScriptBlock CompareScript { get; set; }
        public ScriptBlock EqualityTester { get; set; }
        public ScriptBlock HashCodeScript { get; set; }

        public ScriptBlockComparer()
        {
            if (typeof(T).Equals(typeof(string)))
            {
                var c = StringComparer.CurrentCultureIgnoreCase;
                _comparer = (IComparer<T>)c;
                _equalityComparer = (IEqualityComparer<T>)c;
            }
            else
            {
                _comparer = Comparer<T>.Default;
                _equalityComparer = EqualityComparer<T>.Default;
            }
        }
        //public ScriptBlockComparer(bool isCaseSensitive)
        //{
        //    if (isCaseSensitive)
        //    {
        //        _comparer = Comparer.Default;
        //    }
        //    else
        //    {
        //        _comparer = CaseInsensitiveComparer.Default;
        //    }
        //}

        //public IComparer GetDefaultComparer()
        //{
        //    return _comparer;
        //}

        public int Compare(T x, T y)
        {
            //if (this.CompareScript == null)
            if (null == this.CompareScript)
            {
                //return _comparer.Compare(x, y);
                return _comparer.Compare(x, y);
            }

            int answer = 1;
            try
            {
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
            if (null == this.EqualityTester)
            {
                //return x.Equals(y);
                return _equalityComparer.Equals(x, y);
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
            //if (this.HashCodeScript == null)
            if (null == this.HashCodeScript)
            {
                //return item.GetHashCode();
                return _equalityComparer.GetHashCode(item);
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

    public class SortedSetList<T> : SortedSet<T>
    {
        public T this[int index]
        {
            get
            {
                if (index < 0)
                {
                    index = base.Count + index;
                }

                return this.ElementAtOrDefault(index);
            }
        }

        public SortedSetList() : base() { }
        public SortedSetList(IComparer<T> comparer)
            : base(comparer)
        {
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
        'System.Linq',
        'System.Management.Automation'
    )
}
else {
    $atArgs.ReferencedAssemblies = @(
        "System",
        "System.Collections",
        "System.Collections.NonGeneric",
        "System.Linq",
        "System.Management.Automation",
        "System.Runtime.Extensions"
    )
}

Add-Type @atArgs