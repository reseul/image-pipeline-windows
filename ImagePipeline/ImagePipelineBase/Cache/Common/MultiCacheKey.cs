﻿using FBCore.Common.Internal;
using System;
using System.Collections.Generic;

namespace Cache.Common
{
    /// <summary>
    /// A cache key that wraps multiple cache keys.
    ///
    /// Note: <code> equals</code> and <code> hashcode</code> are implemented in a way that two MultiCacheKeys are
    /// equal if and only if the underlying list of cache keys is equal. That implies AllOf semantics.
    /// Unfortunately, it is not possible to implement AnyOf semantics for <code> equals</code> because the
    /// transitivity requirement wouldn't be satisfied. I.e. we would have:
    /// {A} = {A, B}, {A, B} = {B}, but {A} != {B}.
    ///
    /// It is fine to use this key with AnyOf semantics, but one should be aware of <code> equals</code> and
    /// <code> hashcode</code> behavior, and should implement AnyOf logic manually.
    /// </summary>
    public class MultiCacheKey : ICacheKey
    {
        internal readonly IList<ICacheKey> _cacheKeys;

        /// <summary>
        /// Instantiates the <see cref="MultiCacheKey"/>
        /// </summary>
        /// <param name="cacheKeys"></param>
        public MultiCacheKey(IList<ICacheKey> cacheKeys)
        {
            _cacheKeys = Preconditions.CheckNotNull(cacheKeys);
        }

        /// <summary>
        /// Gets the cache keys
        /// </summary>
        public IList<ICacheKey> CacheKeys
        {
            get
            {
                return _cacheKeys;
            }
        }

        /// <summary>
        /// This is useful for instrumentation and debugging purposes. 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "MultiCacheKey:" + _cacheKeys.ToString();
        }

        /// <summary>
        /// Compares objects _cacheKeys
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o.GetType() == typeof(MultiCacheKey)) 
            {
                MultiCacheKey otherKey = (MultiCacheKey)o;
                return _cacheKeys.Equals(otherKey._cacheKeys);
            }

            return false;
        }

        /// <summary>
        /// Gets the hash code
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return _cacheKeys.GetHashCode();
        }

        /// <summary>
        /// Returns true if this key was constructed from this <see cref="Uri"/>.
        ///
        /// Used for cases like deleting all keys for a given uri.
        /// </summary>
        public bool ContainsUri(Uri uri)
        {
            foreach (var cacheKey in _cacheKeys)
            {
                if (cacheKey.ContainsUri(uri))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
