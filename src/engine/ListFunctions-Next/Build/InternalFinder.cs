using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyMetadata("Author", "Mike Garvey")]
[assembly: AssemblyMetadata("CompatiblePSEditions", "Core,Desktop")]
[assembly: AssemblyMetadata("Guid", "731eae50-355d-4718-a2df-89d9beaae89e")]
[assembly: AssemblyMetadata("HelpInfoURI", "https://github.com/Yevrag35/PowerShell-ListFunctions/issues")]
[assembly: AssemblyMetadata("IconUri", "https://github.com/Yevrag35/PowerShell-ListFunctions/blob/master/.icon/list-functions.png?raw=true")]
[assembly: AssemblyMetadata("LicenseUri", "https://raw.githubusercontent.com/Yevrag35/PowerShell-ListFunctions/master/LICENSE")]
[assembly: AssemblyMetadata("PowerShellVersion", "5.1")]
#if NET6_0_OR_GREATER
[assembly: AssemblyMetadata("RootModule", "ListFunctions.Next.dll")]
[assembly: AssemblyDescription("A PowerShell module that provides functions to manipulate and search through Arrays, Collections, Lists, and Sets.")]
#else
[assembly: AssemblyMetadata("RootModule", "ListFunctions.NETFramework.dll")]
#endif
[assembly: AssemblyMetadata("ProjectUri", "https://github.com/Yevrag35/PowerShell-ListFunctions")]
[assembly: AssemblyMetadata("Tags", "All,Any,Array,Assert,bool,Collection,compare,Condition,count,Enumerable,equality,Find,HashSet,index,Last,Linq,List,Modify,Predicate,Remove,set,sort,Test,Where")]
[assembly: InternalsVisibleTo("LFPublish, PublicKey=0024000004800000140100000602000000240000525341310008000001000100e17bb555393c9ba9ea4c510f00814fd916d9a179760843dc3597977c796fab81086199b08bb44d2c8442c9741d24bc6bbcaf02c1c5390156cad6ad1085a68f2daa829d566849975c0254c324374e61f3d22fcd6ac2e0c9ff88613465d063c6f3363ef4ffe4fb2503721427e57d41bd7c742b8e364b5b0b29629838a454367f434143eadbe92073c389e55cc0f8f9b3a696dc6b4bf97dde7e4470a32306614bebdc83d734347df09fd095d5d5bc5fa91e7effabed78cfc4e0965e2288d3733a107dc46fc2a39cdf9ef32a2ba578fa09d43c889b47291f04c4f3e9d95d7caacc0dba69eef8e9215523d1ac21911b26ec56e900d9c55939c0fa732be8758990e8cb")]
namespace ListFunctions.Build
{
    internal static class InternalFinder
    {
        internal static IEnumerable<Type> GetCmdletTypes(Func<Type, bool> filter)
        {
            Assembly thisAss = typeof(InternalFinder).Assembly;

            return thisAss
                .GetExportedTypes()
                    .Where(filter);
        }
    }
}

