using ListFunctions.Modern.Constructors;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ListFunctions.Modern
{
    public sealed class AddMethodInvoker
    {
        readonly Type[] _genericTypes;
        readonly MethodInfo _method;
        public Type ImplementingType { get; }

        public AddMethodInvoker(GenericCollectionCtor constructor)
        {
            Guard.NotNull(constructor, nameof(constructor));

            this.ImplementingType = constructor.ConstructingGenericType;
            _genericTypes = constructor.GenericArgumentTypes;
            _method = ReflectionResolver.GetAddMethod(this.ImplementingType, _genericTypes);
        }

        public bool TryInvoke(object collection, object?[] arguments, bool addIfNull, [NotNullWhen(false)] out Exception? caughtException)
        {
            if (arguments is null)
            {
                caughtException = new ArgumentNullException(nameof(arguments), "The arguments array itself cannot be null.");
                return false;
            }

            caughtException = null;
            if (!addIfNull)
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (arguments[i] is null)
                    {
                        return true;    // don't add but also don't error.
                    }
                }
            }

            try
            {
                _ = _method.Invoke(collection, arguments);
                return true;
            }
            catch (Exception e)
            {
                caughtException = e;
                return false;
            }
        }
    }
}

