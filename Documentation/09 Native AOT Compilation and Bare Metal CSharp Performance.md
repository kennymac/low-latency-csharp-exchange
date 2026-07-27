# Native AOT Compilation & Bare-Metal C# Performance

This document details how **Native AOT** (Ahead-of-Time compilation) works in modern .NET 8/9/10, why it is critical for high-frequency trading engines, and how to publish Native AOT binaries across operating systems.

---

## 1. Why Native AOT for Low-Latency C#?

In standard .NET runtime environments, the Just-In-Time (JIT) compiler compiles C# Intermediate Language (IL) byte-code into machine code on-the-fly while the application is running.

While RyuJIT is fast, JIT compilation introduces three major issues for HFT systems:
1. **Tiering Jitter:** RyuJIT compiles methods in tiers (Tier 0 interpret/quick-JIT, Tier 1 optimized JIT). When a market order burst occurs, methods suddenly re-JIT, causing unpredictable microsecond latency spikes.
2. **Cold-Start Delays:** JIT loading and metadata parsing delays startup by hundreds of milliseconds.
3. **Memory Footprint:** The JIT compiler and runtime metadata consume megabytes of RAM memory overhead.

### Native AOT Solution
**Native AOT (`<PublishAot>true</PublishAot>`)** compiles C# code ahead-of-time directly into native machine code (`.o` / binary executable), producing an autonomous, standalone native binary with **no JIT compiler loaded at runtime**:
* **0 JIT compilation stalls:** 100% of code is fully optimized native assembly before launch.
* **Instant Startup:** Launch time drops to **< 5 milliseconds**.
* **C++ / Rust Equivalence:** Executes at bare-metal native CPU speed.

---

## 2. Native AOT Command Reference

To publish Native AOT binaries across platforms:

```bash
# Publish Native AOT binary for macOS ARM64 (Apple Silicon M1/M2/M3/M4)
dotnet publish LowLatency.ScratchPad.Engine/LowLatency.ScratchPad.Engine.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained \
  /p:PublishAot=true

# Publish Native AOT binary for macOS Intel x64
dotnet publish LowLatency.ScratchPad.Engine/LowLatency.ScratchPad.Engine.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained \
  /p:PublishAot=true

# Publish Native AOT binary for Linux x64 (Production HFT Server Target)
dotnet publish LowLatency.ScratchPad.Engine/LowLatency.ScratchPad.Engine.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained \
  /p:PublishAot=true
```

---

## 3. Project Configuration (`.csproj`)

To enable Native AOT natively in `LowLatency.ScratchPad.Engine.csproj`:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <OptimizationPreference>Speed</OptimizationPreference>
  <InvariantGlobalization>true</InvariantGlobalization>
  <StackTraceSupport>false</StackTraceSupport>
</PropertyGroup>
```

---

## 4. Code Compatibility Checklist

Native AOT enforces a strict reflection-free compilation contract:

| C# Feature | Native AOT Compatible? | Our Engine Implementation |
| :--- | :--- | :--- |
| **Static Generic Constraints** | ✅ YES | `SpscRingBuffer<T>`, `MemPool<T>` |
| **Struct Memory Offsets** | ✅ YES | `[StructLayout(LayoutKind.Explicit)]` |
| **Span & MemoryMarshal** | ✅ YES | `MemoryMarshal.Read<ClientRequest>` |
| **Value Structs & `in` parameters** | ✅ YES | `LogEntry`, `ClientResponse` |
| **System.Reflection.Emit** | ❌ NO | **0 reflection used anywhere** |
| **Runtime Code Generation** | ❌ NO | **0 dynamic code emission** |

Our entire codebase is 100% Native AOT compliant out of the box!
