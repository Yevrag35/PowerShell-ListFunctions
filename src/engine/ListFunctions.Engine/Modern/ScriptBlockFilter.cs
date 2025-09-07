using ListFunctions.Internal;
using ListFunctions.Modern.Pools;
using ListFunctions.Modern.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using ZLinq;

#nullable enable

namespace ListFunctions.Modern
{
    public sealed class ScriptBlockFilter : IDisposable
    {
        private bool _disposed;
        private PSThisVariable _constants;
        private List<PSVariable> _extraVariables;
        private readonly ScriptBlock _scriptBlock;
        private List<PSVariable> _variables;

        public ScriptBlockFilter(ScriptBlock scriptBlock, params
#if NET9_0_OR_GREATER
                ReadOnlySpan<PSVariable>
#else
                PSVariable[]
#endif
                additionalVariables)
        {
            Guard.NotNull(scriptBlock, nameof(scriptBlock));
            _scriptBlock = scriptBlock;
            _extraVariables = ListPool<PSVariable>.Rent();
            _extraVariables.AddRange(additionalVariables
#if !NET9_0_OR_GREATER
                ?? Array.Empty<PSVariable>()
                #endif
            );
            _variables = ListPool<PSVariable>.Rent();
            _constants = ObjPool<PSThisVariable>.Rent();
        }

        private List<PSVariable> InitializeContext(object? value)
        {
            _variables.Clear();
            _constants.SetValue(value);
            _constants.InsertIntoList(_variables);
            _variables.AddRange(_extraVariables);
            return _variables;
        }

        public bool All(ICollection? collection)
        {
            if (collection is null || collection.Count == 0)
                return false;

            foreach (object? item in collection)
            {
                if (!this.IsTrue(item))
                    return false;
            }

            return true;
        }
        public bool Any(ICollection? collection)
        {
            if (collection is null || collection.Count == 0)
                return false;

            foreach (object? item in collection)
            {
                if (this.IsTrue(item))
                    return true;
            }

            return false;
        }
        public bool IsTrue(object? value)
        {
            List<PSVariable> variables = this.InitializeContext(value);

            return _scriptBlock.InvokeWithContext(
                variables: variables,
                selectAs: LanguagePrimitives.IsTrue);
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (!(_constants is null))
                    {
                        ObjPool<PSThisVariable>.Return(_constants);
                        _constants = null!;
                    }

                    if (!(_extraVariables is null))
                    {
                        ListPool<PSVariable>.Return(_extraVariables);
                        _extraVariables = null!;
                    }

                    if (!(_variables is null))
                    {
                        ListPool<PSVariable>.Return(_variables);
                        _variables = null!;
                    }
                }

                _disposed = true;
            }
        }
    }
}