# PicoEntityStore Performance Report (120 FPS)

This report analyzes the performance of `PicoEntityStore` methods in the context of a high-performance game running at **120 FPS**.

## Frame Budget
*   **Target Frame Rate:** 120 FPS
*   **Frame Time (Budget):** ~8.33 ms (8,333,333 ns)

## Methodology
Benchmarks were executed using BenchmarkDotNet on a 13th Gen Intel Core i9-13980HX. The results below show how many times the specific benchmark method (including any internal loops) can execute within the 8.33ms frame budget.

| Method | Entity Count | Mean Execution Time | Ops per Frame (120 FPS) |
| :--- | :--- | :--- | :--- |
| **Add** (Bulk Add) | 1,000 | 29.94 µs | **278** |
| **Add** (Bulk Add) | 10,000 | 498.81 µs | **16** |
| **Get\<T\>** (Single Get) | 1,000 | 10.29 ns | **809,847** |
| **Get\<T\>** (Single Get) | 10,000 | 10.32 ns | **807,493** |
| **Descendants** (100 depth) | 100 | 1.03 µs | **8,092** |
| **All** (Retrieve all) | 1,000 | 329.83 ns | **25,265** |
| **All** (Retrieve all) | 10,000 | 3.17 µs | **2,628** |
| **All\<T\>** (Type Filter) | 1,000 | 1.17 µs | **7,134** |
| **All\<T\>** (Type Filter) | 10,000 | 9.14 µs | **911** |
| **First\<T\>** (Generic) | N/A | 11.28 ns | **738,770** |
| **ForEach** (Lambda) | 1,000 | 470.28 ns | **17,719** |
| **ForEach** (Lambda) | 10,000 | 3.88 µs | **2,145** |
| **ForEach\<T\>** | 1,000 | 315.23 ns | **26,435** |
| **ForEach\<T\>** | 10,000 | 3.07 µs | **2,716** |
| **Remove** (Bulk) | 100* | 18.30 µs | **455** |
| **Remove** (Bulk) | 100* | 23.64 µs | **352** |

*\*Note: Remove in the benchmark always removes 100 entities regardless of the PicoEntityCount parameter setup.*

## Key Observations
1.  **Extreme Efficiency:** Querying the first entity of a type (`First<T>`) is incredibly fast, allowing for over 700,000 calls per frame.
2.  **Scalability:** Iterating over 10,000 entities (`ForEach`) takes less than 4 µs, meaning you could perform approximately 2,000 full-world iterations per frame.
3.  **Hierarchy Traversal:** Descendant traversal for a hierarchy of 100 deep is very efficient, allowing over 8,000 full traversals per frame.
4.  **Bulk Operations:** Even bulk-adding 10,000 entities only consumes about 6% of a single 120 FPS frame (0.5ms).
