# Module: Guardian — Architecture Integrity

Detect: god classes (>500 lines), mixed responsibilities, direct module coupling, UI logic in services.

On detection: flag issue → explain problem → propose refactor PLAN.

Auto-trigger: file modification, new module, large feature.

Refactor types: split class, introduce service, extract interface, decouple modules.

Refactor must: improve modularity, reduce complexity, maintain performance.
