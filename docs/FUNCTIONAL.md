# Statistics Parser SSMS Extension
Statistics Parser Extension is a SQL Server Management Studio (SSMS) extension that turns raw SQL Server diagnostic output into readable tables. 

## How to Use

Right-click anywhere in the Messages tab and select **Parse Statistics** from the context menu. The Stats Parser tab opens (or activates if already open) next to the Messages tab and displays the parsed results.

If the Messages tab is empty or contains no recognizable statistics output, the Stats Parser tab displays an empty state message.

The Stats Parser tab respects the active SSMS color theme (light, dark, and blue).

## Input
The extension takes the Messages tab output and opens a tab named **Stats Parser** next to the Messages tab.

### Messages Tab Output Example (Single Statement)
(100 rows affected)
Table 'Posts'. Scan count 1, logical reads 32, physical reads 3, page server reads 0, read-ahead reads 1957, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.

 SQL Server Execution Times:
   CPU time = 0 ms,  elapsed time = 959 ms.

Completion time: 2026-04-27T15:33:34.6405733-04:00

### Stats Parser Tab Output Example (Single Statement)
**100 rows affected**

| Row Num | Table | Scan Count | Logical Reads | Physical Reads | Page Server Reads | Read-Ahead Reads | Page Server Read-Ahead Reads | LOB Logical Reads | LOB Physical Reads | LOB Page Server Reads | LOB Read-Ahead Reads | LOB Page Server Read-Ahead Reads | % Logical Reads of Total Reads |
|---------|-------|------------|---------------|----------------|-------------------|------------------|------------------------------|-------------------|--------------------|------------------------|----------------------|----------------------------------|-------------------------------|
| 1       | Posts | 1          | 32            | 3              | 0                 | 1957             | 0                            | 0                 | 0                  | 0                      | 0                    | 0                                | 100.000%                      |
|         | Total | 1          | 32            | 3              | 0                 | 1957             | 0                            | 0                 | 0                  | 0                      | 0                    | 0                                |                               |

|                            | CPU        | Elapsed     |
|----------------------------|------------|-------------|
| SQL Server Execution Times | 00:00:00.000 | 00:00:00.959 |

Completion time: 2026-04-27T15:33:34.6405733-04:00

**Totals:**

| Table | Scan Count | Logical Reads | Physical Reads | Page Server Reads | Read-Ahead Reads | Page Server Read-Ahead Reads | LOB Logical Reads | LOB Physical Reads | LOB Page Server Reads | LOB Read-Ahead Reads | LOB Page Server Read-Ahead Reads | % Logical Reads of Total Reads |
|-------|------------|---------------|----------------|-------------------|------------------|------------------------------|-------------------|--------------------|------------------------|----------------------|----------------------------------|-------------------------------|
| Posts | 1          | 32            | 3              | 0                 | 1957             | 0                            | 0                 | 0                  | 0                      | 0                    | 0                                | 100.000%                      |
| Total | 1          | 32            | 3              | 0                 | 1957             | 0                            | 0                 | 0                  | 0                      | 0                    | 0                                |                               |

|                                      | CPU        | Elapsed     |
|--------------------------------------|------------|-------------|
| SQL Server parse and compile time    | 00:00:00.000 | 00:00:00.000 |
| SQL Server Execution Times           | 00:00:00.000 | 00:00:00.959 |
| Total                                | 00:00:00.000 | 00:00:00.959 |

### Messages Tab Output Example (Multiple Statements)
(100 rows affected)
Table 'Posts'. Scan count 1, logical reads 32, physical reads 3, page server reads 0, read-ahead reads 1957, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.
Table 'Users'. Scan count 1, logical reads 8, physical reads 0, page server reads 0, read-ahead reads 0, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.

 SQL Server Execution Times:
   CPU time = 5 ms,  elapsed time = 25 ms.

Completion time: 2026-04-27T15:33:35.0000000-04:00

(50 rows affected)
Table 'Comments'. Scan count 2, logical reads 64, physical reads 0, page server reads 0, read-ahead reads 0, page server read-ahead reads 0, lob logical reads 0, lob physical reads 0, lob page server reads 0, lob read-ahead reads 0, lob page server read-ahead reads 0.

 SQL Server Execution Times:
   CPU time = 3 ms,  elapsed time = 15 ms.

Completion time: 2026-04-27T15:33:35.3000000-04:00

### Stats Parser Tab Output Example (Multiple Statements)
**100 rows affected**

| Row Num | Table | Scan Count | Logical Reads | Physical Reads | Read-Ahead Reads | % Logical Reads of Total Reads |
|---------|-------|------------|---------------|----------------|------------------|-------------------------------|
| 1       | Posts | 1          | 32            | 3              | 1957             | 80.000%                       |
| 2       | Users | 1          | 8             | 0              | 0                | 20.000%                       |
|         | Total | 2          | 40            | 3              | 1957             |                               |

|                            | CPU          | Elapsed      |
|----------------------------|--------------|--------------|
| SQL Server Execution Times | 00:00:00.005 | 00:00:00.025 |

Completion time: 2026-04-27T15:33:35.0000000-04:00

**50 rows affected**

| Row Num | Table    | Scan Count | Logical Reads | Physical Reads | Read-Ahead Reads | % Logical Reads of Total Reads |
|---------|----------|------------|---------------|----------------|------------------|-------------------------------|
| 1       | Comments | 2          | 64            | 0              | 0                | 100.000%                      |
|         | Total    | 2          | 64            | 0              | 0                |                               |

|                            | CPU          | Elapsed      |
|----------------------------|--------------|--------------|
| SQL Server Execution Times | 00:00:00.003 | 00:00:00.015 |

Completion time: 2026-04-27T15:33:35.3000000-04:00

**Totals:**

| Table    | Scan Count | Logical Reads | Physical Reads | Read-Ahead Reads | % Logical Reads of Total Reads |
|----------|------------|---------------|----------------|------------------|-------------------------------|
| Comments | 2          | 64            | 0              | 0                | 61.538%                       |
| Posts    | 1          | 32            | 3              | 1957             | 30.769%                       |
| Users    | 1          | 8             | 0              | 0                | 7.692%                        |
| Total    | 4          | 104           | 3              | 1957             |                               |

|                            | CPU          | Elapsed      |
|----------------------------|--------------|--------------|
| SQL Server Execution Times | 00:00:00.008 | 00:00:00.040 |
| Total                      | 00:00:00.008 | 00:00:00.040 |

## IO Statistics Output

For each block of `STATISTICS IO` output, the tool produces a sortable table with one row per table. Columns shown depend on what SQL Server reported, and may include:

| Column | What It Means |
|--------|---------------|
| Row Num | Order the table appeared within the current statement; resets to 1 for each new statement |
| Table | Table name |
| Scan Count | Number of times the table was scanned |
| Logical Reads | Pages read from the buffer cache |
| Physical Reads | Pages read from disk |
| Read-Ahead Reads | Pages pre-fetched from disk |
| Page Server Reads | Pages read from Azure SQL page server |
| Page Server Read-Ahead Reads | Pre-fetched pages from Azure SQL page server |
| LOB Logical Reads | Large object pages read from cache |
| LOB Physical Reads | Large object pages read from disk |
| LOB Read-Ahead Reads | Large object pages pre-fetched |
| LOB Page Server Reads | LOB pages from Azure SQL page server |
| LOB Page Server Read-Ahead Reads | LOB pre-fetched pages from Azure SQL page server |
| Segment Reads | Columnstore segments read |
| Segment Skipped | Columnstore segments skipped (eliminated) |
| % Logical Reads of Total Reads | Each table's share of total logical reads for that statement |

Each table also shows a **totals row** at the bottom summing all numeric columns.

## Time Statistics Output

For each block of `STATISTICS TIME` output, the tool shows a small table with:

- **CPU time** — processor time used (formatted as hh:mm:ss.ms)
- **Elapsed time** — wall-clock time (formatted as hh:mm:ss.ms)

Separate rows are shown for **parse/compile time** and **execution time**. If SQL Server emits a summary time row that matches the sum of the others, the tool detects it and marks it as a summary so it is not double-counted.

## Totals Section

At the bottom of all results, a **Totals** section provides cross-statement aggregates:

- **Grand IO total** — one row per unique table name, aggregated across all statements in the pasted output, with logical-read percentages recalculated against the overall total
- **Time total** — compile time and execution time summed across all statements, with a grand total row

## Other Output Elements

Alongside the tables, the tool also surfaces:

- **Rows affected** — displayed inline wherever it appears in the output
- **Error messages** — SQL Server `Msg` errors are shown in red text
- **Completion time** — statement completion timestamps, formatted in a locale-aware way
