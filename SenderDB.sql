--**SYNC SENDER DB SCHEMA**
CREATE PROCEDURE [usp_AddSyncItem](@System VARCHAR(50), @Table VARCHAR(255), @SQL NVARCHAR(MAX), @SyncDatestamp DATETIME) AS
DECLARE @SystemId INT
DECLARE @SyncId BIGINT

SELECT @SystemId = SystemId FROM SyncSystems WHERE SystemName = @System
IF @SystemId IS NULL
	RETURN -- can't process what we don't know about, but don't error on client

INSERT INTO SyncMaster (SystemId, SyncDatestamp)
	SELECT @SystemId, @SyncDatestamp

SELECT @SyncId = SCOPE_IDENTITY()

INSERT INTO SyncProcessing(SyncId, SyncSystemEndpointId)
SELECT @SyncID, SyncSystemEndpointId FROM SyncSystemEndpoints WHERE SystemId = @SystemId

DECLARE @SyncPackage XML
SET @SyncPackage= (SELECT SyncId = @SyncId, SyncDatestamp = @SyncDatestamp, TableName = @Table, SQLStatement = @SQL FOR XML PATH('SyncPackage'),ELEMENTS XSINIL, BINARY BASE64)
UPDATE SyncMaster SET SyncPackage = @SyncPackage WHERE SyncId = @SyncId

GO

CREATE PROCEDURE [usp_GetNextDataItem] AS

DECLARE @SyncProcessingId BIGINT

SELECT TOP 1 @SyncProcessingId = SyncProcessingId 
FROM SyncMaster 
INNER JOIN SyncProcessing ON SyncMaster.SyncId = SyncProcessing.SyncId
INNER JOIN SyncSystemEndpoints ON SyncSystemEndpoints.SyncSystemEndpointId = SyncProcessing.SyncSystemEndpointId
WHERE ProcessedState = 0 AND IsActive = 1
ORDER BY SyncDatestamp, ProcessOrder

--lock it
exec usp_SetProcessedState @SyncProcessingId, 1

--return transfer package
SELECT
SyncProcessingId,
SystemName,
Description AS EndpointDescription,
SyncPackage,
EndpointUrl
FROM 
SyncMaster
INNER JOIN SyncProcessing ON SyncMaster.SyncId = SyncProcessing.SyncId
INNER JOIN SyncSystems ON SyncMaster.SystemId = SyncSystems.SystemId
INNER JOIN SyncSystemEndpoints ON SyncSystemEndpoints.SyncSystemEndpointId = SyncProcessing.SyncSystemEndpointId
WHERE 
SyncProcessingId = @SyncProcessingId
AND SyncSystemEndpoints.IsActive = 1


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
FROM SyncMaster 
INNER JOIN SyncProcessing ON SyncMaster.SyncId = SyncProcessing.SyncId
INNER JOIN SyncSystemEndpoints ON SyncSystemEndpoints.SyncSystemEndpointId = SyncProcessing.SyncSystemEndpointId
WHERE ProcessedState = 0 AND IsActive = 1
ORDER BY SyncDatestamp, ProcessOrder

--return transfer package
SELECT
SyncProcessingId,
SystemName,
Description AS EndpointDescription,
SyncPackage,
EndpointUrl
FROM 
SyncMaster
INNER JOIN SyncProcessing ON SyncMaster.SyncId = SyncProcessing.SyncId
INNER JOIN SyncSystems ON SyncMaster.SystemId = SyncSystems.SystemId
INNER JOIN SyncSystemEndpoints ON SyncSystemEndpoints.SyncSystemEndpointId = SyncProcessing.SyncSystemEndpointId
WHERE 
SyncProcessingId = @SyncProcessingId
AND SyncSystemEndpoints.IsActive = 1

GO

CREATE TABLE [SyncMaster](
	[SyncId] [bigint] IDENTITY(1,1) NOT NULL,
	[SystemId] [int] NOT NULL,
	[SyncDatestamp] [datetime] NOT NULL,
	[SyncPackage] [xml] NULL,
 CONSTRAINT [PK_SyncMaster] PRIMARY KEY CLUSTERED 
(
	[SyncId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

CREATE TABLE [SyncProcessing](
	[SyncProcessingId] [bigint] IDENTITY(1,1) NOT NULL,
	[SyncId] [bigint] NOT NULL,
	[SyncSystemEndpointId] [int] NOT NULL,
	[ProcessingInfo] NVARCHAR(MAX) NULL,
	[ProcessedState] [int] NOT NULL,
	[ProcessedWhen] [datetime] NULL,
 CONSTRAINT [PK_SyncProcessing] PRIMARY KEY CLUSTERED 
(
	[SyncProcessingId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

CREATE TABLE [SyncSystemEndpoints](
	[SyncSystemEndpointId] [int] IDENTITY(1,1) NOT NULL,
	[SystemId] [int] NOT NULL,
	[Description] [varchar](50) NOT NULL,
	[ProcessOrder] [int] NOT NULL,
	[EndpointUrl] [nvarchar](1000) NOT NULL,
	[IsActive] [bit] NOT NULL,
 CONSTRAINT [PK_SyncSystemEndpoints] PRIMARY KEY CLUSTERED 
(
	[SyncSystemEndpointId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

CREATE TABLE [SyncSystems](
	[SystemId] [int] IDENTITY(1,1) NOT NULL,
	[SystemName] [varchar](50) NOT NULL,
 CONSTRAINT [PK_SyncSystems] PRIMARY KEY CLUSTERED 
(
	[SystemId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
CREATE UNIQUE NONCLUSTERED INDEX [NonClusteredIndex-20140302-131039] ON [SyncProcessing]
(
	[SyncId] ASC,
	[SyncSystemEndpointId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE [SyncMaster] ADD  CONSTRAINT [DF_SyncMaster_SyncDatestamp]  DEFAULT (getdate()) FOR [SyncDatestamp]
GO
ALTER TABLE [SyncProcessing] ADD  CONSTRAINT [DF_SyncProcessing_ProcessedState]  DEFAULT ((0)) FOR [ProcessedState]
GO
ALTER TABLE [SyncSystemEndpoints] ADD  CONSTRAINT [DF_SyncSystemEndpoints_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [SyncMaster]  WITH CHECK ADD  CONSTRAINT [FK_SyncMaster_SyncSystems] FOREIGN KEY([SystemId])
REFERENCES [SyncSystems] ([SystemId])
GO
ALTER TABLE [SyncMaster] CHECK CONSTRAINT [FK_SyncMaster_SyncSystems]
GO
ALTER TABLE [SyncSystemEndpoints]  WITH CHECK ADD  CONSTRAINT [FK_SyncSystemEndpoints_SyncSystems] FOREIGN KEY([SystemId])
REFERENCES [SyncSystems] ([SystemId])
GO
ALTER TABLE [SyncSystemEndpoints] CHECK CONSTRAINT [FK_SyncSystemEndpoints_SyncSystems]
GO


