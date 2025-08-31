using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace ListFunctions.Modern
{
    public static class ReflectionResolver
    {
        static readonly MethodInfo _colAddMethod;
        static readonly MethodInfo _dictAddMethod;

        static ReflectionResolver()
        {
            Type type = typeof(ReflectionResolver);
            _colAddMethod = type.GetMethod(nameof(GetCollectionAdd));
            _dictAddMethod = type.GetMethod(nameof(GetDictionaryAdd));
        }

        public static MethodInfo GetAddMethod(Type collectionType, Type[] types)
        {
            if (typeof(Hashtable).Equals(collectionType))
            {
                return GetHashtableAdd();
            }

            Guard.NotNull(types, nameof(types));
            if (types.Length <= 0 || types.Length > 2)
            {
                throw new ArgumentException("Wrong number of Type arguments were supplied.");
            }

            MethodInfo getAdd;
            if (typeof(IDictionary).IsAssignableFrom(collectionType) && types.Length == 2)
            {
                getAdd = _dictAddMethod.MakeGenericMethod(collectionType, types[0], types[1]);
            }
            else if ((typeof(ICollection).IsAssignableFrom(collectionType)
                || (collectionType.IsGenericType && 
                    typeof(HashSet<>).Equals(collectionType.GetGenericTypeDefinition())))
                && types.Length == 1)
            {
                getAdd = _colAddMethod.MakeGenericMethod(collectionType, types[0]);
            }
            else
            {
                throw new ArgumentException("Type argument exception.");
            }

            return (MethodInfo)getAdd.Invoke(null, null);
        }

        public static MethodInfo GetCollectionAdd<TCol, TItem>() where TCol : ICollection<TItem>
        {
            return GetCollectionAddMethod<TCol>(col => col.Add(default!));
        }

        private static MethodInfo GetHashtableAdd()
        {
            Expression<Action<Hashtable>> exp = (x) => x.Add(default!, default!);
            return ((MethodCallExpression)exp.Body).Method;
        }

        public static MethodInfo GetDictionaryAdd<TDict, TKey, TValue>()
            where TDict : IDictionary<TKey, TValue>
            where TKey : notnull
        {
            return GetCollectionAddMethod<TDict>(dict => dict.Add(default!, default!));
        }

        private static MethodInfo GetCollectionAddMethod<TCol>(Expression<Action<TCol>> callExpression)
        {
            return ((MethodCallExpression)callExpression.Body).Method;
        }
    }
}

