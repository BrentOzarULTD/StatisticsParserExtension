
-- Temp table

SET STATISTICS IO, TIME ON;
GO

-- Create a temp table with some data
IF OBJECT_ID('tempdb..#Orders') IS NOT NULL DROP TABLE #Orders;

CREATE TABLE #Orders (
    OrderID     INT IDENTITY(1,1) PRIMARY KEY,
    CustomerID  INT NOT NULL,
    OrderDate   DATETIME2 NOT NULL,
    Amount      DECIMAL(18,2) NOT NULL,
    Notes       NVARCHAR(400) NULL
);

-- Populate ~50k rows so IO is non-trivial
INSERT INTO #Orders (CustomerID, OrderDate, Amount, Notes)
SELECT TOP (50000)
    ABS(CHECKSUM(NEWID())) % 1000,
    DATEADD(MINUTE, -1 * (ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), SYSDATETIME()),
    CAST((ABS(CHECKSUM(NEWID())) % 100000) / 100.0 AS DECIMAL(18,2)),
    REPLICATE(N'x', 200)
FROM sys.all_objects a CROSS JOIN sys.all_objects b;
GO

-- Query 1: clustered index scan with aggregation
SELECT CustomerID, COUNT(*) AS Orders, SUM(Amount) AS Total
FROM #Orders
GROUP BY CustomerID
ORDER BY Total DESC;

-- Query 2: filter + sort, joins to a system view to add a second table to the IO output
SELECT TOP (100) o.OrderID, o.CustomerID, o.Amount, o.OrderDate, t.name AS TypeName
FROM #Orders AS o
CROSS JOIN sys.types AS t
WHERE o.Amount > 500
ORDER BY o.OrderDate DESC;
GO

DROP TABLE #Orders;
GO


-- Summary Row
SET STATISTICS IO, TIME ON;
GO

SELECT TOP 100 p.Id, u.DisplayName
FROM Posts p JOIN Users u ON p.OwnerUserId = u.Id;
SELECT TOP 50 * FROM Comments;

-- Error Row
SET STATISTICS IO, TIME ON;
SELECT TOP 10 * FROM Posts;
RAISERROR('boom', 16, 1);