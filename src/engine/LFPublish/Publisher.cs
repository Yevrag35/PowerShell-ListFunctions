using LFPublish.Internal;
using ListFunctions.Build;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.CompilerServices;
using ManCmd = Microsoft.PowerShell.Commands.NewModuleManifestCommand;

namespace LFPublish
{
    public static class Publisher
    {
        const char DASH = '-';
        static readonly Type _aliasAtt;
        static readonly Type _cmdletAtt;
        static Publisher()
        {
            _aliasAtt = typeof(AliasAttribute);
            _cmdletAtt = typeof(CmdletAttribute);
        }

        public static async Task GetModuleInfoAsync(Hashtable hashtable, CancellationToken cancellationToken = default)
        {
            if (!hashtable.IsSynchronized)
            {
                throw new ArgumentException("The hashtable must be synchronized.");
            }

            var tuple = (hashtable, Path.GetDirectoryName((string)hashtable[nameof(ManCmd.Path)]!));

            Task formatAndTypesTask = Task.Factory
                .StartNew(GetFormatsAndTypePaths, tuple, cancellationToken);

            IEnumerable<Type> types = InternalFinder.GetCmdletTypes(IsCmdlet);
            List<string> names = new(5);
            hashtable.Add(nameof(ManCmd.CmdletsToExport), names);

            List<string> aliases = new(names.Capacity * 2);
            hashtable.Add(nameof(ManCmd.AliasesToExport), aliases);

            foreach (Type cmdletType in types)
            {
                AddCmdletData(cmdletType, names, aliases);
            }

            names.Sort();
            aliases.Sort();

            await formatAndTypesTask;
        }
        public static async Task<Hashtable> GetModuleInfoAsync(string outputDirectory, CancellationToken cancellationToken = default)
        {
            Hashtable table = BuildTable(outputDirectory);
            var tuple = (typeof(InternalFinder).Assembly, table);

            Task infoTask = GetModuleInfoAsync(table, cancellationToken);
            Task readTask = Task.Factory.StartNew(ReadAssemblyInfo, tuple, cancellationToken);

            await Task.WhenAll(infoTask, readTask);

            return table;
        }

        #region BACKEND
        private static void AddCmdletData(Type cmdletType, List<string> names, List<string> aliases)
        {
            CmdletAttribute cmdletAtt = cmdletType.GetCustomAttributes<CmdletAttribute>().First();
            names.Add(GetCmdletName(cmdletAtt));

            if (cmdletType.IsDefined(_aliasAtt, false))
            {
                IEnumerable<string> allAliases = cmdletType.GetCustomAttributes<AliasAttribute>()
                    .Where(x => x.AliasNames is not null && x.AliasNames.Count > 0)
                    .SelectMany(x => x.AliasNames);

                aliases.AddRange(allAliases);
            }
        }
        private static Hashtable BuildTable(string outputDir)
        {
            Hashtable table = new(21)
            {
                { nameof(ManCmd.Path), Path.Combine(Path.GetFullPath(outputDir), $"{nameof(ListFunctions)}.psd1") },
                { nameof(ManCmd.FunctionsToExport), Array.Empty<string>() },
                { nameof(ManCmd.VariablesToExport), Array.Empty<string>() },

            };

            return Hashtable.Synchronized(table);
        }
        private static string GetCmdletName(CmdletAttribute cmdletAttribute)
        {
            int length = cmdletAttribute.VerbName.Length + cmdletAttribute.NounName.Length + 1;
            return string.Create(length, cmdletAttribute, (chars, state) =>
            {
                state.VerbName.CopyTo(chars);
                int position = state.VerbName.Length;

                chars[position++] = DASH;

                state.NounName.CopyTo(chars.Slice(position));
            });
        }
        private static void GetFormatsAndTypePaths(object? state)
        {
            if (state is not ValueTuple<Hashtable, string> tuple || string.IsNullOrWhiteSpace(tuple.Item2))
            {
                return;
            }

            string outputDir = tuple.Item2;

            string formatStr = ".Format.ps1xml";
            string typeStr = ".Type.ps1xml";
            FileTypes paths = new(formatStr, typeStr, outputDir);

            foreach (string file in Directory.EnumerateFiles(outputDir, "*.ps1xml", SearchOption.AllDirectories))
            {
                paths.AddPath(file);
            }

            if (paths.HasAnyFiles)
            {
                tuple.Item1.Add("Formats", paths.FormatPaths.ToArray());
                tuple.Item1.Add("Types", paths.TypePaths.ToArray());
            }
        }
        private static bool IsCmdlet(Type type)
        {
            return type.IsDefined(_cmdletAtt, false);
        }
        private static void ReadAssemblyInfo(object? state)
        {
            if (state is not ValueTuple<Assembly, Hashtable> tuple)
            {
                throw new InvalidOperationException("Invalid tuple");
            }

            (Assembly assembly, Hashtable table) = tuple;

            if (TryGetAttributeInfo(assembly, (AssemblyFileVersionAttribute x) => Version.Parse(x.Version), out var version))
            {
                table.Add(nameof(ManCmd.ModuleVersion), new Version(version.Major, version.Minor, version.Build));
            }

            if (TryGetAttributeInfo(assembly, (AssemblyDescriptionAttribute x) => x.Description, out string? description))
            {
                table.Add(nameof(ManCmd.Description), description);
            }

            if (TryGetAttributeInfo(assembly, (AssemblyCopyrightAttribute x) => x.Copyright, out string? copyright))
            {
                table.Add(nameof(ManCmd.Copyright), copyright.Replace(((char)169).ToString(), "(c)"));
            }

            if (TryGetAttributeInfo(assembly, (AssemblyCompanyAttribute x) => x.Company, out string? company))
            {
                table.Add(nameof(ManCmd.CompanyName), company);
            }

            foreach (var metadataAtt in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                object? val = metadataAtt.Value;
                if (!string.IsNullOrEmpty(metadataAtt.Value) && metadataAtt.Value.Contains(','))
                {
                    val = metadataAtt.Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                }

                table.Add(metadataAtt.Key, val);
            }
        }

        private static bool TryGetAttributeInfo<T, TOut>(Assembly assembly, Func<T, TOut> selectFunc, [NotNullWhen(true)] out TOut? value)
            where T : Attribute
            where TOut : notnull
        {
            if (assembly.IsDefined(typeof(T), false))
            {
                T att = assembly.GetCustomAttributes<T>().First();
                value = selectFunc(att);
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        #endregion


        private readonly struct FileTypes
        {
            readonly string _formatStr;
            readonly string _outputDir;
            readonly string _typeStr;

            internal readonly List<string> FormatPaths;
            internal readonly List<string> TypePaths;

            internal bool HasAnyFiles => FormatPaths.Count > 0 || TypePaths.Count > 0;

            internal FileTypes(string formatStr, string typeStr, string outputDir)
            {
                _formatStr = formatStr;
                _outputDir = outputDir;
                _typeStr = typeStr;
                FormatPaths = new(10);
                TypePaths = new(1);
            }

            internal void AddPath(string filePath)
            {
                ReadOnlySpan<char> path = filePath.AsSpan();
                if (path.EndsWith(_formatStr, StringComparison.InvariantCultureIgnoreCase)
                    &&
                    TryGetFinalPath(_outputDir, path, out string? finalPath))
                {
                    FormatPaths.Add(finalPath);
                }
                else if (path.EndsWith(_typeStr, StringComparison.InvariantCultureIgnoreCase)
                    &&
                    TryGetFinalPath(_outputDir, path, out string? typePath))
                {
                    TypePaths.Add(typePath);
                }
            }
            private static bool TryGetFinalPath(ReadOnlySpan<char> outputDir, ReadOnlySpan<char> path, [NotNullWhen(true)] out string? finalPath)
            {
                finalPath = null;
                foreach (ReadOnlySpan<char> section in path.SpanSplit(outputDir))
                {
                    if (section.StartsWith('/') || section.StartsWith('\\'))
                    {
                        finalPath = new string(section.Slice(1));
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
