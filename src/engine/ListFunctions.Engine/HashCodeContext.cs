using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;

namespace ListFunctions
{
    public class HashCodeContext
    {
        private readonly List<PSVariable> _list;

        public PSVariable Variable
        {
            get
            {
                if (_list.Count <= 0 || _list[0] is null)
                {
                    _list.Add(new PSVariable("_", null));
                    return _list[0];
                }
                else
                    return _list[0];
            }
        }

        public HashCodeContext()
        {
            _list = new List<PSVariable>(1)
            {
                new PSVariable("_", null)
            };
        }

        public List<PSVariable> GetList() => _list;
    }
}