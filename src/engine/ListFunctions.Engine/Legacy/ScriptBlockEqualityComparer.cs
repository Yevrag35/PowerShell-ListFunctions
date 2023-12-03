using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;

namespace ListFunctions.Legacy
{
    public sealed class ScriptBlockEqualityComparer<T> : BaseComparer<T>, IEqualityComparer<T>
    {
        private readonly ComparingContext _eqContext;
        private readonly HashCodeContext _hcContext;

        public ScriptBlock? EqualityScript { get; set; }
        public ScriptBlock? HashCodeScript { get; set; }


        #region CONSTRUCTORS
        public ScriptBlockEqualityComparer(ScriptBlock? equalityScript, ScriptBlock? hashCodeScript)
            : base()
        {
            // Equality Script assumes '$X' and '$Y' are being used to represent the two objects being compared.
            this.EqualityScript = equalityScript;
            this.HashCodeScript = hashCodeScript;

            _eqContext = new ComparingContext();
            _hcContext = new HashCodeContext();
        }

        #endregion

        #region EQUALITY METHODS

        public bool Equals(T x, T y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            return this.HasEqualityScript()
                ? this.ExecuteEqualityScript(x, y)
                : this.DefaultEqualityComparer.Equals(x, y);
        }
        private bool ExecuteEqualityScript(T x, T y)
        {
            _eqContext[nameof(x)].Value = x;
            _eqContext[nameof(y)].Value = y;

            Collection<PSObject>? resultCol = this.EqualityScript?.InvokeWithContext(null, _eqContext.GetList());
            if (null == resultCol)
            {
                return false;
            }

            return GetFirstValue(resultCol, obj =>
            {
                return Convert.ToBoolean(PSObject.AsPSObject(obj).ImmediateBaseObject);
            });
        }

        #endregion

        #region HASH CODE METHODS
        public int GetHashCode(T obj)
        {
            if (!this.IsValueType && null == obj)
            {
                return 0;
            }

            return this.HasHashCodeScript()
                ? this.ExecuteHashCodeScript(obj)
                : this.DefaultEqualityComparer.GetHashCode(obj);
        }

        private int ExecuteHashCodeScript(T obj)
        {
            _hcContext.Variable.Value = obj;
            Collection<PSObject>? resultCol;
            try
            {
                resultCol = this.HashCodeScript?.InvokeWithContext(null, _hcContext.GetList());
                if (null == resultCol)
                {
                    return 0;
                }
            }
            catch
            {
                return 0;
            }

            return GetFirstValue(resultCol, value =>
            {
                return Convert.ToInt32(PSObject.AsPSObject(value).ImmediateBaseObject);
            });
        }

        #endregion

        private bool HasEqualityScript() => !(this.EqualityScript is null);
        private bool HasHashCodeScript() => !(this.HashCodeScript is null);
    }
}
