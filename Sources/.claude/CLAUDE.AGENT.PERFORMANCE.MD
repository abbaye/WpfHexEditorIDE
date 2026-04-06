# Agent: PERFORMANCE [Priority: CRITICAL — large file handling]

Rules: no full file load, streaming/chunking, avoid memory copies.
Detect: O(n²) operations, UI blocking, large allocations.
Output: performance risks, optimization suggestions.
