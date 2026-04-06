# Module: Performance / Binary / Hex

- NEVER load full file in memory. Streaming/paging mandatory.
- Chunk-based processing. Avoid copies. Prefer spans/memory views.
- Virtualized rendering mandatory. Incremental updates only.
- Must support GB-scale files. No blocking UI thread.

Validation: O(1) or O(log n) where possible, no unnecessary allocations. ❌ Otherwise rejected.
