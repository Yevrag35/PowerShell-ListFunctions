using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions
{
    public sealed class ModuleInitializer : IModuleAssemblyInitializer
    {
        private static readonly string _assLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly Dictionary<string, Assembly> _assNames = new Dictionary<string, Assembly>(4);

        public void OnImport()
        {
            AppDomain.CurrentDomain.AssemblyResolve += this.CurrentDomain_AssemblyResolve;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs e)
        {
            int index = e.Name.IndexOf(',');
            string name = e.Name.Substring(0, index);

            if (_assNames.TryGetValue(name, out Assembly ass))
            {
                return ass;
            }

            string fullPath = Path.Combine(_assLocation, $"{name}.dll");
            if (File.Exists(fullPath))
            {
                ass = Assembly.LoadFile(fullPath);
                _assNames.Add(name, ass);
                return ass;
            }

            return null;
        }
    }
}
