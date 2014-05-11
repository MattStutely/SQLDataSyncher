--**SYNC RECEIVER DB SCHEMA**
CREATE PROCEDURE [usp_GetEndpoint] @System VARCHAR(50), @Endpoint VARCHAR(50) AS

SELECT SyncSystemEndpointId
FROM SyncSystemEndpoints 
WHERE SystemName = @System AND EndpointName = @Endpoint

GO

CREATE PROCEDURE [usp_GetNextDataItem] AS

DECLARE @SyncProcessingId BIGINT

SELECT TOP 1 @SyncProcessingId = SyncProcessingId 
FROM SyncProcessing
INNER JOIN SyncSystemEndpoints ON SyncProcessing.SyncSystemEndpointId = SyncSystemEndpoints.SyncSystemEndpointId 
WHERE ProcessedState = 0 AND IsActive = 1
ORDER BY SyncDatestamp

--lock it
exec usp_SetProcessedState @SyncProcessingId, 1

--return transfer package
SELECT
SyncProcessingId,
DbConStr,
TableName,
SQLStatement
FROM 
SyncProcessing
INNER JOIN SyncSystemEndpoints ON SyncSystemEndpoints.SyncSystemEndpointId = SyncProcessing.SyncSystemEndpointId
WHERE 
SyncProcessingId = @SyncProcessingId

GO


CREATE PROCEDURE [usp_SetProcessedState] @SyncProcessingId BIGINT, @State INT, @Info NVARCHAR(MAX) = NULL AS
UPDATE SyncProcessing SET ProcessedState = @State, ProcessingInfo = @Info, ProcessedWhen=GETDATE() WHERE SyncProcessingId = @SyncProcessingId
--if we've gone to error, lock the endpoint so nothing else can be sent until fixed to prevent things going askew
IF @State = 3
BEGIN
	DECLARE @EndpointId INT
	SELECT @EndpointId = SyncSystemEndpointId FROM SyncProcessing WHERE SyncProcessingId = @SyncProcessingId
	UPDATE SyncSystemEndpoints SET IsActive = 0 WHERE SyncSystemEndpointId = @EndpointId
END
GO


CREATE PROCEDURE [usp_SniffNextDataItem] AS

DECLARE @SyncProcessingId BIGINT

SELECT TOP 1 @SyncProcessingId = SyncProcessingId 
FROM SyncProcessing
INNER JOIN SyncSystemEndpoints ON SyncProcessing.SyncSystemEndpointId = SyncSystemEndpoints.SyncSystemEndpointId 
WHERE ProcessedState = 0 AND IsActive = 1
ORDER BY SyncDatestamp

--return transfer package
SELECT
SyncProcessingId,
DbConStr,
TableName,
SQLStatement
FROM 
SyncProcessing
INNER JOIN SyncSystemEndpoints ON SyncSystemEndpoints.SyncSystemEndpointId = SyncProcessing.SyncSystemEndpointId
WHERE 
SyncProcessingId = @SyncProcessingId

GO

CREATE PROCEDURE [usp_WriteSyncPackage](@SyncId BIGINT, @EndpointId INT, @Datestamp DATETIME, @TableName varchar(255), @SQL nvarchar(MAX)) AS
INSERT INTO SyncProcessing
(ClientSyncId,
SyncSystemEndpointId,
SyncDatestamp,
ReceivedWhen,
ProcessedState,
TableName,
SQLStatement)
SELECT @SyncId, @EndpointId, @Datestamp, getdate(), 0, @TableName, @SQL

GO

CREATE TABLE [SyncProcessing](
	[SyncProcessingId] [bigint] IDENTITY(1,1) NOT NULL,
	[SyncSystemEndpointId] [int] NOT NULL,
	[ClientSyncId] [bigint] NOT NULL,
	[SyncDatestamp] [datetime] NOT NULL,
	[ReceivedWhen] [datetime] NOT NULL,
	[ProcessedState] [int] NOT NULL,
	[ProcessedWhen] [datetime] NULL,
	[TableName] [varchar](255) NOT NULL,
	[SQLStatement] [nvarchar](max) NOT NULL,
	[ProcessingInfo] [nvarchar](max) NULL
 CONSTRAINT [PK_SyncProcessing] PRIMARY KEY CLUSTERED 
(
	[SyncProcessingId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

CREATE TABLE [SyncSystemEndpoints](
	[SyncSystemEndpointId] [int] IDENTITY(1,1) NOT NULL,
	[SystemName] [varchar](50) NOT NULL,
	[EndpointName] [varchar](50) NOT NULL,
	[DbConStr] [nvarchar](1000) NOT NULL,
	[IsActive] [bit] NOT NULL,
 CONSTRAINT [PK_SyncSystemEndpoints] PRIMARY KEY CLUSTERED 
(
	[SyncSystemEndpointId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
CREATE UNIQUE NONCLUSTERED INDEX [UI_SyncProcessing] ON [SyncProcessing]
(
	[SyncSystemEndpointId] ASC,
	[ClientSyncId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE [SyncProcessing] ADD  CONSTRAINT [DF_SyncProcessing_ReceivedWhen]  DEFAULT (getdate()) FOR [ReceivedWhen]
GO
ALTER TABLE [SyncSystemEndpoints] ADD  CONSTRAINT [DF_SyncSystemEndpoints_IsActive]  DEFAULT (1) FOR [IsActive]
GO
ALTER TABLE [SyncProcessing]  WITH CHECK ADD  CONSTRAINT [FK_SyncProcessing_SyncSystemEndpoints] FOREIGN KEY([SyncSystemEndpointId])
REFERENCES [SyncSystemEndpoints] ([SyncSystemEndpointId])
GO
ALTER TABLE [SyncProcessing] CHECK CONSTRAINT [FK_SyncProcessing_SyncSystemEndpoints]
GO

CREATE PROCEDURE usp_Stats AS

SELECT SystemName, EndpointName, IsActive, QueueSize, LastMessageReceived, LastMessageProcessed, LastError
FROM SyncSystemEndpoints
CROSS APPLY (SELECT COUNT(SyncProcessingId) AS QueueSize, MAX(ReceivedWhen) AS LastMessageReceived FROM SyncProcessing WHERE SyncProcessing.SyncSystemEndpointId = SyncSystemEndpointId AND ProcessedState = 0) unprocessed
CROSS APPLY (SELECT MAX(ProcessedWhen) AS LastMessageProcessed FROM SyncProcessing WHERE SyncProcessing.SyncSystemEndpointId = SyncSystemEndpointId AND ProcessedState = 2) processed
CROSS APPLY (SELECT MAX(ProcessedWhen) AS LastError FROM SyncProcessing WHERE SyncProcessing.SyncSystemEndpointId = SyncSystemEndpointId AND ProcessedState = 3) error
GO
