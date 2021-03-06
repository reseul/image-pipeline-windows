﻿using ImagePipeline.Memory;
using System;
using System.Collections.Generic;

namespace ImagePipeline.Tests.Memory
{
    /// <summary>
    /// Helper class to get pool stats
    /// </summary>
    public class PoolStats<T>
    {
        /// <summary>
        /// The base pool
        /// </summary>
        public BasePool<T> Pool { get; set; }

        /// <summary>
        /// Keep track of used bytes
        /// </summary>
        public int UsedBytes { get; set; }

        /// <summary>
        /// Keep track of used count
        /// </summary>
        public int UsedCount { get; set; }

        /// <summary>
        /// Keep track of free bytes
        /// </summary>
        public int FreeBytes { get; set; }

        /// <summary>
        /// Keep track of free count
        /// </summary>
        public int FreeCount { get; set; }

        /// <summary>
        /// Keep track of bucket stats
        /// </summary>
        public Dictionary<int, Tuple<int, int>> BucketStats { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public PoolStats(BasePool<T> pool)
        {
            Pool = pool;
            BucketStats = new Dictionary<int, Tuple<int, int>>();
        }

        /// <summary>
        /// Set pool
        /// </summary>
        public void SetPool(BasePool<T> pool)
        {
            Pool = pool;
        }

        /// <summary>
        /// Refresh all stats
        /// </summary>
        public void Refresh()
        {
            RefreshBasic();
            RefreshBucketStats();
        }

        /// <summary>
        /// Refresh bytes and count stats
        /// </summary>
        public void RefreshBasic()
        {
            UsedBytes = Pool._usedCounter.NumBytes;
            UsedCount = Pool._usedCounter.Count;
            FreeBytes = Pool._freeCounter.NumBytes;
            FreeCount = Pool._freeCounter.Count;
        }

        /// <summary>
        /// Refresh bucket stats
        /// </summary>
        public void RefreshBucketStats()
        {
            BucketStats.Clear();
            foreach (var entry in Pool.Buckets)
            {
                int bucketedSize = entry.Key;
                Bucket<T> bucket = entry.Value;
                BucketStats.Add(bucketedSize, new Tuple<int, int>(bucket.GetInUseCount(), bucket.GetFreeListSize()));
            }
        }
    }
}
