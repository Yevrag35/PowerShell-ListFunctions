$code = @"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;

namespace ListFunctions
{
    public class ScriptBlockComparer<T> : IEqualityComparer<T>
    {
        public ScriptBlock EqualityTester { get; set; }
        public ScriptBlock HashCodeScript { get; set; }

        public ScriptBlockComparer() { }
        public ScriptBlockComparer(ScriptBlock tester)
        {
            this.EqualityTester = tester;
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
    Language = "CSharp"
    ReferencedAssemblies = @(
        "System", 
        "System.Collections",
        "System.Management.Automation", 
        "System.Linq"
    )
}

Add-Type @atArgs