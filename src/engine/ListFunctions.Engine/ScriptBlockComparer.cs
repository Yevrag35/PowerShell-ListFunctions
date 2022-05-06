using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Text;

namespace ListFunctions
{
    public class ScriptBlockComparer<T> : BaseComparer<T>, IComparer<T>
    {
        private readonly ComparingContext _context;

        public ScriptBlock ComparerScript { get; set; }

        public ScriptBlockComparer(ScriptBlock comparerScript)
        {
            _context = new ComparingContext();
            this.ComparerScript = comparerScript;
        }

        public int Compare(T x, T y)
        {
            return this.HasComparerScript()
                ? this.ExecuteCompareScript(x, y)
                : this.DefaultComparer.Compare(x, y);
        }

        private int ExecuteCompareScript(T x, T y)
        {
            _context[nameof(x)].Value = x;
            _context[nameof(y)].Value = y;

            Collection<PSObject> resultCol = this.ComparerScript.InvokeWithContext(null, _context.GetList());
            return GetFirstValue(resultCol, obj =>
            {
                return Convert.ToInt32(PSObject.AsPSObject(obj).ImmediateBaseObject);
            });
        }

        private bool HasComparerScript() => !(this.ComparerScript is null);
    }
}