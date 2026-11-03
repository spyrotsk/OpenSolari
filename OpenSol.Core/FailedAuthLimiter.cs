using System;
using System.Collections.Concurrent;

namespace OpenSol.Core
{
    /// <summary>
    /// Blocks an IP address for a while after too many requests with a wrong token,
    /// so codes cannot be guessed and wrong tokens cannot keep the server busy.
    /// </summary>
    public class FailedAuthLimiter
    {
        public int MaxFailures { get; }
        public TimeSpan Window { get; }
        public TimeSpan BlockDuration { get; }

        private const int CleanupThreshold = 10000;

        private sealed class Entry
        {
            public int Failures;
            public DateTime WindowStart;
            public DateTime BlockedUntil;
        }

        private readonly ConcurrentDictionary<string, Entry> _entries = new ConcurrentDictionary<string, Entry>();

        public FailedAuthLimiter(int maxFailures = 5, TimeSpan? window = null, TimeSpan? blockDuration = null)
        {
            MaxFailures = maxFailures;
            Window = window ?? TimeSpan.FromMinutes(1);
            BlockDuration = blockDuration ?? TimeSpan.FromMinutes(15);
        }

        public bool IsBlocked(string ip)
        {
            if (!_entries.TryGetValue(ip, out var entry)) return false;
            lock (entry)
            {
                return entry.BlockedUntil > DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Records a failed attempt. Returns true when this attempt blocks the address.
        /// </summary>
        public bool RegisterFailure(string ip)
        {
            var now = DateTime.UtcNow;
            if (_entries.Count > CleanupThreshold) RemoveStale(now);

            var entry = _entries.GetOrAdd(ip, _ => new Entry { WindowStart = now });
            lock (entry)
            {
                if (now - entry.WindowStart > Window)
                {
                    entry.Failures = 0;
                    entry.WindowStart = now;
                }

                entry.Failures++;
                if (entry.Failures >= MaxFailures)
                {
                    entry.Failures = 0;
                    entry.BlockedUntil = now + BlockDuration;
                    return true;
                }
                return false;
            }
        }

        private void RemoveStale(DateTime now)
        {
            foreach (var pair in _entries)
            {
                bool stale;
                lock (pair.Value)
                {
                    stale = pair.Value.BlockedUntil <= now && now - pair.Value.WindowStart > Window;
                }
                if (stale) _entries.TryRemove(pair.Key, out _);
            }
        }
    }
}
