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

        public void OnImport()
        {
            AppDomain.CurrentDomain.AssemblyResolve += this.CurrentDomain_AssemblyResolve;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs e)
        {
            int index = e.Name.IndexOf(',');
            string name = e.Name.Substring(0, index);

            string fullPath = Path.Combine(_assLocation, $"{name}.dll");
            if (File.Exists(fullPath))
            {
                return Assembly.LoadFile(fullPath);
            }

            return null;
        }
    }
}
