﻿using System;

namespace Cache.Disk
{
    /// <summary>
    /// Sorts entries by date of the last access, evicting old ones first.
    /// </summary>
    public class DefaultEntryEvictionComparatorSupplier : IEntryEvictionComparatorSupplier
    {
        /// <summary>
        /// Returns the <see cref="IEntryEvictionComparator"/>
        /// </summary>
        /// <returns></returns>
        public IEntryEvictionComparator Get()
        {
            return new EntryEvictionComparatorImpl((e1, e2) =>
            {
                DateTime time1 = e1.Timestamp;
                DateTime time2 = e2.Timestamp;
                return time1 < time2 ? -1 : ((time2 == time1) ? 0 : 1);
            });
        }
    }
}
