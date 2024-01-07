using ListFunctions.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace ListFunctions.Modern.Exceptions
{
    public sealed class ActivatorCtorException : Exception
    {
        const string DEF_FORMAT = "An exception occurred attempting to construct an object of type \"{0}\".";

        public Type OffendingType { get; }

        public ActivatorCtorException(Type offendingType)
            : this(offendingType, GetNullException())
        {
        }
        public ActivatorCtorException(Type offendingType, Exception? innerException)
            : base(string.Format(DEF_FORMAT, offendingType.GetTypeName()), innerException)
        {
            this.OffendingType = offendingType;
        }

        private static NullReferenceException GetNullException()
        {
            return new NullReferenceException($"The constructed object returned from \"{nameof(Activator)}.{nameof(Activator.CreateInstance)}()\" was null.");
        }
    }
}

