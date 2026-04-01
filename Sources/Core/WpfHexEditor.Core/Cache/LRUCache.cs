// ==========================================================
// Project: WpfHexEditor.Core
// File: LRUCache.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Generic thread-safe LRU (Least Recently Used) cache that automatically
//     evicts the least recently used entry when capacity is reached. Used for
//     caching search results, computed values, and expensive operations.
//
// Architecture Notes:
//     Repository pattern for in-memory storage. TKey must implement IEquatable.
//     Thread-safety via lock on internal LinkedList. Consumed by SearchEngine
//     and FormatDetectionService. No WPF dependencies.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace WpfHexEditor.Core.Cache
{
    /// <summary>
    /// Generic LRU (Least Recently Used) Cache implementation.
    /// Thread-safe cache that automatically evicts least recently used items when capacity is reached.
    /// Perfect for caching search results, computed values, or any expensive operations.
    /// </summary>
    /// <typeparam name="TKey">Type of cache key (must implement IEquatable for proper key comparison)</typeparam>
    /// <typeparam name="TValue">Type of cached value</typeparam>
    public class LRUCache<TKey, TValue> where TKey : IEquatable<TKey>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
        private readonly LinkedList<CacheItem> _lruList;
        private readonly object _lock = new object();

        /// <summary>
        /// Gets the maximum number of items this cache can hold
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Gets the current number of items in the cache
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }

        /// <summary>
        /// Creates a new LRU cache with the specified capacity
        /// </summary>
        /// <param name="capacity">Maximum number of items to cache (default: 10)</param>
        public LRUCache(int capacity = 10)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than 0");

            _capacity = capacity;
            _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
            _lruList = new LinkedList<CacheItem>();
        }

        /// <summary>
        /// Attempts to get a value from the cache.
        /// If found, moves the item to the front (most recently used).
        /// </summary>
        /// <param name="key">Key to look up</param>
        /// <param name="value">Output value if found</param>
        /// <returns>True if key was found, false otherwise</returns>
        public bool TryGet(TKey key, out TValue value)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    // Move to front (most recently used)
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);

                    value = node.Value.Value;
                    return true;
                }

                value = default;
                return false;
            }
        }

        /// <summary>
        /// Adds or updates a value in the cache.
        /// If capacity is reached, evicts the least recently used item.
        /// </summary>
        /// <param name="key">Key to add or update</param>
        /// <param name="value">Value to cache</param>
        public void Put(TKey key, TValue value)
        {
            lock (_lock)
            {
                // If key already exists, update it and move to front
                if (_cache.TryGetValue(key, out var existingNode))
                {
                    _lruList.Remove(existingNode);
                    existingNode.Value.Value = value;
                    _lruList.AddFirst(existingNode);
                    return;
                }

                // If at capacity, remove least recently used item
                if (_cache.Count >= _capacity)
                {
                    var lruNode = _lruList.Last;
                    if (lruNode != null)
                    {
                        _cache.Remove(lruNode.Value.Key);
                        _lruList.RemoveLast();
                    }
                }

                // Add new item to front (most recently used)
                var newItem = new CacheItem { Key = key, Value = value };
                var newNode = new LinkedListNode<CacheItem>(newItem);
                _lruList.AddFirst(newNode);
                _cache[key] = newNode;
            }
        }

        /// <summary>
        /// Removes a specific key from the cache
        /// </summary>
        /// <param name="key">Key to remove</param>
        /// <returns>True if key was found and removed, false otherwise</returns>
        public bool Remove(TKey key)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    _lruList.Remove(node);
                    _cache.Remove(key);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                _lruList.Clear();
            }
        }

        /// <summary>
        /// Checks if a key exists in the cache without affecting LRU order
        /// </summary>
        /// <param name="key">Key to check</param>
        /// <returns>True if key exists, false otherwise</returns>
        public bool ContainsKey(TKey key)
        {
            lock (_lock)
            {
                return _cache.ContainsKey(key);
            }
        }

        /// <summary>
        /// Gets all keys currently in the cache (ordered from most to least recently used)
        /// </summary>
        /// <returns>Enumerable of keys</returns>
        public IEnumerable<TKey> GetKeys()
        {
            lock (_lock)
            {
                return _lruList.Select(item => item.Key).ToList();
            }
        }

        /// <summary>
        /// Gets cache statistics for diagnostics
        /// </summary>
        /// <returns>String with cache statistics</returns>
        public string GetStatistics()
        {
            lock (_lock)
            {
                return $"LRU Cache: {_cache.Count}/{_capacity} items, " +
                       $"Usage: {(_cache.Count * 100.0 / _capacity):F1}%";
            }
        }

        /// <summary>
        /// Internal cache item structure
        /// </summary>
        private class CacheItem
        {
            public TKey Key { get; set; }
            public TValue Value { get; set; }
        }
    }
}
