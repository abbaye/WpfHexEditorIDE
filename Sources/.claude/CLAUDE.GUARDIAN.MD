\# 🛡️ claude.guardian.md



\## Purpose



Prevent architecture degradation over time



---



\## Detection



Claude must detect:



\* God classes (>500 lines)

\* Mixed responsibilities

\* Direct coupling between modules

\* UI logic in services



---



\## Actions



If detected:



1\. Flag issue

2\. Explain problem

3\. Propose refactor PLAN



---



\## Auto-Trigger



Activated when:



\* File modification

\* New module

\* Large feature



---



\## Refactor Types



\* Split class

\* Introduce service

\* Extract interface

\* Decouple modules



---



\## Validation



Refactor must:



\* Improve modularity

\* Reduce complexity

\* Maintain performance



