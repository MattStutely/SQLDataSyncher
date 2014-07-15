--**SYNC TRIGGER**
--this will create triggers on each table to sync, you can control this by changing the WHERE clause that populates the table variable at the start to omit other tables other than just sysdiagrams
DECLARE @SQL varchar(max)
DECLARE @WHERESQL varchar(max)
DECLARE @TABLE_NAME sysname
DECLARE @PKColList TABLE(ROWID INT IDENTITY(1,1), COLUMN_NAME VARCHAR(255), DATA_TYPE VARCHAR(50))
DECLARE @COUNTER INT
DECLARE @MAXROWS INT
DECLARE @PKCOUNTER INT
DECLARE @PKMAXROWS INT
DECLARE @PKMIN INT

SELECT @COUNTER = 1
SET NOCOUNT ON

DECLARE @TABLELIST TABLE (ROWID INT IDENTITY(1,1), TABLE_NAME VARCHAR(255))

INSERT INTO @TABLELIST(TABLE_NAME) 
	SELECT TABLE_NAME FROM INFORMATION_SCHEMA.Tables
	WHERE TABLE_TYPE= 'BASE TABLE'
	AND TABLE_NAME NOT IN ('sysdiagrams')

SELECT @MAXROWS = COUNT(*) FROM @TABLELIST

WHILE @COUNTER<=@MAXROWS
BEGIN

SELECT @TABLE_NAME = TABLE_NAME FROM @TABLELIST WHERE ROWID = @COUNTER

--set up the pk columns
DELETE FROM @PKColList
INSERT INTO @PKColList(COLUMN_NAME, DATA_TYPE)
SELECT cu.COLUMN_NAME, col.DATA_TYPE +
CASE WHEN CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN 
	'(' + 
	CASE WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN 
		'MAX' 
	ELSE 
		CAST(CHARACTER_MAXIMUM_LENGTH AS VARCHAR) 
	END 
	+ ')'
ELSE
	''
END 
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE cu 
INNER JOIN INFORMATION_SCHEMA.COLUMNS col ON cu.COLUMN_NAME = col.COLUMN_NAME AND col.TABLE_NAME = @TABLE_NAME
WHERE EXISTS ( SELECT tc.* FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc WHERE tc.TABLE_NAME = @TABLE_NAME AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME )

SELECT @PKMIN=MIN(ROWID), @PKMAXROWS = MAX(ROWID) FROM @PKColList
	
EXEC('IF OBJECT_ID (''' + @TABLE_NAME+ '_SyncData'', ''TR'') IS NOT NULL DROP TRIGGER ' + @TABLE_NAME+ '_SyncData')
SET @SQL =
'
CREATE TRIGGER ' + @TABLE_NAME+ '_SyncData ON ' + @TABLE_NAME+ ' FOR INSERT, UPDATE, DELETE
AS
SET NOCOUNT ON
DECLARE
@Table_Name varchar(128) ,
@Data xml,
@SQL nvarchar(max),
@Type char(1),
@RowId INT,
@RowCount INT,
@UpdatedWhen DATETIME,
'
SET @PKCOUNTER = @PKMIN
WHILE @PKCOUNTER <= @PKMAXROWS
BEGIN
	IF @PKCOUNTER>@PKMIN SET @SQL = @SQL + ',
	'
	SELECT @SQL = @SQL + '@' + COLUMN_NAME + 'Val ' + DATA_TYPE FROM @PKColList WHERE ROWID = @PKCOUNTER
	SET @PKCOUNTER = @PKCOUNTER + 1
END
SET @SQL = @SQL + '

SELECT @Table_Name = ''' + @TABLE_NAME+ '''

--need to handle multiple rows
DECLARE @MyRows TABLE (RowID INT IDENTITY(1,1), '
SET @PKCOUNTER = @PKMIN
WHILE @PKCOUNTER <= @PKMAXROWS
BEGIN
	IF @PKCOUNTER>@PKMIN SET @SQL = @SQL + ', '
	SELECT @SQL = @SQL + COLUMN_NAME + 'Val ' + DATA_TYPE FROM @PKColList WHERE ROWID = @PKCOUNTER
	SET @PKCOUNTER = @PKCOUNTER + 1
END
SET @SQL = @SQL + ')
INSERT INTO @MyRows('
SET @PKCOUNTER = @PKMIN
WHILE @PKCOUNTER <= @PKMAXROWS
BEGIN
	IF @PKCOUNTER>@PKMIN SET @SQL = @SQL + ', '
	SELECT @SQL = @SQL + COLUMN_NAME + 'Val' FROM @PKColList WHERE ROWID = @PKCOUNTER
	SET @PKCOUNTER = @PKCOUNTER + 1
END
SET @SQL = @SQL + ') 
SELECT '
SET @PKCOUNTER = @PKMIN
WHILE @PKCOUNTER <= @PKMAXROWS
BEGIN
	IF @PKCOUNTER>@PKMIN SET @SQL = @SQL + ', '
	SELECT @SQL = @SQL + COLUMN_NAME FROM @PKColList WHERE ROWID = @PKCOUNTER
	SET @PKCOUNTER = @PKCOUNTER + 1
END
SET @SQL = @SQL + ' FROM inserted
UNION
SELECT '
SET @PKCOUNTER = @PKMIN
WHILE @PKCOUNTER <= @PKMAXROWS
BEGIN
	IF @PKCOUNTER>@PKMIN SET @SQL = @SQL + ', '
	SELECT @SQL = @SQL + COLUMN_NAME FROM @PKColList WHERE ROWID = @PKCOUNTER
	SET @PKCOUNTER = @PKCOUNTER + 1
END
SET @SQL = @SQL + ' FROM deleted
SELECT @RowCount = MAX(RowID) FROM @MyRows
SET @RowId = 1

WHILE @RowID <= @RowCount
BEGIN
	-- date and user
	SELECT @UpdatedWhen = getdate()

	--pk data for getting xml and whereclause for use later
	SELECT '
	SET @WHERESQL = ''
	SET @PKCOUNTER = @PKMIN
	WHILE @PKCOUNTER <= @PKMAXROWS
	BEGIN
		IF @PKCOUNTER>@PKMIN SET @SQL = @SQL + ', '
		IF @PKCOUNTER>@PKMIN SET @WHERESQL = @WHERESQL + ' AND '
		SELECT @SQL = @SQL + '@' + COLUMN_NAME + 'Val' + ' = ' + COLUMN_NAME + 'Val' FROM @PKColList WHERE ROWID = @PKCOUNTER
		SELECT @WHERESQL = @WHERESQL + COLUMN_NAME + ' = @' + COLUMN_NAME + 'Val' FROM @PKColList WHERE ROWID = @PKCOUNTER
		SET @PKCOUNTER = @PKCOUNTER + 1
	END
	SET @SQL = @SQL + ' FROM @MyRows WHERE RowId = @RowId

	-- Action
	IF EXISTS (SELECT 1 FROM deleted WHERE ' + @WHERESQL +')
	BEGIN
		IF EXISTS (SELECT 1 FROM inserted WHERE ' + @WHERESQL +')
			SELECT @Type = ''U''
		ELSE
			SELECT @Type = ''D''
	END
	ELSE
		SELECT @Type = ''I''
	
	IF @Type = ''D''
		SET @Data = (SELECT * FROM deleted AS DataChange WHERE '
	SET @SQL = @SQL + @WHERESQL + ' FOR XML AUTO,ELEMENTS,BINARY BASE64)
	ELSE
		SET @Data = (SELECT * FROM inserted AS DataChange WHERE '
	SET @SQL = @SQL + @WHERESQL + ' FOR XML AUTO,ELEMENTS,BINARY BASE64)
	
	exec usp_CreateSyncItem @Table_Name, NULL, @Data, @Type, @UpdatedWhen --at replace NULL with a comma delimited string of columns you do not wish to sync

	SET @RowId = @RowId + 1
END
'
EXEC(@SQL)
SET @COUNTER = @COUNTER + 1
END
GO


--**SYNC STORED PROCEDURE**
--this proc needed in each db that you want to sync, trigger will call it to write to sync db
CREATE PROCEDURE [dbo].[usp_CreateSyncItem](@TableName VARCHAR(255), @IgnoreCols VARCHAR(MAX) = NULL, @ChangeXML XML = NULL, @UpdateType CHAR(1), @UpdatedWhen DATETIME) AS
SET NOCOUNT ON
DECLARE @System VARCHAR(50)
SET @System = 'LordsDigest' -- change to specify the system you want this to be called across the sync process

--build entry to data queue
DECLARE @InsertSQL NVARCHAR(MAX)
DECLARE @InsertCols NVARCHAR(MAX)
DECLARE @InsertValues NVARCHAR(MAX)
DECLARE @UpdateSQL NVARCHAR(MAX)
DECLARE @ExecSQL NVARCHAR(MAX)
DECLARE @WhereSQL NVARCHAR(MAX)

DECLARE @PKColList TABLE(COLUMN_NAME VARCHAR(255), DATA_TYPE VARCHAR(50))
DECLARE @IgnoreColList TABLE(COLUMN_NAME VARCHAR(255))
DECLARE @RowID INT
DECLARE @MaxRowID INT
DECLARE @ColName VARCHAR(255)
DECLARE @ColDT VARCHAR(50)
DECLARE @Value NVARCHAR(MAX)
DECLARE @XValue XML

DECLARE @Quote CHAR(1)
SELECT @Quote = ''''

INSERT INTO @PKColList(COLUMN_NAME)
	SELECT cu.COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE cu
	WHERE EXISTS ( SELECT tc.* FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc WHERE tc.TABLE_NAME = @TableName AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME )

DECLARE @COLS TABLE (ROWID INT IDENTITY(1,1),COLUMN_NAME VARCHAR(255), DATATYPE VARCHAR(50))
INSERT INTO @COLS
--get the columns to work with
SELECT 
COLUMN_NAME,
DATA_TYPE +
CASE WHEN CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN 
	'(' + 
	CASE WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN 
		'MAX' 
	ELSE 
		CAST(CHARACTER_MAXIMUM_LENGTH AS VARCHAR) 
	END 
	+ ')'
ELSE
	''
END
FROM information_schema.columns
WHERE TABLE_NAME = @TableName
ORDER BY ORDINAL_POSITION

SET @InsertSQL = ''
SET @InsertCols = ''
SET @InsertValues = ''
SET @UpdateSQL = ''
SET @WhereSQL = ''

--build the sql by working through the columns and setting values as necessary
--always build both insert and update for resiliance if a row is missing on the dataplatform for some reason
SELECT @RowId = MIN(ROWID), @MaxRowId = MAX(ROWID) FROM @COLS
WHILE @RowId <= @MaxRowId
BEGIN  
	SELECT @ColName = COLUMN_NAME, @ColDt = DATATYPE FROM @COLS WHERE RowID = @RowID
	IF @ColName NOT IN (SELECT COLUMN_NAME FROM @IgnoreColList)
	BEGIN
		IF @UpdateSQL <>'' SET @UpdateSQL = @UpdateSQL + ', '
		IF @InsertCols <>'' SET @InsertCols = @InsertCols + ', '
		IF @InsertValues <>'' SET @InsertValues = @InsertValues + ', '
                    
		IF @ColName NOT IN (SELECT COLUMN_NAME FROM @PKColList)--ignore pk col for update, but get the col part for the where clause
			SET @UpdateSQL = @UpdateSQL + @ColName + ' = '
		ELSE
		BEGIN
			IF @WhereSQL <> ''
				SET @WhereSQL = @WhereSQL + ' AND '

			SET @WhereSQL = @WhereSQL + '(' + @ColName + ' = '
		END
				
		SET @InsertCols = @InsertCols + @ColName

		IF @ColDT <> 'xml(MAX)'
			SELECT @VALUE = x.item.value('*[local-name() = sql:variable("@ColName")][1]','nvarchar(max)') FROM @ChangeXML.nodes('//DataChange') AS x(item)
		ELSE
			BEGIN
				--xml data types need extracting and converting, or you lose any xml inside them
				SELECT @XVALUE = x.item.query('*[local-name() = sql:variable("@ColName")]/*') FROM @ChangeXML.nodes('//DataChange') AS x(item)
				SET @VALUE=CONVERT(NVARCHAR(MAX),@XVALUE)
			END
			
		IF @VALUE IS NULL
		BEGIN
			IF @ColName NOT IN (SELECT COLUMN_NAME FROM @PKColList)--ignore pk col for update but use for the where clause (but a NULL PK is gonna be death!)
				SELECT @UpdateSQL = @UpdateSQL + 'NULL'
			ELSE
				SELECT @WhereSQL = @WhereSQL + 'NULL'
					
			SELECT @InsertValues = @InsertValues + 'NULL'
		END
		ELSE
		BEGIN
			IF @ColDt IN ('binary(MAX)', 'varbinary(MAX)')
				--binary need to be treated differently, converted to hex from base64
				SET @VALUE  = sys.fn_varbintohexstr(cast(N'' as xml).value('xs:base64Binary(sql:variable("@VALUE"))', 'varbinary(MAX)'))
			ELSE
				--normal field
				IF @ColDt NOT IN ('int','bigint','float','bit')
					SELECT @VALUE = @Quote + replace(@VALUE, @Quote, @Quote + @Quote) + @Quote

            IF @ColName NOT IN (SELECT COLUMN_NAME FROM @PKColList)--ignore pk col for update but use for the where clause
				SELECT @UpdateSQL = @UpdateSQL + @VALUE
			ELSE
				SELECT @WhereSQL = @WhereSQL + @VALUE + ')'
					
			SELECT @InsertValues = @InsertValues + @VALUE
				
		END

	END
		                  
	SET @RowId = @RowId + 1
END
             
IF @UpdateType IN ('U','I')
BEGIN
	IF @UpdateSQL<>''
		SET @UpdateSQL = 'UPDATE ' + @TableName + ' SET ' + @UpdateSQL + ' WHERE ' + @WhereSQL
	ELSE
		SET @UpdateSQL = 'SELECT 1 FROM ' + @TableName

	SET @InsertSQL = 'INSERT INTO ' + @TableName + '(' + @InsertCols + ') VALUES (' + @InsertValues + ')'

	SET @ExecSQL = 'IF EXISTS(SELECT 1 FROM ' + @TableName +  ' WHERE ' + @WhereSQL + ') '
	SET @ExecSQL = @ExecSQL + @UpdateSQL
	SET @ExecSQL = @ExecSQL + ' ELSE '
	SET @ExecSQL = @ExecSQL + @InsertSQL
END
ELSE
	--delete
	SET @ExecSQL = 'DELETE FROM ' + @TableName + ' WHERE ' + @WhereSQL	 

EXECUTE [DEVCI_SystemSyncSender].dbo.[usp_AddSyncItem] --change to specify db location of our sync db
   @System, @TableName, @ExecSQL, @UpdatedWhen
GO

