using System;
using System.Collections.Generic;
using System.Threading;

namespace disfr.Configuration
{
    public sealed class ThreadSafeCache<TKey, TValue>
    {
        private readonly ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();
        private readonly Dictionary<TKey, TValue> cache = new Dictionary<TKey, TValue>();
        private readonly Func<TKey, TValue> evaluator;

        public ThreadSafeCache(Func<TKey, TValue> evaluator) => this.evaluator = evaluator != null ? evaluator : throw new ArgumentNullException(nameof(evaluator));

        public TValue this[TKey key]
        {
            get
            {
                TValue obj;
                bool flag;
                using (cacheLock.GetReadLock())
                    flag = cache.TryGetValue(key, out obj);
                if (flag)
                    return obj;
                using (this.cacheLock.GetWriteLock())
                {
                    if (!this.cache.TryGetValue(key, out obj))
                    {
                        obj = this.evaluator(key);
                        this.cache.Add(key, obj);
                    }
                }
                return obj;
            }
        }

        public void Invalidate()
        {
            this.cacheLock.EnterWriteLock();
            this.cache.Clear();
            this.cacheLock.ExitWriteLock();
        }
    }
}
