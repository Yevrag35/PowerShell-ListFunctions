using System;
using System.IO;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions
{
    public sealed class ModuleInitializer : IModuleAssemblyInitializer
    {
        private static readonly string _assLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private const string DLL = ".dll";
        private const string BACK = "\\";

        public void OnImport()
        {
            AppDomain.CurrentDomain.AssemblyResolve += this.CurrentDomain_AssemblyResolve;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs e)
        {
            string name = e.Name;
            int index = name.IndexOf(',');
            if (index != -1)
            {
                name = name.Substring(0, index);
            }

            string fullPath = string.Concat(_assLocation, BACK, name, DLL);
            if (File.Exists(fullPath))
            {
                return Assembly.LoadFile(fullPath);
            }

            return null;
        }
    }
}
