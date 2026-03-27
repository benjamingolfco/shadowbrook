---
name: Container App resource sizing — .NET + Wolverine
description: Why 0.25vCPU/0.5Gi OOMs and what the minimum viable tier is for this stack
type: project
---

The test Container App (`shadowbrook-app-test`) was OOM-killed at 0.25 vCPU / 0.5Gi with `minReplicas: 0`.

**Why:** WolverineFx performs Roslyn-based code generation at startup (handler discovery, compiled pipelines). Combined with EF Core model initialization and OpenTelemetry SDK startup, the peak memory spike during cold starts exceeds 0.5Gi. Scale-to-zero means every inactivity period triggers this cold-start OOM cycle.

**Fix applied (2026-03-26):**
- Bumped to 0.5 vCPU / 1Gi (next valid consumption plan tier — ~$7.48/month at idle vs ~$3.74)
- Set `minReplicas: 1` to eliminate cold starts entirely for test stability
- Added `DOTNET_GCConserveMemory=7` and `DOTNET_GCHeapHardLimit=671088640` (640MiB = ~60% of container limit) to bound heap growth

**How to apply:** If memory issues recur at higher load or on a new environment, next tier is 0.75/1.5Gi. AOT and trimming are not viable — Wolverine uses Roslyn codegen and reflection. GCHeapHardLimit should be ~60% of container memory limit to leave room for native allocations and thread stacks.

**Valid consumption plan CPU/memory pairs:** 0.25/0.5, 0.5/1, 0.75/1.5, 1/2, 1.25/2.5, 1.5/3, 1.75/3.5, 2/4 (all in Gi).
