// <copyright file="Hash.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// Derived from code under the Apache 2 License from https://github.com/dotliquid/dotliquid

namespace DotLiquid
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    public class Hash : IDictionary<string, object>, IDictionary
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Action<object, Hash>> MapperCache = new System.Collections.Concurrent.ConcurrentDictionary<string, Action<object, Hash>>();
        private readonly Func<Hash, string, object> lambda;
        private readonly Dictionary<string, object> nestedDictionary;
        private readonly object defaultValue;

        /// <summary>
        /// Build a hash from the properties of an object of any type (including anonymous types).
        /// </summary>
        /// <param name="anonymousObject"></param>
        /// <param name="includeBaseClassProperties">If this is set to true, method will map base class' properties too. </param>
        /// <returns></returns>
        public static Hash FromAnonymousObject(object anonymousObject, bool includeBaseClassProperties = false)
        {
            var result = new Hash();
            if (anonymousObject != null)
            {
                FromAnonymousObject(anonymousObject, result, includeBaseClassProperties);
            }

            return result;
        }

        private static void FromAnonymousObject(object anonymousObject, Hash hash, bool includeBaseClassProperties)
        {
            Action<object, Hash> mapper = GetObjToDictionaryMapper(anonymousObject.GetType(), includeBaseClassProperties);
            mapper.Invoke(anonymousObject, hash);
        }

        private static Action<object, Hash> GetObjToDictionaryMapper(Type type, bool includeBaseClassProperties)
        {
            string cacheKey = type.FullName + "_" + (includeBaseClassProperties ? "WithBaseProperties" : "WithoutBaseProperties");

            if (!MapperCache.TryGetValue(cacheKey, out Action<object, Hash> mapper))
            {
                /* Bogdan Mart: Note regarding concurrency:
                 * This is concurrent dictionary, but if this will be called from two threads
                 * this code would generate two same mappers, which will cause some CPU overhead.
                 * But I have no idea on what I can lock here, first thought was to use lock(type),
                 * but that could cause deadlock, if some outside code will lock Type.
                 * Only correct solution would be to use ConcurrentDictionary<Type, Action<object, Hash>>
                 * with some CAS race, and then locking, or Semaphore, but first will add complexity,
                 * second would add overhead in locking on Kernel-level named object.
                 *
                 * So I assume tradeoff in not using locks here is better,
                 * we at most will waste some CPU cycles on code generation,
                 * but RAM would be collected, due to http://stackoverflow.com/questions/5340201/
                 *
                 * If someone have conserns, than one can lock(mapperCache) but that would
                 * create bottleneck, as only one mapper could be generated at a time.
                 */
                mapper = GenerateMapper(type, includeBaseClassProperties);
                MapperCache[cacheKey] = mapper;
            }

            return mapper;
        }

        private static void AddBaseClassProperties(Type type, List<PropertyInfo> propertyList)
        {
            propertyList.AddRange(type.GetTypeInfo().BaseType.GetTypeInfo().DeclaredProperties
                .Where(p => p.CanRead && p.GetMethod.IsPublic && !p.GetMethod.IsStatic).ToList());
        }

        private static Action<object, Hash> GenerateMapper(Type type, bool includeBaseClassProperties)
        {
            ParameterExpression objParam = Expression.Parameter(typeof(object), "objParam");
            ParameterExpression hashParam = Expression.Parameter(typeof(Hash), "hashParam");
            var bodyInstructions = new List<Expression>();

            ParameterExpression castedObj = Expression.Variable(type, "castedObj");

            bodyInstructions.Add(
                Expression.Assign(castedObj, Expression.Convert(objParam, type)));

            // Add properties
            var propertyList = type.GetTypeInfo().DeclaredProperties
                .Where(p => p.CanRead && p.GetMethod.IsPublic && !p.GetMethod.IsStatic).ToList();

            // Add properties from base class
            if (includeBaseClassProperties)
            {
                AddBaseClassProperties(type, propertyList);
            }

            foreach (PropertyInfo property in propertyList)
            {
                bodyInstructions.Add(
                    Expression.Assign(
                        Expression.MakeIndex(
                            hashParam,
                            typeof(Hash).GetTypeInfo().GetDeclaredProperty("Item"),
                            new[] { Expression.Constant(property.Name, typeof(string)) }),
                        Expression.Convert(
                            Expression.Property(castedObj, property),
                            typeof(object))));
            }

            BlockExpression body = Expression.Block(typeof(void), new[] { castedObj }, bodyInstructions);

            var expr = Expression.Lambda<Action<object, Hash>>(body, objParam, hashParam);

            return expr.Compile();
        }

        public static Hash FromDictionary(IDictionary<string, object> dictionary)
        {
            var result = new Hash();

            foreach (KeyValuePair<string, object> keyValue in dictionary)
            {
                if (keyValue.Value is IDictionary<string, object>)
                {
                    result.Add(keyValue.Key, FromDictionary((IDictionary<string, object>)keyValue.Value));
                }
                else
                {
                    result.Add(keyValue);
                }
            }

            return result;
        }

        public Hash(object defaultValue)
            : this()
        {
            this.defaultValue = defaultValue;
        }

        public Hash(Func<Hash, string, object> lambda)
            : this()
        {
            this.lambda = lambda;
        }

        public Hash()
        {
            this.nestedDictionary = new Dictionary<string, object>(Template.NamingConvention.StringComparer);
        }

        public void Merge(IDictionary<string, object> otherValues)
        {
            foreach (string key in otherValues.Keys)
            {
                this.nestedDictionary[key] = otherValues[key];
            }
        }

        protected virtual object GetValue(string key)
        {
            if (this.nestedDictionary.ContainsKey(key))
            {
                return this.nestedDictionary[key];
            }

            if (this.lambda != null)
            {
                return this.lambda(this, key);
            }

            if (this.defaultValue != null)
            {
                return this.defaultValue;
            }

            return null;
        }

        public T Get<T>(string key)
        {
            return (T)this[key];
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return this.nestedDictionary.GetEnumerator();
        }

        public void Remove(object key)
        {
            ((IDictionary)this.nestedDictionary).Remove(key);
        }

        object IDictionary.this[object key]
        {
            get
            {
                if (!(key is string))
                {
                    throw new NotSupportedException();
                }

                return this.GetValue((string)key);
            }

            set
            {
                ((IDictionary)this.nestedDictionary)[key] = value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.nestedDictionary.GetEnumerator();
        }

        public void Add(KeyValuePair<string, object> item)
        {
            ((IDictionary<string, object>)this.nestedDictionary).Add(item);
        }

        public virtual bool Contains(object key)
        {
            return ((IDictionary)this.nestedDictionary).Contains(key);
        }

        public void Add(object key, object value)
        {
            ((IDictionary)this.nestedDictionary).Add(key, value);
        }

        public void Clear()
        {
            this.nestedDictionary.Clear();
        }

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return ((IDictionary)this.nestedDictionary).GetEnumerator();
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return ((IDictionary<string, object>)this.nestedDictionary).Contains(item);
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            ((IDictionary<string, object>)this.nestedDictionary).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            return ((IDictionary<string, object>)this.nestedDictionary).Remove(item);
        }

        public void CopyTo(Array array, int index)
        {
            ((IDictionary)this.nestedDictionary).CopyTo(array, index);
        }

        public int Count
        {
            get { return this.nestedDictionary.Count; }
        }

        public object SyncRoot
        {
            get { return ((IDictionary)this.nestedDictionary).SyncRoot; }
        }

        public bool IsSynchronized
        {
            get { return ((IDictionary)this.nestedDictionary).IsSynchronized; }
        }

        ICollection IDictionary.Values
        {
            get { return ((IDictionary)this.nestedDictionary).Values; }
        }

        public bool IsReadOnly
        {
            get { return ((IDictionary<string, object>)this.nestedDictionary).IsReadOnly; }
        }

        public bool IsFixedSize
        {
            get { return ((IDictionary)this.nestedDictionary).IsFixedSize; }
        }

        public bool ContainsKey(string key)
        {
            return this.nestedDictionary.ContainsKey(key);
        }

        public void Add(string key, object value)
        {
            this.nestedDictionary.Add(key, value);
        }

        public bool Remove(string key)
        {
            return this.nestedDictionary.Remove(key);
        }

        public bool TryGetValue(string key, out object value)
        {
            return this.nestedDictionary.TryGetValue(key, out value);
        }

        public object this[string key]
        {
            get { return this.GetValue(key); }
            set { this.nestedDictionary[key] = value; }
        }

        public ICollection<string> Keys
        {
            get { return this.nestedDictionary.Keys; }
        }

        ICollection IDictionary.Keys
        {
            get { return ((IDictionary)this.nestedDictionary).Keys; }
        }

        public ICollection<object> Values
        {
            get { return this.nestedDictionary.Values; }
        }
    }
}
