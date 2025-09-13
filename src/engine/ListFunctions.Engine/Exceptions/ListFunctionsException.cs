using System;
using System.Collections.Generic;
using System.Text;

namespace ListFunctions.Exceptions
{
    public class ListFunctionsException : Exception
    {
        public ListFunctionsException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}

