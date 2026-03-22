\# ⚡ claude.perf.md — Binary/Hex Optimized



\## Core Rules



\* NEVER load full file in memory

\* Use streaming / paging



\## Buffer Management



\* Use chunk-based processing

\* Avoid copies

\* Prefer spans / memory views



\## UI + Data



\* Virtualized rendering mandatory

\* Incremental updates only



\## Large Files



\* Must support GB-scale files

\* No blocking UI thread



\## Validation



\* O(1) or O(log n) where possible

\* No unnecessary allocations



❌ Otherwise rejected



