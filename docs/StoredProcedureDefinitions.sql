/* dbo.CreateTempTables */

        CREATE PROCEDURE dbo.CreateTempTables
        AS
            CREATE TABLE [TradeNetDBTest].dbo.ASPStateTempSessions (
                SessionId           nvarchar(88)    NOT NULL PRIMARY KEY,
                Created             datetime        NOT NULL DEFAULT GETUTCDATE(),
                Expires             datetime        NOT NULL,
                LockDate            datetime        NOT NULL,
                LockDateLocal       datetime        NOT NULL,
                LockCookie          int             NOT NULL,
                Timeout             int             NOT NULL,
                Locked              bit             NOT NULL,
                SessionItemShort    VARBINARY(7000) NULL,
                SessionItemLong     image           NULL,
                Flags               int             NOT NULL DEFAULT 0,
            ) 

            CREATE NONCLUSTERED INDEX Index_Expires ON [TradeNetDBTest].dbo.ASPStateTempSessions(Expires)

            CREATE TABLE [TradeNetDBTest].dbo.ASPStateTempApplications (
                AppId               int             NOT NULL PRIMARY KEY,
                AppName             char(280)       NOT NULL,
            ) 

            CREATE NONCLUSTERED INDEX Index_AppName ON [TradeNetDBTest].dbo.ASPStateTempApplications(AppName)

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     
GO

/* dbo.DeleteExpiredSessions */

        CREATE PROCEDURE dbo.DeleteExpiredSessions
        AS
            SET NOCOUNT ON
            SET DEADLOCK_PRIORITY LOW 

            DECLARE @now datetime
            SET @now = GETUTCDATE() 

            CREATE TABLE #tblExpiredSessions 
            ( 
                SessionId nvarchar(88) NOT NULL PRIMARY KEY
            )

            INSERT #tblExpiredSessions (SessionId)
                SELECT SessionId
                FROM [TradeNetDBTest].dbo.ASPStateTempSessions WITH (READUNCOMMITTED)
                WHERE Expires < @now

            IF @@ROWCOUNT <> 0 
            BEGIN 
                DECLARE ExpiredSessionCursor CURSOR LOCAL FORWARD_ONLY READ_ONLY
                FOR SELECT SessionId FROM #tblExpiredSessions 

                DECLARE @SessionId nvarchar(88)

                OPEN ExpiredSessionCursor

                FETCH NEXT FROM ExpiredSessionCursor INTO @SessionId

                WHILE @@FETCH_STATUS = 0 
                    BEGIN
                        DELETE FROM [TradeNetDBTest].dbo.ASPStateTempSessions WHERE SessionId = @SessionId AND Expires < @now
                        FETCH NEXT FROM ExpiredSessionCursor INTO @SessionId
                    END

                CLOSE ExpiredSessionCursor

                DEALLOCATE ExpiredSessionCursor

            END 

            DROP TABLE #tblExpiredSessions

        RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     
GO

/* dbo.GetHashCode */

/*****************************************************************************/

CREATE PROCEDURE dbo.GetHashCode
    @input tAppName,
    @hash int OUTPUT
AS
    /* 
       This sproc is based on this C# hash function:

        int GetHashCode(string s)
        {
            int     hash = 5381;
            int     len = s.Length;

            for (int i = 0; i < len; i++) {
                int     c = Convert.ToInt32(s[i]);
                hash = ((hash << 5) + hash) ^ c;
            }

            return hash;
        }

        However, SQL 7 doesn't provide a 32-bit integer
        type that allows rollover of bits, we have to
        divide our 32bit integer into the upper and lower
        16 bits to do our calculation.
    */
       
    DECLARE @hi_16bit   int
    DECLARE @lo_16bit   int
    DECLARE @hi_t       int
    DECLARE @lo_t       int
    DECLARE @len        int
    DECLARE @i          int
    DECLARE @c          int
    DECLARE @carry      int

    SET @hi_16bit = 0
    SET @lo_16bit = 5381
    
    SET @len = DATALENGTH(@input)
    SET @i = 1
    
    WHILE (@i <= @len)
    BEGIN
        SET @c = ASCII(SUBSTRING(@input, @i, 1))

        /* Formula:                        
           hash = ((hash << 5) + hash) ^ c */

        /* hash << 5 */
        SET @hi_t = @hi_16bit * 32 /* high 16bits << 5 */
        SET @hi_t = @hi_t & 0xFFFF /* zero out overflow */
        
        SET @lo_t = @lo_16bit * 32 /* low 16bits << 5 */
        
        SET @carry = @lo_16bit & 0x1F0000 /* move low 16bits carryover to hi 16bits */
        SET @carry = @carry / 0x10000 /* >> 16 */
        SET @hi_t = @hi_t + @carry
        SET @hi_t = @hi_t & 0xFFFF /* zero out overflow */

        /* + hash */
        SET @lo_16bit = @lo_16bit + @lo_t
        SET @hi_16bit = @hi_16bit + @hi_t + (@lo_16bit / 0x10000)
        /* delay clearing the overflow */

        /* ^c */
        SET @lo_16bit = @lo_16bit ^ @c

        /* Now clear the overflow bits */	
        SET @hi_16bit = @hi_16bit & 0xFFFF
        SET @lo_16bit = @lo_16bit & 0xFFFF

        SET @i = @i + 1
    END

    /* Do a sign extension of the hi-16bit if needed */
    IF (@hi_16bit & 0x8000 <> 0)
        SET @hi_16bit = 0xFFFF0000 | @hi_16bit

    /* Merge hi and lo 16bit back together */
    SET @hi_16bit = @hi_16bit * 0x10000 /* << 16 */
    SET @hash = @hi_16bit | @lo_16bit

    RETURN 0
GO

/* dbo.GetMajorVersion */

/*****************************************************************************/

CREATE PROCEDURE dbo.GetMajorVersion
    @@ver int OUTPUT
AS
BEGIN
	DECLARE @version        nchar(100)
	DECLARE @dot            int
	DECLARE @hyphen         int
	DECLARE @SqlToExec      nchar(4000)

	SELECT @@ver = 7
	SELECT @version = @@Version
	SELECT @hyphen  = CHARINDEX(N' - ', @version)
	IF (NOT(@hyphen IS NULL) AND @hyphen > 0)
	BEGIN
		SELECT @hyphen = @hyphen + 3
		SELECT @dot    = CHARINDEX(N'.', @version, @hyphen)
		IF (NOT(@dot IS NULL) AND @dot > @hyphen)
		BEGIN
			SELECT @version = SUBSTRING(@version, @hyphen, @dot - @hyphen)
			SELECT @@ver     = CONVERT(int, @version)
		END
	END
END
GO

/* dbo.GetRequestAutoApproveDescriptions */
Create PROCEDURE [dbo].[GetRequestAutoApproveDescriptions]
    @FromDate DATETIME,
    @ToDate DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM RequestAutoApproveDescription
    WHERE CreatedDate >= @FromDate AND CreatedDate <= @ToDate;
END

GO

/* dbo.GetRequestAutoApproveDescriptionsImport */
CREATE PROCEDURE [dbo].[GetRequestAutoApproveDescriptionsImport]
    @FromDate DATETIME,
    @ToDate DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM RequestAutoApproveDescriptionImport
    WHERE CreatedDate >= @FromDate AND CreatedDate <= @ToDate;
END

GO

/* dbo.GetRequestById */
Create PROCEDURE [dbo].[GetRequestById]
    @Id uniqueidentifier
AS
BEGIN
    SELECT TOP 1 *
    FROM RequestAutoApproveDescription
    WHERE id = @Id
END

GO

/* dbo.GetRequestByIdImport */
CREATE PROCEDURE [dbo].[GetRequestByIdImport]
    @Id uniqueidentifier
AS
BEGIN
    SELECT TOP 1 *
    FROM RequestAutoApproveDescriptionImport
    WHERE id = @Id
END

GO

/* dbo.sp_AccountSummaryReport */
CREATE PROCEDURE [dbo].[sp_AccountSummaryReport]
(
    -- Add the parameters for the stored procedure here
    @FromDate datetime,
	@ToDate datetime,
    @FormType nvarchar(50),
	@SakhanId int
)
AS
BEGIN
	SELECT * FROM
    (
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,'' CompanyRegistrationNo,VoucherNo,'' CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Member' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN MemberRegistration ON AccountTransaction.TransactionId = MemberRegistration.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKaRegistration.CompanyRegistrationNo,VoucherNo,CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Pa Tha Ka' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN PaThaKaRegistration ON AccountTransaction.TransactionId = PaThaKaRegistration.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Business Service Agency' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BusinessServiceAgencyRegistration ON AccountTransaction.TransactionId = BusinessServiceAgencyRegistration.Id
	INNER JOIN PaThaKa ON BusinessServiceAgencyRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Duty Free Shop' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN DutyFreeShopRegistration ON AccountTransaction.TransactionId = DutyFreeShopRegistration.Id
	INNER JOIN PaThaKa ON DutyFreeShopRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Re-Export' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ReExportRegistration ON AccountTransaction.TransactionId = ReExportRegistration.Id
	INNER JOIN PaThaKa ON ReExportRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,RegistrationType FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN SaleCenterRegistration ON AccountTransaction.TransactionId = SaleCenterRegistration.Id
	INNER JOIN PaThaKa ON SaleCenterRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,RegistrationType FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ShowRoomRegistration ON AccountTransaction.TransactionId = ShowRoomRegistration.Id
	INNER JOIN PaThaKa ON ShowRoomRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,RegistrationType FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN EVShowRoomRegistration ON AccountTransaction.TransactionId = EVShowRoomRegistration.Id
	INNER JOIN PaThaKa ON EVShowRoomRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,RegistrationType FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN EVCycleShowRoomRegistration ON AccountTransaction.TransactionId = EVCycleShowRoomRegistration.Id
	INNER JOIN PaThaKa ON EVCycleShowRoomRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,RegistrationType FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN WholeSaleRetailRegistration ON AccountTransaction.TransactionId = WholeSaleRetailRegistration.Id
	INNER JOIN PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Wine Imporation' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN WineImportationRegistration ON AccountTransaction.TransactionId = WineImportationRegistration.Id
	INNER JOIN PaThaKa ON WineImportationRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Import Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN DeleteData ON AccountTransaction.TransactionId = DeleteData.TransactionId
	INNER JOIN PaThaKa ON DeleteData.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 and DeleteData.SakhanId=0
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Export Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN DeleteData ON AccountTransaction.TransactionId = DeleteData.TransactionId
	INNER JOIN Sakhan ON DeleteData.SakhanId = Sakhan.Id
	INNER JOIN PaThaKa ON DeleteData.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND  DeleteData.SakhanId <> 0
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Export Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ExportLicence ON AccountTransaction.TransactionId = ExportLicence.Id
	INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Import Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ImportLicence ON AccountTransaction.TransactionId = ImportLicence.Id
	INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Export Permit' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ExportPermit ON AccountTransaction.TransactionId = ExportPermit.Id
	INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Import Permit' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ImportPermit ON AccountTransaction.TransactionId = ImportPermit.Id
	INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Export Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderExportLicence ON AccountTransaction.TransactionId = BorderExportLicence.Id
	INNER JOIN Sakhan ON BorderExportLicence.SakhanId = Sakhan.Id
	INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND CardType='Pa Tha Ka'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,IndividualTrading.TINNo CompanyRegistrationNo,VoucherNo,IndividualTrading.Name CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Export Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderExportLicence ON AccountTransaction.TransactionId = BorderExportLicence.Id
	INNER JOIN Sakhan ON BorderExportLicence.SakhanId = Sakhan.Id
	INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
	WHERE IsPayment=1 AND CardType='Individual Trading'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Import Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderImportLicence ON AccountTransaction.TransactionId = BorderImportLicence.Id
	INNER JOIN Sakhan ON BorderImportLicence.SakhanId = Sakhan.Id
	INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND CardType='Pa Tha Ka'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,IndividualTrading.TINNo CompanyRegistrationNo,VoucherNo,IndividualTrading.Name CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Import Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderImportLicence ON AccountTransaction.TransactionId = BorderImportLicence.Id
	INNER JOIN Sakhan ON BorderImportLicence.SakhanId = Sakhan.Id
	INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
	WHERE IsPayment=1 AND CardType='Individual Trading'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Export Permit' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderExportPermit ON AccountTransaction.TransactionId = BorderExportPermit.Id
	INNER JOIN Sakhan ON BorderExportPermit.SakhanId = Sakhan.Id
	INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Import Permit' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderImportPermit ON AccountTransaction.TransactionId = BorderImportPermit.Id
	INNER JOIN Sakhan ON BorderImportPermit.SakhanId = Sakhan.Id
	INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate))tmp
	WHERE tmp.FormType = (CASE WHEN @FormType='' THEN tmp.FormType ELSE @FormType END)
	AND tmp.SakhanId = (CASE WHEN @SakhanId='' THEN tmp.SakhanId ELSE @SakhanId END)
	ORDER BY tmp.PaymentDate,tmp.SortOrder
END

GO

/* dbo.sp_ActualAmendReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_ExportLicenceAmendReport ''
CREATE PROCEDURE [dbo].[sp_ActualAmendReport] 
	-- Add the parameters for the stored procedure here
	@FormType nvarchar(50),
	@FromDate datetime,
	@ToDate datetime,
	@ExportImportSectionId int,
	@AmendRemarkId int,
	@CompanyRegistrationNo nvarchar(50),
	@SakhanId int
AS
BEGIN
	IF(@FormType='Export Licence')
	BEGIN
		SELECT ExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,ExportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(ExportLicenceItem.Amount,0) FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Amount
		FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType='Actual Amend' AND ExportLicence.Status='Approved'
		AND (ExportLicence.CreatedDate>=@FromDate AND ExportLicence.CreatedDate<=@ToDate)
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Import Licence')
	BEGIN
		SELECT ImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,ImportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Currency,
		(SELECT top 1 ISNULL(ImportLicenceItem.Amount,0) FROM ImportLicenceItem
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Amount
		FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType='Actual Amend' AND ImportLicence.Status='Approved'
		AND (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Export Permit')
	BEGIN
		SELECT ExportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,CONVERT(varchar,ExportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ExportPermitItem
		INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Currency,
		(SELECT top 1 ISNULL(ExportPermitItem.Amount,0) FROM ExportPermitItem
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Amount
		FROM ExportPermit
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType='Actual Amend' AND ExportPermit.Status='Approved'
		AND (ExportPermit.CreatedDate>=@FromDate AND ExportPermit.CreatedDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ExportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ExportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Import Permit')
	BEGIN
		SELECT ImportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,CONVERT(varchar,ImportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ImportPermitItem
		INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Currency,
		(SELECT top 1 ISNULL(ImportPermitItem.Amount,0) FROM ImportPermitItem
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Amount
		FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType='Actual Amend' AND ImportPermit.Status='Approved'
		AND (ImportPermit.CreatedDate>=@FromDate AND ImportPermit.CreatedDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ImportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Border Export Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(BorderExportLicenceItem.Amount,0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportLicence
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Actual Amend' AND BorderExportLicence.Status='Approved' AND CardType='Pa Tha Ka'
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
		IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(BorderExportLicenceItem.Amount,0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Actual Amend' AND BorderExportLicence.Status='Approved' AND CardType='Individual Trading'
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		)tmp
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Import Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT top 1 ISNULL(BorderImportLicenceItem.Amount,0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Actual Amend' AND BorderImportLicence.Status='Approved' AND CardType='Pa Tha Ka'
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
		IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT top 1 ISNULL(BorderImportLicenceItem.Amount,0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Actual Amend' AND BorderImportLicence.Status='Approved' AND CardType='Individual Trading'
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		)tmp
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Export Permit')
	BEGIN
		SELECT BorderExportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,CONVERT(varchar,BorderExportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportPermitItem
		INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Currency,
		(SELECT top 1 ISNULL(BorderExportPermitItem.Amount,0) FROM BorderExportPermitItem
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportPermit
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		WHERE ApplyType='Actual Amend' AND BorderExportPermit.Status='Approved'
		AND (BorderExportPermit.CreatedDate>=@FromDate AND BorderExportPermit.CreatedDate<=@ToDate)
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END)
	END
	ELSE IF(@FormType='Border Import Permit')
	BEGIN
		SELECT BorderImportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,CONVERT(varchar,BorderImportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportPermitItem
		INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Currency,
		(SELECT top 1 ISNULL(BorderImportPermitItem.Amount,0) FROM BorderImportPermitItem
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType='Actual Amend' AND BorderImportPermit.Status='Approved'
		AND (BorderImportPermit.CreatedDate>=@FromDate AND BorderImportPermit.CreatedDate<=@ToDate)
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
	END
	
END

GO

/* dbo.sp_AmendReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_ExportLicenceAmendReport ''
CREATE PROCEDURE [dbo].[sp_AmendReport] 
	-- Add the parameters for the stored procedure here
	@FormType nvarchar(50),
	@FromDate datetime,
	@ToDate datetime,
	@ExportImportSectionId int,
	@AmendRemarkId int,
	@CompanyRegistrationNo nvarchar(50),
	@SakhanId int
AS
BEGIN
	IF(@FormType='Export Licence')
	BEGIN
		SELECT ExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,ExportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(ExportLicenceItem.Amount,0) FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Amount
		FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType='Amend' AND ExportLicence.Status='Approved'
		AND (ExportLicence.CreatedDate>=@FromDate AND ExportLicence.CreatedDate<=@ToDate)
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Import Licence')
	BEGIN
		SELECT ImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,ImportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Currency,
		(SELECT top 1 ISNULL(ImportLicenceItem.Amount,0) FROM ImportLicenceItem
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Amount
		FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType='Amend' AND ImportLicence.Status='Approved'
		AND (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Export Permit')
	BEGIN
		SELECT ExportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,CONVERT(varchar,ExportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ExportPermitItem
		INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Currency,
		(SELECT top 1 ISNULL(ExportPermitItem.Amount,0) FROM ExportPermitItem
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Amount
		FROM ExportPermit
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType='Amend' AND ExportPermit.Status='Approved'
		AND (ExportPermit.CreatedDate>=@FromDate AND ExportPermit.CreatedDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ExportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ExportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Import Permit')
	BEGIN
		SELECT ImportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,CONVERT(varchar,ImportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ImportPermitItem
		INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Currency,
		(SELECT top 1 ISNULL(ImportPermitItem.Amount,0) FROM ImportPermitItem
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Amount
		FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType='Amend' AND ImportPermit.Status='Approved'
		AND (ImportPermit.CreatedDate>=@FromDate AND ImportPermit.CreatedDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND ImportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then ImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Border Export Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(BorderExportLicenceItem.Amount,0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportLicence
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Amend' AND BorderExportLicence.Status='Approved' AND CardType='Pa Tha Ka'
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
		IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(BorderExportLicenceItem.Amount,0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Amend' AND BorderExportLicence.Status='Approved' AND CardType='Individual Trading'
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		)tmp
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Import Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT top 1 ISNULL(BorderImportLicenceItem.Amount,0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Amend' AND BorderImportLicence.Status='Approved' AND CardType='Pa Tha Ka'
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
		IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT top 1 ISNULL(BorderImportLicenceItem.Amount,0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Amend' AND BorderImportLicence.Status='Approved' AND CardType='Individual Trading'
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportLicence.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportLicence.AmendRemarkId ELSE @AmendRemarkId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		)tmp
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Export Permit')
	BEGIN
		SELECT BorderExportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,CONVERT(varchar,BorderExportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportPermitItem
		INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Currency,
		(SELECT top 1 ISNULL(BorderExportPermitItem.Amount,0) FROM BorderExportPermitItem
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportPermit
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		WHERE ApplyType='Amend' AND BorderExportPermit.Status='Approved'
		AND (BorderExportPermit.CreatedDate>=@FromDate AND BorderExportPermit.CreatedDate<=@ToDate)
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderExportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderExportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END)
	END
	ELSE IF(@FormType='Border Import Permit')
	BEGIN
		SELECT BorderImportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,CONVERT(varchar,BorderImportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportPermitItem
		INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Currency,
		(SELECT top 1 ISNULL(BorderImportPermitItem.Amount,0) FROM BorderImportPermitItem
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType='Amend' AND BorderImportPermit.Status='Approved'
		AND (BorderImportPermit.CreatedDate>=@FromDate AND BorderImportPermit.CreatedDate<=@ToDate)
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND BorderImportPermit.AmendRemarkId=(CASE WHEN @AmendRemarkId=0 then BorderImportPermit.AmendRemarkId ELSE @AmendRemarkId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
	END
	
END

GO

/* dbo.sp_ApplicationHistory */
-- =============================================
-- Author:      Name
-- Create Date: 
-- Description: 
-- =============================================
CREATE PROCEDURE [dbo].[sp_ApplicationHistory]
(
    -- Add the parameters for the stored procedure here
    @FromDate datetime,
	@Todate datetime,
	@MemberId char(36),
	@FilterMemberId char(36)
)
AS
BEGIN

	DECLARE @CompanyRegistrationNo nvarchar(20)

	SET @CompanyRegistrationNo = (SELECT TOP 1 CompanyRegistrationNo FROM PaThaKa WHERE MemberId=@MemberId)


   SELECT * FROM 
	(SELECT ApplicationNo,ApplicationDate Date,'Pa Tha Ka' FormType,ApplyType,PaThaKaRegistration.CompanyRegistrationNo,CompanyName,PaThaKaRegistration.CompanyRegistrationNo CardLicencePermitNo,
	CASE WHEN PaThaKaRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=PaThaKaRegistration.Id)
	ELSE [Status].Message END Message,PaThaKaRegistration.MemberId,PaThaKaRegistration.Status,FullName
	FROM PaThaKaRegistration
	INNER JOIN Status ON PaThaKaRegistration.Status = [Status].Status
	INNER JOIN Member ON PaThaKaRegistration.MemberId = Member.Id
	WHERE PaThaKaRegistration.CompanyRegistrationNo=@CompanyRegistrationNo AND (PaThaKaRegistration.Status<>'Approved' AND PaThaKaRegistration.Status<>'')
	AND (PaThaKaRegistration.ApplicationDate>=@FromDate AND PaThaKaRegistration.ApplicationDate<=@ToDate)
	AND PaThaKaRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN PaThaKaRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,PaThaKaRegistration.CreatedDate Date,'Pa Tha Ka' FormType,ApplyType,PaThaKaRegistration.CompanyRegistrationNo,CompanyName,PaThaKaRegistration.CompanyRegistrationNo CardLicencePermitNo,
	CASE WHEN PaThaKaRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=PaThaKaRegistration.Id)
	ELSE [Status].Message END Message,PaThaKaRegistration.MemberId,PaThaKaRegistration.Status,FullName
	FROM PaThaKaRegistration
	INNER JOIN Status ON PaThaKaRegistration.Status = [Status].Status
	INNER JOIN Member ON PaThaKaRegistration.MemberId = Member.Id
	WHERE PaThaKaRegistration.CompanyRegistrationNo=@CompanyRegistrationNo AND (PaThaKaRegistration.Status='Approved')
	AND (PaThaKaRegistration.CreatedDate>=@FromDate AND PaThaKaRegistration.CreatedDate<=@ToDate)
	AND PaThaKaRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN PaThaKaRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,'Border Export Licence' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ExportLicenceNo CardLicencePermitNo,
	CASE WHEN BorderExportLicence.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=BorderExportLicence.Id)
	ELSE [Status].Message END Message,BorderExportLicence.MemberId,BorderExportLicence.Status,FullName
	FROM BorderExportLicence
	INNER JOIN Status ON BorderExportLicence.Status = [Status].Status
	INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON BorderExportLicence.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (BorderExportLicence.Status<>'Approved' AND BorderExportLicence.Status<>'')
	AND (BorderExportLicence.ApplicationDate>=@FromDate AND BorderExportLicence.ApplicationDate<=@ToDate)
	AND BorderExportLicence.MemberId=(CASE WHEN @FilterMemberId='' THEN BorderExportLicence.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,BorderExportLicence.CreatedDate Date,'Border Export Licence' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ExportLicenceNo CardLicencePermitNo,
	CASE WHEN BorderExportLicence.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=BorderExportLicence.Id)
	ELSE [Status].Message END Message,BorderExportLicence.MemberId,BorderExportLicence.Status,FullName
	FROM BorderExportLicence
	INNER JOIN Status ON BorderExportLicence.Status = [Status].Status
	INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON BorderExportLicence.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (BorderExportLicence.Status='Approved')
	AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
	AND BorderExportLicence.MemberId=(CASE WHEN @FilterMemberId='' THEN BorderExportLicence.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,'Border Export Permit' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ExportPermitNo CardLicencePermitNo,
	CASE WHEN BorderExportPermit.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=BorderExportPermit.Id)
	ELSE [Status].Message END Message,BorderExportPermit.MemberId,BorderExportPermit.Status,FullName
	FROM BorderExportPermit
	INNER JOIN Status ON BorderExportPermit.Status = [Status].Status
	INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON BorderExportPermit.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (BorderExportPermit.Status<>'Approved' AND BorderExportPermit.Status<>'')
	AND (BorderExportPermit.ApplicationDate>=@FromDate AND BorderExportPermit.ApplicationDate<=@ToDate)
	AND BorderExportPermit.MemberId=(CASE WHEN @FilterMemberId='' THEN BorderExportPermit.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,BorderExportPermit.CreatedDate Date,'Border Export Permit' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ExportPermitNo CardLicencePermitNo,
	CASE WHEN BorderExportPermit.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=BorderExportPermit.Id)
	ELSE [Status].Message END Message,BorderExportPermit.MemberId,BorderExportPermit.Status,FullName
	FROM BorderExportPermit
	INNER JOIN Status ON BorderExportPermit.Status = [Status].Status
	INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON BorderExportPermit.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (BorderExportPermit.Status='Approved')
	AND (BorderExportPermit.CreatedDate>=@FromDate AND BorderExportPermit.CreatedDate<=@ToDate)
	AND BorderExportPermit.MemberId=(CASE WHEN @FilterMemberId='' THEN BorderExportPermit.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,'Border Import Licence' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ImportLicenceNo CardLicencePermitNo,
	CASE WHEN BorderImportLicence.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=BorderImportLicence.Id)
	ELSE [Status].Message END Message,BorderImportLicence.MemberId,BorderImportLicence.Status,FullName
	FROM BorderImportLicence
	INNER JOIN Status ON BorderImportLicence.Status = [Status].Status
	INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON BorderImportLicence.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (BorderImportLicence.Status<>'Approved' AND BorderImportLicence.Status<>'')
	AND (BorderImportLicence.ApplicationDate>=@FromDate AND BorderImportLicence.ApplicationDate<=@ToDate)
	AND BorderImportLicence.MemberId=(CASE WHEN @FilterMemberId='' THEN BorderImportLicence.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,BorderImportLicence.CreatedDate Date,'Border Import Licence' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ImportLicenceNo CardLicencePermitNo,
	CASE WHEN BorderImportLicence.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=BorderImportLicence.Id)
	ELSE [Status].Message END Message,BorderImportLicence.MemberId,BorderImportLicence.Status,FullName
	FROM BorderImportLicence
	INNER JOIN Status ON BorderImportLicence.Status = [Status].Status
	INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON BorderImportLicence.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (BorderImportLicence.Status='Approved')
	AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
	AND BorderImportLicence.MemberId=(CASE WHEN @FilterMemberId='' THEN BorderImportLicence.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,'Border Import Permit' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ImportPermitNo CardLicencePermitNo,
	CASE WHEN BorderImportPermit.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=BorderImportPermit.Id)
	ELSE [Status].Message END Message,BorderImportPermit.MemberId,BorderImportPermit.Status,FullName
	FROM BorderImportPermit
	INNER JOIN Status ON BorderImportPermit.Status = [Status].Status
	INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON BorderImportPermit.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (BorderImportPermit.Status<>'Approved' AND BorderImportPermit.Status<>'')
	AND (BorderImportPermit.ApplicationDate>=@FromDate AND BorderImportPermit.ApplicationDate<=@ToDate)
	AND BorderImportPermit.MemberId=(CASE WHEN @FilterMemberId='' THEN BorderImportPermit.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,BorderImportPermit.CreatedDate Date,'Border Import Permit' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ImportPermitNo CardLicencePermitNo,
	CASE WHEN BorderImportPermit.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=BorderImportPermit.Id)
	ELSE [Status].Message END Message,BorderImportPermit.MemberId,BorderImportPermit.Status,FullName
	FROM BorderImportPermit
	INNER JOIN Status ON BorderImportPermit.Status = [Status].Status
	INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON BorderImportPermit.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (BorderImportPermit.Status='Approved')
	AND (BorderImportPermit.CreatedDate>=@FromDate AND BorderImportPermit.CreatedDate<=@ToDate)
	AND BorderImportPermit.MemberId=(CASE WHEN @FilterMemberId='' THEN BorderImportPermit.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,'Export Licence' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ExportLicenceNo CardLicencePermitNo,
	CASE WHEN ExportLicence.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=ExportLicence.Id)
	ELSE [Status].Message END Message,ExportLicence.MemberId,ExportLicence.Status,FullName
	FROM ExportLicence
	INNER JOIN Status ON ExportLicence.Status = [Status].Status
	INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON ExportLicence.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (ExportLicence.Status<>'Approved' AND ExportLicence.Status<>'')
	AND (ExportLicence.ApplicationDate>=@FromDate AND ExportLicence.ApplicationDate<=@ToDate)
	AND ExportLicence.MemberId=(CASE WHEN @FilterMemberId='' THEN ExportLicence.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ExportLicence.CreatedDate Date,'Export Licence' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ExportLicenceNo CardLicencePermitNo,
	CASE WHEN ExportLicence.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=ExportLicence.Id)
	ELSE [Status].Message END Message,ExportLicence.MemberId,ExportLicence.Status,FullName
	FROM ExportLicence
	INNER JOIN Status ON ExportLicence.Status = [Status].Status
	INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON ExportLicence.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (ExportLicence.Status='Approved')
	AND (ExportLicence.CreatedDate>=@FromDate AND ExportLicence.CreatedDate<=@ToDate)
	AND ExportLicence.MemberId=(CASE WHEN @FilterMemberId='' THEN ExportLicence.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,'Export Permit' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ExportPermitNo CardLicencePermitNo,
	CASE WHEN ExportPermit.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=ExportPermit.Id)
	ELSE [Status].Message END Message,ExportPermit.MemberId,ExportPermit.Status,FullName
	FROM ExportPermit
	INNER JOIN Status ON ExportPermit.Status = [Status].Status
	INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON ExportPermit.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (ExportPermit.Status<>'Approved' AND ExportPermit.Status<>'')
	AND (ExportPermit.ApplicationDate>=@FromDate AND ExportPermit.ApplicationDate<=@ToDate)
	AND ExportPermit.MemberId=(CASE WHEN @FilterMemberId='' THEN ExportPermit.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ExportPermit.CreatedDate Date,'Export Permit' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ExportPermitNo CardLicencePermitNo,
	CASE WHEN ExportPermit.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=ExportPermit.Id)
	ELSE [Status].Message END Message,ExportPermit.MemberId,ExportPermit.Status,FullName
	FROM ExportPermit
	INNER JOIN Status ON ExportPermit.Status = [Status].Status
	INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON ExportPermit.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (ExportPermit.Status='Approved')
	AND (ExportPermit.CreatedDate>=@FromDate AND ExportPermit.CreatedDate<=@ToDate)
	AND ExportPermit.MemberId=(CASE WHEN @FilterMemberId='' THEN ExportPermit.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,'Import Licence' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ImportLicenceNo CardLicencePermitNo,
	CASE WHEN ImportLicence.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=ImportLicence.Id)
	ELSE [Status].Message END Message,ImportLicence.MemberId,ImportLicence.Status,FullName
	FROM ImportLicence
	INNER JOIN Status ON ImportLicence.Status = [Status].Status
	INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON ImportLicence.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (ImportLicence.Status<>'Approved' AND ImportLicence.Status<>'')
	AND (ImportLicence.ApplicationDate>=@FromDate AND ImportLicence.ApplicationDate<=@ToDate)
	AND ImportLicence.MemberId=(CASE WHEN @FilterMemberId='' THEN ImportLicence.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ImportLicence.CreatedDate Date,'Import Licence' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ImportLicenceNo CardLicencePermitNo,
	CASE WHEN ImportLicence.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=ImportLicence.Id)
	ELSE [Status].Message END Message,ImportLicence.MemberId,ImportLicence.Status,FullName
	FROM ImportLicence
	INNER JOIN Status ON ImportLicence.Status = [Status].Status
	INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON ImportLicence.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (ImportLicence.Status='Approved')
	AND (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate)
	AND ImportLicence.MemberId=(CASE WHEN @FilterMemberId='' THEN ImportLicence.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,'Import Permit' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ImportPermitNo CardLicencePermitNo,
	CASE WHEN ImportPermit.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=ImportPermit.Id)
	ELSE [Status].Message END Message,ImportPermit.MemberId,ImportPermit.Status,FullName
	FROM ImportPermit
	INNER JOIN Status ON ImportPermit.Status = [Status].Status
	INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON ImportPermit.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (ImportPermit.Status<>'Approved' AND ImportPermit.Status<>'')
	AND (ImportPermit.ApplicationDate>=@FromDate AND ImportPermit.ApplicationDate<=@ToDate)
	AND ImportPermit.MemberId=(CASE WHEN @FilterMemberId='' THEN ImportPermit.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ImportPermit.CreatedDate Date,'Import Permit' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ImportPermitNo CardLicencePermitNo,
	CASE WHEN ImportPermit.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=ImportPermit.Id)
	ELSE [Status].Message END Message,ImportPermit.MemberId,ImportPermit.Status,FullName
	FROM ImportPermit
	INNER JOIN Status ON ImportPermit.Status = [Status].Status
	INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON ImportPermit.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (ImportPermit.Status='Approved')
	AND (ImportPermit.CreatedDate>=@FromDate AND ImportPermit.CreatedDate<=@ToDate)
	AND ImportPermit.MemberId=(CASE WHEN @FilterMemberId='' THEN ImportPermit.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,'Business Service Agency' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,BusinessServiceAgencyNo CardLicencePermitNo,
	CASE WHEN BusinessServiceAgencyRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=BusinessServiceAgencyRegistration.Id)
	ELSE [Status].Message END Message,BusinessServiceAgencyRegistration.MemberId,BusinessServiceAgencyRegistration.Status,FullName
	FROM BusinessServiceAgencyRegistration
	INNER JOIN Status ON BusinessServiceAgencyRegistration.Status = [Status].Status
	INNER JOIN PaThaKa ON BusinessServiceAgencyRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON BusinessServiceAgencyRegistration.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (BusinessServiceAgencyRegistration.Status<>'Approved' AND BusinessServiceAgencyRegistration.Status<>'')
	AND (BusinessServiceAgencyRegistration.ApplicationDate>=@FromDate AND BusinessServiceAgencyRegistration.ApplicationDate<=@ToDate)
	AND BusinessServiceAgencyRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN BusinessServiceAgencyRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,BusinessServiceAgencyRegistration.CreatedDate Date,'Business Service Agency' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,BusinessServiceAgencyNo CardLicencePermitNo,
	CASE WHEN BusinessServiceAgencyRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=BusinessServiceAgencyRegistration.Id)
	ELSE [Status].Message END Message,BusinessServiceAgencyRegistration.MemberId,BusinessServiceAgencyRegistration.Status,FullName
	FROM BusinessServiceAgencyRegistration
	INNER JOIN Status ON BusinessServiceAgencyRegistration.Status = [Status].Status
	INNER JOIN PaThaKa ON BusinessServiceAgencyRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON BusinessServiceAgencyRegistration.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (BusinessServiceAgencyRegistration.Status='Approved')
	AND (BusinessServiceAgencyRegistration.CreatedDate>=@FromDate AND BusinessServiceAgencyRegistration.CreatedDate<=@ToDate)
	AND BusinessServiceAgencyRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN BusinessServiceAgencyRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,'Duty Free Shop' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,DutyFreeShopNo CardLicencePermitNo,
	CASE WHEN DutyFreeShopRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=DutyFreeShopRegistration.Id)
	ELSE [Status].Message END Message,DutyFreeShopRegistration.MemberId,DutyFreeShopRegistration.Status,FullName
	FROM DutyFreeShopRegistration
	INNER JOIN Status ON DutyFreeShopRegistration.Status = [Status].Status
	INNER JOIN PaThaKa ON DutyFreeShopRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON DutyFreeShopRegistration.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (DutyFreeShopRegistration.Status<>'Approved' AND DutyFreeShopRegistration.Status<>'')
	AND (DutyFreeShopRegistration.ApplicationDate>=@FromDate AND DutyFreeShopRegistration.ApplicationDate<=@ToDate)
	AND DutyFreeShopRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN DutyFreeShopRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,DutyFreeShopRegistration.CreatedDate Date,'Duty Free Shop' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,DutyFreeShopNo CardLicencePermitNo,
	CASE WHEN DutyFreeShopRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=DutyFreeShopRegistration.Id)
	ELSE [Status].Message END Message,DutyFreeShopRegistration.MemberId,DutyFreeShopRegistration.Status,FullName
	FROM DutyFreeShopRegistration
	INNER JOIN Status ON DutyFreeShopRegistration.Status = [Status].Status
	INNER JOIN PaThaKa ON DutyFreeShopRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON DutyFreeShopRegistration.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (DutyFreeShopRegistration.Status='Approved')
	AND (DutyFreeShopRegistration.CreatedDate>=@FromDate AND DutyFreeShopRegistration.CreatedDate<=@ToDate)
	AND DutyFreeShopRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN DutyFreeShopRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,RegistrationType FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,SaleCenterNo CardLicencePermitNo,
	CASE WHEN SaleCenterRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=SaleCenterRegistration.Id)
	ELSE [Status].Message END Message,SaleCenterRegistration.MemberId,SaleCenterRegistration.Status,FullName
	FROM SaleCenterRegistration
	INNER JOIN Status ON SaleCenterRegistration.Status = [Status].Status
	INNER JOIN PaThaKa ON SaleCenterRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON SaleCenterRegistration.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (SaleCenterRegistration.Status<>'Approved' AND SaleCenterRegistration.Status<>'')
	AND (SaleCenterRegistration.ApplicationDate>=@FromDate AND SaleCenterRegistration.ApplicationDate<=@ToDate)
	AND SaleCenterRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN SaleCenterRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,SaleCenterRegistration.CreatedDate Date,RegistrationType FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,SaleCenterNo CardLicencePermitNo,
	CASE WHEN SaleCenterRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=SaleCenterRegistration.Id)
	ELSE [Status].Message END Message,SaleCenterRegistration.MemberId,SaleCenterRegistration.Status,FullName
	FROM SaleCenterRegistration
	INNER JOIN Status ON SaleCenterRegistration.Status = [Status].Status
	INNER JOIN PaThaKa ON SaleCenterRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON SaleCenterRegistration.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (SaleCenterRegistration.Status='Approved')
	AND (SaleCenterRegistration.CreatedDate>=@FromDate AND SaleCenterRegistration.CreatedDate<=@ToDate)
	AND SaleCenterRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN SaleCenterRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate,RegistrationType FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ShowRoomNo CardLicencePermitNo,
	CASE WHEN ShowRoomRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=ShowRoomRegistration.Id)
	ELSE [Status].Message END Message,ShowRoomRegistration.MemberId,ShowRoomRegistration.Status,FullName
	FROM ShowRoomRegistration
	INNER JOIN Status ON ShowRoomRegistration.Status = [Status].Status
	INNER JOIN PaThaKa ON ShowRoomRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON ShowRoomRegistration.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (ShowRoomRegistration.Status<>'Approved' AND ShowRoomRegistration.Status<>'')
	AND (ShowRoomRegistration.ApplicationDate>=@FromDate AND ShowRoomRegistration.ApplicationDate<=@ToDate)
	AND ShowRoomRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN ShowRoomRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ShowRoomRegistration.CreatedDate Date,RegistrationType FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,ShowRoomNo CardLicencePermitNo,
	CASE WHEN ShowRoomRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=ShowRoomRegistration.Id)
	ELSE [Status].Message END Message,ShowRoomRegistration.MemberId,ShowRoomRegistration.Status,FullName
	FROM ShowRoomRegistration
	INNER JOIN Status ON ShowRoomRegistration.Status = [Status].Status
	INNER JOIN PaThaKa ON ShowRoomRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON ShowRoomRegistration.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (ShowRoomRegistration.Status='Approved')
	AND (ShowRoomRegistration.CreatedDate>=@FromDate AND ShowRoomRegistration.CreatedDate<=@ToDate)
	AND ShowRoomRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN ShowRoomRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,RegistrationType FormType,ApplyType,WholeSaleRetailRegistration.CompanyRegistrationNo,WholeSaleRetailRegistration.CompanyName,WholeSaleRetailNo CardLicencePermitNo,
	CASE WHEN WholeSaleRetailRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=WholeSaleRetailRegistration.Id)
	ELSE [Status].Message END Message,WholeSaleRetailRegistration.MemberId,WholeSaleRetailRegistration.Status,FullName
	FROM WholeSaleRetailRegistration
	INNER JOIN Status ON WholeSaleRetailRegistration.Status = [Status].Status
	INNER JOIN PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON WholeSaleRetailRegistration.MemberId = Member.Id
	WHERE WholeSaleRetailRegistration.CompanyRegistrationNo=@CompanyRegistrationNo 
	AND (WholeSaleRetailRegistration.Status<>'Approved' AND WholeSaleRetailRegistration.Status<>'')
	AND (WholeSaleRetailRegistration.ApplicationDate>=@FromDate AND WholeSaleRetailRegistration.ApplicationDate<=@ToDate)
	AND WholeSaleRetailRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN WholeSaleRetailRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,WholeSaleRetailRegistration.CreatedDate Date,RegistrationType FormType,ApplyType,WholeSaleRetailRegistration.CompanyRegistrationNo,WholeSaleRetailRegistration.CompanyName,WholeSaleRetailNo CardLicencePermitNo,
	CASE WHEN WholeSaleRetailRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=WholeSaleRetailRegistration.Id)
	ELSE [Status].Message END Message,WholeSaleRetailRegistration.MemberId,WholeSaleRetailRegistration.Status,FullName
	FROM WholeSaleRetailRegistration
	INNER JOIN Status ON WholeSaleRetailRegistration.Status = [Status].Status
	INNER JOIN PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON WholeSaleRetailRegistration.MemberId = Member.Id
	WHERE WholeSaleRetailRegistration.CompanyRegistrationNo=@CompanyRegistrationNo 
	AND (WholeSaleRetailRegistration.Status='Approved')
	AND (WholeSaleRetailRegistration.CreatedDate>=@FromDate AND WholeSaleRetailRegistration.CreatedDate<=@ToDate)
	AND WholeSaleRetailRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN WholeSaleRetailRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,ApplicationDate Date,'Alcoholic Beverages Importation' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,WineImportationNo CardLicencePermitNo,
	CASE WHEN WineImportationRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=WineImportationRegistration.Id)
	ELSE [Status].Message END Message,WineImportationRegistration.MemberId,WineImportationRegistration.Status,FullName
	FROM WineImportationRegistration
	INNER JOIN Status ON WineImportationRegistration.Status = [Status].Status
	INNER JOIN PaThaKa ON WineImportationRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON WineImportationRegistration.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (WineImportationRegistration.Status<>'Approved' AND WineImportationRegistration.Status<>'')
	AND (WineImportationRegistration.ApplicationDate>=@FromDate AND WineImportationRegistration.ApplicationDate<=@ToDate)
	AND WineImportationRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN WineImportationRegistration.MemberId ELSE @FilterMemberId END)
	UNION ALL
	SELECT ApplicationNo,WineImportationRegistration.CreatedDate Date,'Alcoholic Beverages Importation' FormType,ApplyType,PaThaKa.CompanyRegistrationNo,CompanyName,WineImportationNo CardLicencePermitNo,
	CASE WHEN WineImportationRegistration.Status='Reject' THEN (SELECT TOP 1 Message FROM Message WHERE TransactionId=WineImportationRegistration.Id)
	ELSE [Status].Message END Message,WineImportationRegistration.MemberId,WineImportationRegistration.Status,FullName
	FROM WineImportationRegistration
	INNER JOIN Status ON WineImportationRegistration.Status = [Status].Status
	INNER JOIN PaThaKa ON WineImportationRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN Member ON WineImportationRegistration.MemberId = Member.Id
	WHERE PaThaKa.CompanyRegistrationNo=@CompanyRegistrationNo AND (WineImportationRegistration.Status='Approved')
	AND (WineImportationRegistration.CreatedDate>=@FromDate AND WineImportationRegistration.CreatedDate<=@ToDate)
	AND WineImportationRegistration.MemberId=(CASE WHEN @FilterMemberId='' THEN WineImportationRegistration.MemberId ELSE @FilterMemberId END))tmp
	ORDER BY tmp.Date DESC
END

GO

/* dbo.sp_AutoCancelDataList */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_AutoCancelDataList]
AS
BEGIN
	SELECT Id TransactionId,'Export Licence' FormType FROM ExportLicence
	WHERE Status='Payment Ready'
	AND DATEDIFF(day,ApproveDate,CURRENT_TIMESTAMP)>=10
	AND IsAutoCancel IS NULL 
	UNION ALL
	SELECT Id TransactionId,'Import Licence' FormType FROM ImportLicence
	WHERE Status='Payment Ready'
	AND DATEDIFF(day,ApproveDate,CURRENT_TIMESTAMP)>=10
	AND IsAutoCancel IS NULL 
	UNION ALL
	SELECT Id TransactionId,'Export Permit' FormType FROM ExportPermit
	WHERE Status='Payment Ready'
	AND DATEDIFF(day,ApproveDate,CURRENT_TIMESTAMP)>=10
	AND IsAutoCancel IS NULL 
	UNION ALL
	SELECT Id TransactionId,'Import Permit' FormType FROM ImportPermit
	WHERE Status='Payment Ready'
	AND DATEDIFF(day,ApproveDate,CURRENT_TIMESTAMP)>=10
	AND IsAutoCancel IS NULL 
	UNION ALL
	SELECT Id TransactionId,'Border Export Licence' FormType FROM BorderExportLicence
	WHERE Status='Payment Ready'
	AND DATEDIFF(day,ApproveDate,CURRENT_TIMESTAMP)>=10
	AND IsAutoCancel IS NULL 
	UNION ALL
	SELECT Id TransactionId,'Border Import Licence' FormType FROM BorderImportLicence
	WHERE Status='Payment Ready'
	AND DATEDIFF(day,ApproveDate,CURRENT_TIMESTAMP)>=10
	AND IsAutoCancel IS NULL 
	UNION ALL
	SELECT Id TransactionId,'Border Export Permit' FormType FROM BorderExportPermit
	WHERE Status='Payment Ready'
	AND DATEDIFF(day,ApproveDate,CURRENT_TIMESTAMP)>=10
	AND IsAutoCancel IS NULL 
	UNION ALL
	SELECT Id TransactionId,'Border Import Permit' FormType FROM BorderImportPermit
	WHERE Status='Payment Ready'
	AND DATEDIFF(day,ApproveDate,CURRENT_TIMESTAMP)>=10
	AND IsAutoCancel IS NULL 
END

GO

/* dbo.sp_BusinessServiceAgencyByPaThakaReport */
CREATE PROCEDURE [dbo].[sp_BusinessServiceAgencyByPaThakaReport] 
   @CompanyRegistrationNo nvarchar(20)     
AS   

 SELECT CompanyRegistrationNo,BusinessServiceAgency.BusinessServiceAgencyNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			BusinessServiceAgency.AuthorizeCompany,BusinessServiceAgency.IssuedDate,BusinessServiceAgency.EndDate
			FROM BusinessServiceAgency
			INNER JOIN PaThaKa ON BusinessServiceAgency.PaThaKaId = PaThaKa.Id
			WHERE CompanyRegistrationNo=@CompanyRegistrationNo

GO

/* dbo.sp_BusinessServiceAgencyRegistrationReport */
CREATE PROCEDURE [dbo].[sp_BusinessServiceAgencyRegistrationReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@PaymentType nvarchar(50),
	@ApplyType nvarchar(50)
AS
BEGIN
	SELECT BusinessServiceAgencyRegistration.CreatedDate Date,PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
	UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
	BusinessServiceAgencyRegistration.BusinessServiceAgencyNo,BusinessServiceAgencyRegistration.AuthorizeCompany,
	PaymentType,VoucherNo,VoucherDate,AccountTransaction.TotalAmount as TotalAmount
	FROM BusinessServiceAgencyRegistration
	INNER JOIN PaThaKa ON BusinessServiceAgencyRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN AccountTransaction ON BusinessServiceAgencyRegistration.Id = AccountTransaction.TransactionId
	WHERE BusinessServiceAgencyRegistration.ApplyType=@ApplyType AND BusinessServiceAgencyRegistration.Status='Approved' AND IsPayment=1
	AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
	AND (BusinessServiceAgencyRegistration.CreatedDate>=@FromDate AND BusinessServiceAgencyRegistration.CreatedDate<=@ToDate)
END

GO

/* dbo.sp_BusinessServiceAgencyReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_BusinessServiceAgencyReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@Date datetime,
	@ApplyType nvarchar(20),
	@Type nvarchar(20) --Summary,Detail
AS
BEGIN
	IF(@Type='Summary')
	BEGIN
		SELECT Count(Id) ApplicationCount,'New' ApplyType FROM BusinessServiceAgencyRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='New' AND Status='Approved'
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Cancel' ApplyType FROM BusinessServiceAgencyRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Cancel' AND Status='Approved'
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Extension' ApplyType FROM BusinessServiceAgencyRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Extension' AND Status='Approved'
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Valid' ApplyType FROM BusinessServiceAgency
		WHERE (EndDate>@Date)
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Invalid' ApplyType FROM BusinessServiceAgency
		WHERE (EndDate<@Date)
	END
	ELSE
	BEGIN

		IF(@ApplyType='Valid')
		BEGIN
			SELECT CompanyRegistrationNo,BusinessServiceAgency.BusinessServiceAgencyNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			BusinessServiceAgency.AuthorizeCompany,BusinessServiceAgency.IssuedDate,BusinessServiceAgency.EndDate
			FROM BusinessServiceAgency
			INNER JOIN PaThaKa ON BusinessServiceAgency.PaThaKaId = PaThaKa.Id
			WHERE BusinessServiceAgency.EndDate>@Date
		END
		ELSE IF(@ApplyType='Invalid')
		BEGIN
			SELECT CompanyRegistrationNo,BusinessServiceAgency.BusinessServiceAgencyNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			BusinessServiceAgency.AuthorizeCompany,BusinessServiceAgency.IssuedDate,BusinessServiceAgency.EndDate
			FROM BusinessServiceAgency
			INNER JOIN PaThaKa ON BusinessServiceAgency.PaThaKaId = PaThaKa.Id
			WHERE BusinessServiceAgency.EndDate<@Date
		END
		ELSE
		BEGIN
			SELECT CompanyRegistrationNo,BusinessServiceAgency.BusinessServiceAgencyNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			BusinessServiceAgency.AuthorizeCompany,BusinessServiceAgency.IssuedDate,BusinessServiceAgency.EndDate
			FROM BusinessServiceAgency
			INNER JOIN PaThaKa ON BusinessServiceAgency.PaThaKaId = PaThaKa.Id
			INNER JOIN BusinessServiceAgencyRegistration ON BusinessServiceAgency.BusinessServiceAgencyNo = BusinessServiceAgencyRegistration.BusinessServiceAgencyNo
			WHERE ApplyType=@ApplyType AND BusinessServiceAgencyRegistration.Status='Approved'
			AND (BusinessServiceAgencyRegistration.CreatedDate>=@FromDate AND BusinessServiceAgencyRegistration.CreatedDate<=@ToDate)	
		END

		
	END
END

GO

/* dbo.sp_CancelReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_ExportLicenceAmendReport ''
CREATE PROCEDURE [dbo].[sp_CancelReport] 
	-- Add the parameters for the stored procedure here
	@FormType nvarchar(50),
	@FromDate datetime,
	@ToDate datetime,
	@ExportImportSectionId int,
	@CompanyRegistrationNo nvarchar(50),
	@SakhanId int
AS
BEGIN
	IF(@FormType='Export Licence')
	BEGIN
		SELECT ExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,ExportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE   ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(ExportLicenceItem.Amount,0) FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Amount,
		ExportLicence.Remark
		FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType='Cancel' AND ExportLicence.Status='Approved'
		AND (ExportLicence.CreatedDate>=@FromDate AND ExportLicence.CreatedDate<=@ToDate)
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Import Licence')
	BEGIN
		SELECT ImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,ImportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Currency,
		(SELECT top 1  ISNULL(ImportLicenceItem.Amount,0) FROM ImportLicenceItem
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Amount,
		ImportLicence.Remark
		FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType='Cancel' AND ImportLicence.Status='Approved'
		AND (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Export Permit')
	BEGIN
		SELECT ExportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,CONVERT(varchar,ExportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ExportPermitItem
		INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Currency,
		(SELECT top 1 ISNULL(ExportPermitItem.Amount,0) FROM ExportPermitItem
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Amount,
		ExportPermit.Remark
		FROM ExportPermit
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType='Cancel' AND ExportPermit.Status='Approved'
		AND (ExportPermit.CreatedDate>=@FromDate AND ExportPermit.CreatedDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Import Permit')
	BEGIN
		SELECT ImportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,CONVERT(varchar,ImportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ImportPermitItem
		INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Currency,
		(SELECT top 1  ISNULL(ImportPermitItem.Amount,0) FROM ImportPermitItem
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Amount,
		ImportPermit.Remark
		FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType='Cancel' AND ImportPermit.Status='Approved'
		AND (ImportPermit.CreatedDate>=@FromDate AND ImportPermit.CreatedDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Border Export Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(SELECT top 1  ISNULL(BorderExportLicenceItem.Amount,0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Amount,BorderExportLicence.Remark,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportLicence
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Cancel' AND BorderExportLicence.Status='Approved' AND CardType='Pa Tha Ka'
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
		IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(SELECT top 1  ISNULL(BorderExportLicenceItem.Amount,0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Amount,BorderExportLicence.Remark,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Cancel' AND BorderExportLicence.Status='Approved' AND CardType='Individual Trading'
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		)tmp
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Import Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT top 1  ISNULL(BorderImportLicenceItem.Amount,0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount,BorderImportLicence.Remark,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Cancel' AND BorderImportLicence.Status='Approved' AND CardType='Pa Tha Ka'
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
		IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT top 1  ISNULL(BorderImportLicenceItem.Amount,0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount,BorderImportLicence.Remark,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Cancel' AND BorderImportLicence.Status='Approved' AND CardType='Individual Trading'
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		)tmp
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Export Permit')
	BEGIN
		SELECT BorderExportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,CONVERT(varchar,BorderExportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportPermitItem
		INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Currency,
		(SELECT top 1  ISNULL(BorderExportPermitItem.Amount,0) FROM BorderExportPermitItem
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Amount,BorderExportPermit.Remark,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportPermit
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		WHERE ApplyType='Cancel' AND BorderExportPermit.Status='Approved'
		AND (BorderExportPermit.CreatedDate>=@FromDate AND BorderExportPermit.CreatedDate<=@ToDate)
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END)
	END
	ELSE IF(@FormType='Border Import Permit')
	BEGIN
		SELECT BorderImportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,CONVERT(varchar,BorderImportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportPermitItem
		INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Currency,
		(SELECT top 1 ISNULL(BorderImportPermitItem.Amount,0) FROM BorderImportPermitItem
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Amount,BorderImportPermit.Remark,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType='Cancel' AND BorderImportPermit.Status='Approved'
		AND (BorderImportPermit.CreatedDate>=@FromDate AND BorderImportPermit.CreatedDate<=@ToDate)
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
	END

	
END

GO

/* dbo.sp_CardListsByPaThaKaReport */
CREATE PROCEDURE [dbo].[sp_CardListsByPaThaKaReport] 
  @CompanyRegistrationNo nvarchar(20) 
AS   

SELECT pathaka.MICPermitNo MICPermitNo,Pathaka.CompanyRegistrationNo,PaThaKa.CompanyName,PaThaka.CompanyRegistrationDate,PaThaKa.EndDate, 
	businessType.Name BusinessType,lineofBusiness.Name LineofBusiness,
	PaThaKa.UnitLevel,PaThaka.StreetNumberStreetName,PaThaka.QuarterCityTownship,PaThaka.State,PaThaKa.Country,PaThaKa.PostalCode,PaThaka.Capital
	FROM PaThaKa
	INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
	INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
	Where PaThaka.CompanyRegistrationNo=@CompanyRegistrationNo




	



	

GO

/* dbo.sp_ChequeNoDetailReport */
-- =============================================
-- Author:      Name
-- Create Date: 
-- Description: 
-- =============================================
--exec sp_ChequeNoDetailReport '2020-08-26','2020-08-26',2
CREATE PROCEDURE [dbo].[sp_ChequeNoDetailReport]
(
    -- Add the parameters for the stored procedure here
   @FromDate datetime,
   @ToDate datetime,
   @ChequeNoId int
)
AS
BEGIN
   SELECT tmp.TransactionId,tmp.FormType,tmp.Code ChequeNo,CONVERT(varchar,tmp.VoucherDate,103) sDate,tmp.TransactionRefNo,tmp.TransactionDateTime,
   tmp.CardNo,tmp.PaThaKaNo,tmp.CompanyName,tmp.UnitLevel,tmp.StreetNumberStreetName,tmp.QuarterCityTownship,tmp.State,tmp.Country,tmp.PostalCode,tmp.Amount
   FROM
	(SELECT AccountTransaction.TransactionId,'Member' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	MemberRegistration.MemberCode CardNo,'-' PaThaKaNo,'-' CompanyName,
	'' UnitLevel,'' StreetNumberStreetName,'' QuarterCityTownship,'' State,
	'' Country,'' PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN MemberRegistration ON MPUPaymentTransaction.TransactionId= MemberRegistration.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Pa Tha Ka' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	PaThaKaRegistration.PaThaKaNo CardNo,PaThaKaRegistration.CompanyRegistrationNo PaThaKaNo,PaThaKaRegistration.CompanyName,
	PaThaKaRegistration.UnitLevel,PaThaKaRegistration.StreetNumberStreetName,PaThaKaRegistration.QuarterCityTownship,PaThaKaRegistration.State,
	PaThaKaRegistration.Country,PaThaKaRegistration.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN PaThaKaRegistration ON MPUPaymentTransaction.TransactionId= PaThaKaRegistration.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Business Service Agency' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	BusinessServiceAgencyRegistration.BusinessServiceAgencyNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN BusinessServiceAgencyRegistration ON MPUPaymentTransaction.TransactionId= BusinessServiceAgencyRegistration.Id
	INNER JOIN PaThaKa ON BusinessServiceAgencyRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Duty Free Shop' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	DutyFreeShopRegistration.DutyFreeShopNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN DutyFreeShopRegistration ON MPUPaymentTransaction.TransactionId= DutyFreeShopRegistration.Id
	INNER JOIN PaThaKa ON DutyFreeShopRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Re-Export' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	ReExportRegistration.ReExportNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN ReExportRegistration ON MPUPaymentTransaction.TransactionId= ReExportRegistration.Id
	INNER JOIN PaThaKa ON ReExportRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,SaleCenterRegistration.RegistrationType FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	SaleCenterRegistration.SaleCenterNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN SaleCenterRegistration ON MPUPaymentTransaction.TransactionId= SaleCenterRegistration.Id
	INNER JOIN PaThaKa ON SaleCenterRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,ShowRoomRegistration.RegistrationType FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	ShowRoomRegistration.ShowRoomNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN ShowRoomRegistration ON MPUPaymentTransaction.TransactionId= ShowRoomRegistration.Id
	INNER JOIN PaThaKa ON ShowRoomRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,WholeSaleRetailRegistration.RegistrationType FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	WholeSaleRetailRegistration.WholeSaleRetailNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN WholeSaleRetailRegistration ON MPUPaymentTransaction.TransactionId= WholeSaleRetailRegistration.Id
	INNER JOIN PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Wine Importation' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	WineImportationRegistration.WineImportationNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN WineImportationRegistration ON MPUPaymentTransaction.TransactionId= WineImportationRegistration.Id
	INNER JOIN PaThaKa ON WineImportationRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Export Licence' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	ExportLicence.ExportLicenceNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN ExportLicence ON MPUPaymentTransaction.TransactionId= ExportLicence.Id
	INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Import Licence' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	ImportLicence.ImportLicenceNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN ImportLicence ON MPUPaymentTransaction.TransactionId= ImportLicence.Id
	INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Export Permit' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	ExportPermit.ExportPermitNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN ExportPermit ON MPUPaymentTransaction.TransactionId= ExportPermit.Id
	INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Import Permit' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	ImportPermit.ImportPermitNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN ImportPermit ON MPUPaymentTransaction.TransactionId= ImportPermit.Id
	INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Border Export Licence' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	BorderExportLicence.ExportLicenceNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN BorderExportLicence ON MPUPaymentTransaction.TransactionId= BorderExportLicence.Id
	INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	AND BorderExportLicence.CardType='Pa Tha Ka'
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Border Export Licence' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	BorderExportLicence.ExportLicenceNo CardNo, IndividualTrading.TINNo PaThaKaNo,IndividualTrading.Name CompanyName,
	IndividualTrading.UnitLevel,IndividualTrading.StreetNumberStreetName,IndividualTrading.QuarterCityTownship,IndividualTrading.State,
	IndividualTrading.Country,IndividualTrading.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN BorderExportLicence ON MPUPaymentTransaction.TransactionId= BorderExportLicence.Id
	INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	AND BorderExportLicence.CardType='Individual Trading'
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Border Export Permit' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	BorderExportPermit.ExportPermitNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN BorderExportPermit ON MPUPaymentTransaction.TransactionId= BorderExportPermit.Id
	INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Border Import Licence' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	BorderImportLicence.ImportLicenceNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN BorderImportLicence ON MPUPaymentTransaction.TransactionId= BorderImportLicence.Id
	INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	AND BorderImportLicence.CardType='Pa Tha Ka'
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Border Import Licence' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	BorderImportLicence.ImportLicenceNo CardNo, IndividualTrading.TINNo PaThaKaNo,IndividualTrading.Name CompanyName,
	IndividualTrading.UnitLevel,IndividualTrading.StreetNumberStreetName,IndividualTrading.QuarterCityTownship,IndividualTrading.State,
	IndividualTrading.Country,IndividualTrading.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN BorderImportLicence ON MPUPaymentTransaction.TransactionId= BorderImportLicence.Id
	INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	AND BorderImportLicence.CardType='Individual Trading'
	UNION ALL
	SELECT AccountTransaction.TransactionId,'Border Import Permit' FormType,ChequeNo.Code,VoucherDate,MPUPaymentTransaction.TransactionRefNo,MPUPaymentTransaction.TransactionDateTime,
	BorderImportPermit.ImportPermitNo CardNo, PaThaKa.CompanyRegistrationNo PaThaKaNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,
	PaThaKa.Country,PaThaKa.PostalCode,AccountTransactionDetail.Amount,
	AccountTitle.Description AccountTitle
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	INNER JOIN MPUPaymentTransaction ON AccountTransaction.TransactionId = MPUPaymentTransaction.TransactionId AND ResponseCode='00'
	INNER JOIN BorderImportPermit ON MPUPaymentTransaction.TransactionId= BorderImportPermit.Id
	INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND ChequeNo.Id=@ChequeNoId
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate))tmp
	ORDER BY tmp.TransactionDateTime
END

GO

/* dbo.sp_ChequeNoReport */

-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_ChequeNoReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@ChequeNoId int
AS
BEGIN
	SELECT tmp.Id ChequeId,tmp.Code ChequeNo,tmp.VoucherDate Date,tmp.sVoucherDate sDate,SUM(Amount) Amount FROM 
	(SELECT ChequeNo.Id,ChequeNo.Code,VoucherDate,CONVERT(varchar,VoucherDate,103) sVoucherDate,Amount
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ChequeNo ON AccountTitle.ChequeNoId = ChequeNo.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	AND ChequeNo.Id = (CASE WHEN @ChequeNoId=0 THEN ChequeNo.Id ELSE @ChequeNoId END)
	)tmp
	GROUP BY tmp.Id,tmp.Code,tmp.VoucherDate,tmp.sVoucherDate
	ORDER BY tmp.VoucherDate,tmp.Id

END

GO

/* dbo.sp_CompanyProfileReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_CompanyProfileReport '2019-01-01','2020-07-29',''
CREATE PROCEDURE [dbo].[sp_CompanyProfileReport] 
	-- Add the parameters for the stored procedure here,
	@FromDate datetime,
	@ToDate datetime,
	@CompanyRegistrationNo nvarchar(20)
AS
BEGIN
	SELECT PaThaKa.Id,CompanyRegistrationNo,EndDate,CompanyName,CompanyRegistrationDate,
	businessType.Name BusinessType,lineofBusiness.Name LineofBusiness,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,Capital,
	PaThaKaDirectors.Name DirectorName,PaThaKaDirectors.NRC DirectorNRC,PaThaKaDirectors.Position DirectorPosition,
	ISNULL(dbo.fn_GetPermitBusiness(PaThaKa.Id),'') PermitBusiness,
	(SELECT COUNT(Id) FROM PaThaKaRegistration
	WHERE PaThaKaRegistration.CompanyRegistrationNo = PaThaKa.CompanyRegistrationNo
	AND PaThaKaRegistration.ApplyType='Extension' AND PaThaKaRegistration.Status='Approved') ExtensionCount
	FROM PaThaKa
	INNER JOIN PaThaKaDirectors ON PaThaKa.Id=PaThaKaDirectors.PaThaKaId
	INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
	INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
	WHERE 
	(PaThaKa.IssuedDate>=@FromDate AND PaThaKa.IssuedDate<=@ToDate)
	AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	ORDER BY IssuedDate
END

GO

/* dbo.sp_DashboardCompleted */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_DashboardCompleted 1,2020,'758553d0-29e6-44b8-b58b-ad0cb0d8e66b'
CREATE PROCEDURE [dbo].[sp_DashboardCompleted]
	-- Add the parameters for the stored procedure here
	@Month int, 
	@Year int,
	@PaThaKaId char(36),
	@IndividualTradingId char(36),
	@MemberId char(36)
AS
BEGIN
	SELECT COUNT(Id) as TotalCount,ApplyType,'Pa Tha Ka' FormType FROM dbo.PaThaKaRegistration
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND MemberId=@MemberId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Individual Trading' FormType FROM dbo.IndividualTradingRegistration
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND MemberId=@MemberId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,RegistrationType FormType FROM dbo.WholeSaleRetailRegistration
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType,RegistrationType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Wine Importation' FormType FROM dbo.WineImportationRegistration
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Duty Free Shop' FormType FROM dbo.DutyFreeShopRegistration
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Re-Export' FormType FROM dbo.ReExportRegistration
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Business Service Agency' FormType FROM dbo.BusinessServiceAgencyRegistration
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,RegistrationType FormType FROM dbo.SaleCenterRegistration
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType,RegistrationType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,RegistrationType FormType FROM dbo.ShowRoomRegistration
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType,RegistrationType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Export Licence' FormType FROM dbo.ExportLicence
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Import Licence' FormType FROM dbo.ImportLicence
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Export Permit' FormType FROM dbo.ExportPermit
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Import Permit' FormType FROM dbo.ImportPermit
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Export Licence' FormType FROM dbo.BorderExportLicence
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year	AND Status='Approved'
	AND ((PaThaKaId=@PaThaKaId and PaThaKaId is not null)
	or (IndividualTradingId=@IndividualTradingId and IndividualTradingId is not null))
	GROUP BY ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Import Licence' FormType FROM dbo.BorderImportLicence
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year	AND Status='Approved'
	AND ((PaThaKaId=@PaThaKaId and PaThaKaId is not null)
	or (IndividualTradingId=@IndividualTradingId and IndividualTradingId is not null))
	GROUP BY ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Export Permit' FormType FROM dbo.BorderExportPermit
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Import Permit' FormType FROM dbo.BorderImportPermit
	WHERE MONTH(CreatedDate)=@Month AND YEAR(CreatedDate)=@Year AND Status='Approved'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
END

GO

/* dbo.sp_DashboardFeedback */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_DashboardCompleted 11,2019,'758553d0-29e6-44b8-b58b-ad0cb0d8e66b'
CREATE PROCEDURE [dbo].[sp_DashboardFeedback]
	-- Add the parameters for the stored procedure here
	@PaThaKaId char(36),
	@IndividualTradingId char(36),
	@MemberId char(36)

AS
BEGIN
	SELECT COUNT(Id) as TotalCount,ApplyType,'Pa Tha Ka' FormType FROM PaThaKaRegistration
	WHERE Status='Reject'
	AND MemberId=@MemberId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Individual Trading' FormType FROM IndividualTradingRegistration
	WHERE Status='Reject'
	AND MemberId=@MemberId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,RegistrationType FormType FROM WholeSaleRetailRegistration
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType,RegistrationType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Wine Importation' FormType FROM WineImportationRegistration
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Duty Free Shop' FormType FROM DutyFreeShopRegistration
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Re-Export' FormType FROM ReExportRegistration
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Business Service Agency' FormType FROM BusinessServiceAgencyRegistration
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,RegistrationType FormType FROM SaleCenterRegistration
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType,RegistrationType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,RegistrationType FormType FROM ShowRoomRegistration
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType,RegistrationType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Export Licence' FormType FROM ExportLicence
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Import Licence' FormType FROM ImportLicence
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Export Permit' FormType FROM ExportPermit
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Import Permit' FormType FROM ImportPermit
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Export Licence' FormType FROM BorderExportLicence
	WHERE Status='Reject'
	AND ((PaThaKaId=@PaThaKaId and PaThaKaId is not null)
	or (IndividualTradingId=@IndividualTradingId and IndividualTradingId is not null))
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Import Licence' FormType FROM BorderImportLicence
	WHERE Status='Reject'
	AND ((PaThaKaId=@PaThaKaId and PaThaKaId is not null)
	or (IndividualTradingId=@IndividualTradingId and IndividualTradingId is not null))
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Export Permit' FormType FROM BorderExportPermit
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Import Permit' FormType FROM BorderImportPermit
	WHERE Status='Reject'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
END

GO

/* dbo.sp_DashboardPayment */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_DashboardPayment 11,2019,'758553d0-29e6-44b8-b58b-ad0cb0d8e66b'
CREATE PROCEDURE [dbo].[sp_DashboardPayment]
	-- Add the parameters for the stored procedure here
	
	@PaThaKaId char(36),
	@IndividualTradingId char(36),
	@MemberId char(36)
AS
BEGIN
	SELECT COUNT(Id) as TotalCount,ApplyType,'Pa Tha Ka' FormType FROM PaThaKaRegistration
	WHERE Status='Payment Ready'
	AND MemberId=@MemberId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Individual Trading' FormType FROM IndividualTradingRegistration
	WHERE Status='Payment Ready'
	AND MemberId=@MemberId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,RegistrationType FormType FROM WholeSaleRetailRegistration
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId AND PaThaKaId<>''
	group by ApplyType,RegistrationType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Wine Importation' FormType FROM WineImportationRegistration
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Duty Free Shop' FormType FROM DutyFreeShopRegistration
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Re-Export' FormType FROM ReExportRegistration
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Business Service Agency' FormType FROM BusinessServiceAgencyRegistration
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,RegistrationType FormType FROM SaleCenterRegistration
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType,RegistrationType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,RegistrationType FormType FROM ShowRoomRegistration
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType,RegistrationType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Export Licence' FormType FROM ExportLicence
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Import Licence' FormType FROM ImportLicence
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Export Permit' FormType FROM ExportPermit
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Import Permit' FormType FROM ImportPermit
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Export Licence' FormType FROM BorderExportLicence
	WHERE Status='Payment Ready'
	AND ((PaThaKaId=@PaThaKaId and PaThaKaId is not null)
	or (IndividualTradingId=@IndividualTradingId and IndividualTradingId is not null))
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Import Licence' FormType FROM BorderImportLicence
	WHERE Status='Payment Ready'
	AND ((PaThaKaId=@PaThaKaId and PaThaKaId is not null)
	or (IndividualTradingId=@IndividualTradingId and IndividualTradingId is not null))
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Export Permit' FormType FROM BorderExportPermit
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Import Permit' FormType FROM BorderImportPermit
	WHERE Status='Payment Ready'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
END

GO

/* dbo.sp_DashboardProgress */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_DashboardCompleted 11,2019,'758553d0-29e6-44b8-b58b-ad0cb0d8e66b'
CREATE PROCEDURE [dbo].[sp_DashboardProgress]
	-- Add the parameters for the stored procedure here
	@PaThaKaId char(36),
	@IndividualTradingId char(36),
	@MemberId char(36)
AS
BEGIN
	SELECT COUNT(Id) as TotalCount,ApplyType,'Pa Tha Ka' FormType FROM PaThaKaRegistration
	WHERE Status='Pending'
	AND MemberId=@MemberId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Individual Trading' FormType FROM IndividualTradingRegistration
	WHERE Status='Pending'
	AND MemberId=@MemberId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,RegistrationType FormType FROM WholeSaleRetailRegistration
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId AND PaThaKaId<>''
	group by ApplyType,RegistrationType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Wine Importation' FormType FROM WineImportationRegistration
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Duty Free Shop' FormType FROM DutyFreeShopRegistration
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Re-Export' FormType FROM ReExportRegistration
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Business Service Agency' FormType FROM BusinessServiceAgencyRegistration
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,RegistrationType FormType FROM SaleCenterRegistration
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType,RegistrationType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,RegistrationType FormType FROM ShowRoomRegistration
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType,RegistrationType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Export Licence' FormType FROM ExportLicence
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Import Licence' FormType FROM ImportLicence
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Export Permit' FormType FROM ExportPermit
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Import Permit' FormType FROM ImportPermit
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Export Licence' FormType FROM BorderExportLicence
	WHERE Status='Pending'
	AND ((PaThaKaId=@PaThaKaId and PaThaKaId is not null)
	or (IndividualTradingId=@IndividualTradingId and IndividualTradingId is not null))
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Import Licence' FormType FROM BorderImportLicence
	WHERE Status='Pending'
	AND ((PaThaKaId=@PaThaKaId and PaThaKaId is not null)
	or (IndividualTradingId=@IndividualTradingId and IndividualTradingId is not null))
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Export Permit' FormType FROM BorderExportPermit
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
	UNION ALL
	SELECT COUNT(Id) as TotalCount,ApplyType,'Border Import Permit' FormType FROM BorderImportPermit
	WHERE Status='Pending'
	AND PaThaKaId=@PaThaKaId
	group by ApplyType
END

GO

/* dbo.sp_DirectorByPaThaKaReport */
CREATE PROCEDURE [dbo].[sp_DirectorByPaThaKaReport] 
 @CompanyRegistrationNo nvarchar(20)  
AS   

    select Name DirectorName,NRC as DirectorNRC
	From PaThaKaDirectors
	INNER JOIN PaThaKa pathaka ON pathaka.Id=PaThaKaDirectors.PaThaKaId
	Where PaThaka.CompanyRegistrationNo=@CompanyRegistrationNo
	

GO

/* dbo.sp_DirectorListReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--EXEC sp_DirectorListReport '2018-07-01','2020-07-28','','','','',0,0,'','Director List'
CREATE PROCEDURE [dbo].[sp_DirectorListReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@CompanyRegistrationNo nvarchar(20),
	@Name nvarchar(200),
	@Nationality nvarchar(200),
	@NRCType nvarchar(50),
	@NRCPrefixId int,
	@NRCPrefixCodeId int,
	@NRCNo nvarchar(20),
	@Type nvarchar(50) -- By Company Registration No,Director List
AS
BEGIN

	DECLARE @FilterNRCNo nvarchar(20)

	SET @FilterNRCNo=dbo.fn_GetNRCNo(@NRCType,@NRCPrefixId,@NRCPrefixCodeId,@NRCNo)


	IF(@Type='By Company Registration No')
	BEGIN
		SELECT CompanyRegistrationNo,CompanyName,CompanyRegistrationDate,EndDate, 
		businessType.Name BusinessType,lineofBusiness.Name LineofBusiness,
		PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,
		directors.Name DirectorName,dbo.fn_GetNRCNo(directors.NRCType,directors.NRCPrefixId,directors.NRCPrefixCodeId,directors.NRCNo) as DirectorNRC,
		directors.Position DirectorPosition,
		directors.UnitLevel DirectorUnitLevel,directors.StreetNumberStreetName DirectorStreetNumberStreetName,
		directors.QuarterCityTownship DirectorQuarterCityTownship,directors.State DirectorState,directors.Country DirectorCountry,directors.PostalCode DirectorPostalCode
		FROM PaThaKa
		INNER JOIN PaThaKaDirectors directors ON PaThaKa.Id = directors.PaThaKaId
		INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
		INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
		WHERE CompanyRegistrationNo=@CompanyRegistrationNo
		ORDER BY directors.SortOrder
	END
	ELSE
	BEGIN
		SELECT * FROM
		(SELECT CompanyRegistrationNo,CompanyName,CompanyRegistrationDate,IssuedDate,EndDate, 
		businessType.Name BusinessType,lineofBusiness.Name LineofBusiness,
		PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,
		directors.Name DirectorName,dbo.fn_GetNRCNo(directors.NRCType,directors.NRCPrefixId,directors.NRCPrefixCodeId,directors.NRCNo) as DirectorNRC,
		directors.Position DirectorPosition,directors.Nationality DirectorNationality,
		directors.UnitLevel DirectorUnitLevel,directors.StreetNumberStreetName DirectorStreetNumberStreetName,
		directors.QuarterCityTownship DirectorQuarterCityTownship,directors.State DirectorState,directors.Country DirectorCountry,directors.PostalCode DirectorPostalCode,
		directors.SortOrder DirectorSortOrder,
		CASE WHEN directors.IsBlackList=1 THEN 'Black List' ELSE '' END DirectorBlackList

		FROM PaThaKa
		INNER JOIN PaThaKaDirectors directors ON PaThaKa.Id = directors.PaThaKaId
		INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
		INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
		WHERE (IssuedDate>=@FromDate AND IssuedDate<=@ToDate))tmp
		WHERE tmp.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' THEN CompanyRegistrationNo END)
		AND tmp.DirectorName = (CASE WHEN @Name='' THEN tmp.DirectorName ELSE @Name END)
		AND tmp.DirectorNationality = (CASE WHEN @Nationality='' THEN tmp.DirectorNationality ELSE @Nationality END)
		AND tmp.DirectorNRC = (CASE WHEN @FilterNRCNo='' THEN tmp.DirectorNRC ELSE @FilterNRCNo END)
		ORDER BY tmp.IssuedDate,tmp.DirectorSortOrder
		
		
	END

	
END

GO

/* dbo.sp_DutyFreeShopByReport */
CREATE PROCEDURE [dbo].[sp_DutyFreeShopByReport]   
    @CompanyRegistrationNo nvarchar(20)   
AS   

   SELECT CompanyRegistrationNo,DutyFreeShop.DutyFreeShopNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			DutyFreeShop.Name,dbo.fn_GetNRCNo(DutyFreeShop.NRCType,DutyFreeShop.NRCPrefixId,DutyFreeShop.NRCPrefixCodeId,DutyFreeShop.NRCNo) NRCNo,
			UnitLevel DutyFreeShopUnitLevel,StreetNumberStreetName DutyFreeShopStreetNumberStreetName,QuarterCityTownship DutyFreeShopQuarterCityTownship,State DutyFreeShopState,Country DutyFreeShopCountry,PostalCode DutyFreeShopPostalCode,
			DutyFreeShop.IssuedDate,DutyFreeShop.EndDate
			FROM DutyFreeShop
			INNER JOIN PaThaKa ON DutyFreeShop.PaThaKaId = PaThaKa.Id
			WHERE CompanyRegistrationNo=@CompanyRegistrationNo

GO

/* dbo.sp_DutyFreeShopRegistrationReport */
CREATE PROCEDURE [dbo].[sp_DutyFreeShopRegistrationReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@PaymentType nvarchar(50),
	@ApplyType nvarchar(50)
AS
BEGIN
	SELECT DutyFreeShopRegistration.CreatedDate Date,PaThaKa.CompanyRegistrationNo,DutyFreeShopRegistration.DutyFreeShopNo,PaThaKa.CompanyName,
	UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
	DutyFreeShopRegistration.Name,dbo.fn_GetNRCNo(DutyFreeShopRegistration.NRCType,DutyFreeShopRegistration.NRCPrefixId,DutyFreeShopRegistration.NRCPrefixCodeId,DutyFreeShopRegistration.NRCNo) NRCNo,
	LocationUnitLevel DutyFreeShopUnitLevel,LocationStreetNumberStreetName DutyFreeShopStreetNumberStreetName,LocationQuarterCityTownship DutyFreeShopQuarterCityTownship,LocationState DutyFreeShopState,LocationCountry DutyFreeShopCountry,LocationPostalCode DutyFreeShopPostalCode,
	PaymentType,VoucherNo,VoucherDate,AccountTransaction.TotalAmount as TotalAmount
	FROM DutyFreeShopRegistration
	INNER JOIN PaThaKa ON DutyFreeShopRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN AccountTransaction ON DutyFreeShopRegistration.Id = AccountTransaction.TransactionId
	WHERE DutyFreeShopRegistration.ApplyType=@ApplyType AND DutyFreeShopRegistration.Status='Approved' AND IsPayment=1
	AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
	AND (DutyFreeShopRegistration.CreatedDate>=@FromDate AND DutyFreeShopRegistration.CreatedDate<=@ToDate)
END

GO

/* dbo.sp_DutyFreeShopReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_DutyFreeShopReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@Date datetime,
	@ApplyType nvarchar(20),
	@Type nvarchar(20) --Summary,Detail
AS
BEGIN
	IF(@Type='Summary')
	BEGIN
		SELECT Count(Id) ApplicationCount,'New' ApplyType FROM DutyFreeShopRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='New' AND Status='Approved'
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Cancel' ApplyType FROM DutyFreeShopRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Cancel' AND Status='Approved'
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Extension' ApplyType FROM DutyFreeShopRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Extension' AND Status='Approved'
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Valid' ApplyType FROM DutyFreeShop
		WHERE (EndDate>@Date)
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Invalid' ApplyType FROM DutyFreeShop
		WHERE (EndDate<@Date)
	END
	ELSE
	BEGIN

		IF(@ApplyType='Valid')
		BEGIN
			SELECT CompanyRegistrationNo,DutyFreeShop.DutyFreeShopNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			DutyFreeShop.Name,dbo.fn_GetNRCNo(DutyFreeShop.NRCType,DutyFreeShop.NRCPrefixId,DutyFreeShop.NRCPrefixCodeId,DutyFreeShop.NRCNo) NRCNo,
			UnitLevel DutyFreeShopUnitLevel,StreetNumberStreetName DutyFreeShopStreetNumberStreetName,QuarterCityTownship DutyFreeShopQuarterCityTownship,State DutyFreeShopState,Country DutyFreeShopCountry,PostalCode DutyFreeShopPostalCode,
			DutyFreeShop.IssuedDate,DutyFreeShop.EndDate
			FROM DutyFreeShop
			INNER JOIN PaThaKa ON DutyFreeShop.PaThaKaId = PaThaKa.Id
			WHERE DutyFreeShop.EndDate>@Date
		END
		ELSE IF(@ApplyType='Invalid')
		BEGIN
			SELECT CompanyRegistrationNo,DutyFreeShop.DutyFreeShopNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			DutyFreeShop.Name,dbo.fn_GetNRCNo(DutyFreeShop.NRCType,DutyFreeShop.NRCPrefixId,DutyFreeShop.NRCPrefixCodeId,DutyFreeShop.NRCNo) NRCNo,
			UnitLevel DutyFreeShopUnitLevel,StreetNumberStreetName DutyFreeShopStreetNumberStreetName,QuarterCityTownship DutyFreeShopQuarterCityTownship,State DutyFreeShopState,Country DutyFreeShopCountry,PostalCode DutyFreeShopPostalCode,
			DutyFreeShop.IssuedDate,DutyFreeShop.EndDate
			FROM DutyFreeShop
			INNER JOIN PaThaKa ON DutyFreeShop.PaThaKaId = PaThaKa.Id
			WHERE DutyFreeShop.EndDate<@Date
		END
		ELSE
		BEGIN
			SELECT CompanyRegistrationNo,DutyFreeShop.DutyFreeShopNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			DutyFreeShop.Name,dbo.fn_GetNRCNo(DutyFreeShop.NRCType,DutyFreeShop.NRCPrefixId,DutyFreeShop.NRCPrefixCodeId,DutyFreeShop.NRCNo) NRCNo,
			UnitLevel DutyFreeShopUnitLevel,StreetNumberStreetName DutyFreeShopStreetNumberStreetName,QuarterCityTownship DutyFreeShopQuarterCityTownship,State DutyFreeShopState,Country DutyFreeShopCountry,PostalCode DutyFreeShopPostalCode,
			DutyFreeShop.IssuedDate,DutyFreeShop.EndDate
			FROM DutyFreeShop
			INNER JOIN PaThaKa ON DutyFreeShop.PaThaKaId = PaThaKa.Id
			INNER JOIN DutyFreeShopRegistration ON DutyFreeShop.DutyFreeShopNo = DutyFreeShopRegistration.DutyFreeShopNo
			WHERE ApplyType=@ApplyType AND DutyFreeShopRegistration.Status='Approved'
			AND (DutyFreeShopRegistration.CreatedDate>=@FromDate AND DutyFreeShopRegistration.CreatedDate<=@ToDate)	
		END

		
	END
END

GO

/* dbo.sp_EICCBalanceCertificateList */

-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
-- exec sp_EICCBalanceCertificateList '','2020-08-12'
CREATE PROCEDURE [dbo].[sp_EICCBalanceCertificateList] 
	-- Add the parameters for the stored procedure here
	@FormType nvarchar(200),
	@EICCDate datetime
AS
BEGIN
	SELECT * FROM 
	(SELECT EICCCertificate.Id EICCId,BusinessServiceAgencyRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN BusinessServiceAgencyRegistration ON EICCCertificate.TransactionId = BusinessServiceAgencyRegistration.Id
	INNER JOIN EICCNo ON BusinessServiceAgencyRegistration.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,DutyFreeShopRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN DutyFreeShopRegistration ON EICCCertificate.TransactionId = DutyFreeShopRegistration.Id
	INNER JOIN EICCNo ON DutyFreeShopRegistration.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,PaThaKaRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN PaThaKaRegistration ON EICCCertificate.TransactionId = PaThaKaRegistration.Id
	INNER JOIN EICCNo ON PaThaKaRegistration.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,ReExportRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN ReExportRegistration ON EICCCertificate.TransactionId = ReExportRegistration.Id
	INNER JOIN EICCNo ON ReExportRegistration.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,SaleCenterRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN SaleCenterRegistration ON EICCCertificate.TransactionId = SaleCenterRegistration.Id
	INNER JOIN EICCNo ON SaleCenterRegistration.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,ShowRoomRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN ShowRoomRegistration ON EICCCertificate.TransactionId = ShowRoomRegistration.Id
	INNER JOIN EICCNo ON ShowRoomRegistration.EICCNoId = EICCNo.Id
	AND EICCCertificate.Status='Pending'
	AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,WholeSaleRetailRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN WholeSaleRetailRegistration ON EICCCertificate.TransactionId = WholeSaleRetailRegistration.Id
	INNER JOIN EICCNo ON WholeSaleRetailRegistration.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending'
	AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,WineImportationRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN WineImportationRegistration ON EICCCertificate.TransactionId = WineImportationRegistration.Id
	INNER JOIN EICCNo ON WineImportationRegistration.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending'
	AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,ExportLicence.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN ExportLicence ON EICCCertificate.TransactionId = ExportLicence.Id
	INNER JOIN EICCNo ON ExportLicence.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,ImportLicence.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN ImportLicence ON EICCCertificate.TransactionId = ImportLicence.Id
	INNER JOIN EICCNo ON ImportLicence.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,ExportPermit.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN ExportPermit ON EICCCertificate.TransactionId = ExportPermit.Id
	INNER JOIN EICCNo ON ExportPermit.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,ImportPermit.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN ImportPermit ON EICCCertificate.TransactionId = ImportPermit.Id
	INNER JOIN EICCNo ON ImportPermit.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,BorderExportLicence.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN BorderExportLicence ON EICCCertificate.TransactionId = BorderExportLicence.Id
	INNER JOIN EICCNo ON BorderExportLicence.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,BorderImportLicence.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN BorderImportLicence ON EICCCertificate.TransactionId = BorderImportLicence.Id
	INNER JOIN EICCNo ON BorderImportLicence.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,BorderExportPermit.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN BorderExportPermit ON EICCCertificate.TransactionId = BorderExportPermit.Id
	INNER JOIN EICCNo ON BorderExportPermit.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	UNION ALL
	SELECT EICCCertificate.Id EICCId,BorderImportPermit.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
	EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
	FROM EICCCertificate
	INNER JOIN BorderImportPermit ON EICCCertificate.TransactionId = BorderImportPermit.Id
	INNER JOIN EICCNo ON BorderImportPermit.EICCNoId = EICCNo.Id
	WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
	AND EICCCertificate.EICCDate<@EICCDate
	) tmp
	WHERE tmp.FormType LIKE (CASE WHEN @FormType='' THEN tmp.FormType ELSE @FormType+'%' END)
	ORDER BY tmp.CreatedDate
END

GO

/* dbo.sp_EICCPendingCertificateList */

-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
-- exec sp_EICCPendingCertificateList 'Certificate','','2020-08-12',0,0
CREATE PROCEDURE [dbo].[sp_EICCPendingCertificateList] 
	-- Add the parameters for the stored procedure here
	@Type nvarchar(200),
	@FormType nvarchar(200),
	@EICCDate datetime,
	@ProductGroupId int,
	@ProductItemId int
AS
BEGIN
	IF(@Type='Certificate')
	BEGIN
		SELECT * FROM 
		(SELECT EICCCertificate.Id EICCId,BusinessServiceAgencyRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN BusinessServiceAgencyRegistration ON EICCCertificate.TransactionId = BusinessServiceAgencyRegistration.Id
		INNER JOIN EICCNo ON BusinessServiceAgencyRegistration.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,DutyFreeShopRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN DutyFreeShopRegistration ON EICCCertificate.TransactionId = DutyFreeShopRegistration.Id
		INNER JOIN EICCNo ON DutyFreeShopRegistration.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,PaThaKaRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN PaThaKaRegistration ON EICCCertificate.TransactionId = PaThaKaRegistration.Id
		INNER JOIN EICCNo ON PaThaKaRegistration.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,ReExportRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN ReExportRegistration ON EICCCertificate.TransactionId = ReExportRegistration.Id
		INNER JOIN EICCNo ON ReExportRegistration.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,SaleCenterRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN SaleCenterRegistration ON EICCCertificate.TransactionId = SaleCenterRegistration.Id
		INNER JOIN EICCNo ON SaleCenterRegistration.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,ShowRoomRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN ShowRoomRegistration ON EICCCertificate.TransactionId = ShowRoomRegistration.Id
		INNER JOIN EICCNo ON ShowRoomRegistration.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.FormType like 'Show Room%'
		AND EICCCertificate.Status='Pending'
		AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,WholeSaleRetailRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN WholeSaleRetailRegistration ON EICCCertificate.TransactionId = WholeSaleRetailRegistration.Id
		INNER JOIN EICCNo ON WholeSaleRetailRegistration.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending'
		AND EICCCertificate.IsFinish=0
		AND EICCCertificate.EICCDate=@EICCDate
		UNION ALL
		SELECT EICCCertificate.Id EICCId,WineImportationRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN WineImportationRegistration ON EICCCertificate.TransactionId = WineImportationRegistration.Id
		INNER JOIN EICCNo ON WineImportationRegistration.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending'
		AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)) tmp
		WHERE tmp.FormType LIKE (CASE WHEN @FormType='' THEN tmp.FormType ELSE @FormType+'%' END)
		ORDER BY tmp.CreatedDate
	END
	ELSE IF(@Type='LicencePermit')
	BEGIN
		SELECT * FROM 
		(SELECT EICCCertificate.Id EICCId,ExportLicence.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN ExportLicence ON EICCCertificate.TransactionId = ExportLicence.Id
		INNER JOIN EICCNo ON ExportLicence.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=@ProductGroupId
		AND EICCCertificate.ProductItemId=@ProductItemId
		UNION ALL
		SELECT EICCCertificate.Id EICCId,ImportLicence.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN ImportLicence ON EICCCertificate.TransactionId = ImportLicence.Id
		INNER JOIN EICCNo ON ImportLicence.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=@ProductGroupId
		AND EICCCertificate.ProductItemId=@ProductItemId
		UNION ALL
		SELECT EICCCertificate.Id EICCId,ExportPermit.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN ExportPermit ON EICCCertificate.TransactionId = ExportPermit.Id
		INNER JOIN EICCNo ON ExportPermit.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=@ProductGroupId
		AND EICCCertificate.ProductItemId=@ProductItemId
		UNION ALL
		SELECT EICCCertificate.Id EICCId,ImportPermit.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN ImportPermit ON EICCCertificate.TransactionId = ImportPermit.Id
		INNER JOIN EICCNo ON ImportPermit.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=@ProductGroupId
		AND EICCCertificate.ProductItemId=@ProductItemId
		) tmp
		WHERE tmp.FormType LIKE (CASE WHEN @FormType='' THEN tmp.FormType ELSE @FormType+'%' END)
		ORDER BY tmp.CreatedDate
	END
	ELSE IF(@Type='BorderLicencePermit')
	BEGIN
		SELECT * FROM 
		(SELECT EICCCertificate.Id EICCId,BorderExportLicence.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN BorderExportLicence ON EICCCertificate.TransactionId = BorderExportLicence.Id
		INNER JOIN EICCNo ON BorderExportLicence.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=@ProductGroupId
		AND EICCCertificate.ProductItemId=@ProductItemId
		UNION ALL
		SELECT EICCCertificate.Id EICCId,BorderImportLicence.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN BorderImportLicence ON EICCCertificate.TransactionId = BorderImportLicence.Id
		INNER JOIN EICCNo ON BorderImportLicence.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=@ProductGroupId
		AND EICCCertificate.ProductItemId=@ProductItemId
		UNION ALL
		SELECT EICCCertificate.Id EICCId,BorderExportPermit.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN BorderExportPermit ON EICCCertificate.TransactionId = BorderExportPermit.Id
		INNER JOIN EICCNo ON BorderExportPermit.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=@ProductGroupId
		AND EICCCertificate.ProductItemId=@ProductItemId
		UNION ALL
		SELECT EICCCertificate.Id EICCId,BorderImportPermit.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate
		FROM EICCCertificate
		INNER JOIN BorderImportPermit ON EICCCertificate.TransactionId = BorderImportPermit.Id
		INNER JOIN EICCNo ON BorderImportPermit.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status='Pending' AND EICCCertificate.IsFinish=0
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=@ProductGroupId
		AND EICCCertificate.ProductItemId=@ProductItemId
		) tmp
		WHERE tmp.FormType LIKE (CASE WHEN @FormType='' THEN tmp.FormType ELSE @FormType+'%' END)
		ORDER BY tmp.CreatedDate
	END


	
END

GO

/* dbo.sp_EICCReport */

-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
-- exec sp_EICCReport 'LicencePermit','','2020-09-04',0,0,'Approved'
CREATE PROCEDURE [dbo].[sp_EICCReport] 
	-- Add the parameters for the stored procedure here
	@Type nvarchar(200),
	@FormType nvarchar(200),
	@EICCDate datetime,
	@ProductGroupId int,
	@ProductItemId int,
	@EICCStatus nvarchar(50)
AS
BEGIN
	IF(@Type='Certificate')
	BEGIN
		SELECT * FROM 
		(SELECT EICCCertificate.Id EICCId,BusinessServiceAgencyRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN BusinessServiceAgencyRegistration ON EICCCertificate.TransactionId = BusinessServiceAgencyRegistration.Id
		INNER JOIN EICCNo ON BusinessServiceAgencyRegistration.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON BusinessServiceAgencyRegistration.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,DutyFreeShopRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN DutyFreeShopRegistration ON EICCCertificate.TransactionId = DutyFreeShopRegistration.Id
		INNER JOIN EICCNo ON DutyFreeShopRegistration.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON DutyFreeShopRegistration.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,PaThaKaRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKaRegistration.CompanyRegistrationNo PaThaKaNo,PaThaKaRegistration.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN PaThaKaRegistration ON EICCCertificate.TransactionId = PaThaKaRegistration.Id
		INNER JOIN EICCNo ON PaThaKaRegistration.EICCNoId = EICCNo.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,ReExportRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN ReExportRegistration ON EICCCertificate.TransactionId = ReExportRegistration.Id
		INNER JOIN EICCNo ON ReExportRegistration.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON ReExportRegistration.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,SaleCenterRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN SaleCenterRegistration ON EICCCertificate.TransactionId = SaleCenterRegistration.Id
		INNER JOIN EICCNo ON SaleCenterRegistration.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON SaleCenterRegistration.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,ShowRoomRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN ShowRoomRegistration ON EICCCertificate.TransactionId = ShowRoomRegistration.Id
		INNER JOIN EICCNo ON ShowRoomRegistration.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON ShowRoomRegistration.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.FormType like 'Show Room%'
		AND EICCCertificate.Status=@EICCStatus
		
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,WholeSaleRetailRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN WholeSaleRetailRegistration ON EICCCertificate.TransactionId = WholeSaleRetailRegistration.Id
		INNER JOIN EICCNo ON WholeSaleRetailRegistration.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus
		
		AND EICCCertificate.EICCDate=@EICCDate
		UNION ALL
		SELECT EICCCertificate.Id EICCId,WineImportationRegistration.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN WineImportationRegistration ON EICCCertificate.TransactionId = WineImportationRegistration.Id
		INNER JOIN EICCNo ON WineImportationRegistration.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON WineImportationRegistration.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus
		
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)) tmp
		WHERE tmp.FormType LIKE (CASE WHEN @FormType='' THEN tmp.FormType ELSE @FormType+'%' END)
		ORDER BY tmp.CreatedDate
	END
	ELSE IF(@Type='LicencePermit')
	BEGIN
		SELECT * FROM 
		(SELECT EICCCertificate.Id EICCId,ExportLicence.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN ExportLicence ON EICCCertificate.TransactionId = ExportLicence.Id
		INNER JOIN EICCNo ON ExportLicence.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND  EICCCertificate.ProductGroupId=@ProductGroupId
		AND EICCCertificate.ProductItemId=@ProductItemId
		UNION ALL
		SELECT EICCCertificate.Id EICCId,ImportLicence.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN ImportLicence ON EICCCertificate.TransactionId = ImportLicence.Id
		INNER JOIN EICCNo ON ImportLicence.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=(CASE WHEN @ProductGroupId=0 THEN EICCCertificate.ProductGroupId ELSE @ProductGroupId END)
		AND EICCCertificate.ProductItemId=(CASE WHEN @ProductItemId=0 THEN EICCCertificate.ProductItemId ELSE @ProductItemId END)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,ExportPermit.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN ExportPermit ON EICCCertificate.TransactionId = ExportPermit.Id
		INNER JOIN EICCNo ON ExportPermit.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=(CASE WHEN @ProductGroupId=0 THEN EICCCertificate.ProductGroupId ELSE @ProductGroupId END)
		AND EICCCertificate.ProductItemId=(CASE WHEN @ProductItemId=0 THEN EICCCertificate.ProductItemId ELSE @ProductItemId END)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,ImportPermit.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN ImportPermit ON EICCCertificate.TransactionId = ImportPermit.Id
		INNER JOIN EICCNo ON ImportPermit.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=(CASE WHEN @ProductGroupId=0 THEN EICCCertificate.ProductGroupId ELSE @ProductGroupId END)
		AND EICCCertificate.ProductItemId=(CASE WHEN @ProductItemId=0 THEN EICCCertificate.ProductItemId ELSE @ProductItemId END)
		) tmp
		WHERE tmp.FormType LIKE (CASE WHEN @FormType='' THEN tmp.FormType ELSE @FormType+'%' END)
		ORDER BY tmp.CreatedDate
	END
	ELSE IF(@Type='BorderLicencePermit')
	BEGIN
		SELECT * FROM 
		(SELECT EICCCertificate.Id EICCId,BorderExportLicence.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN BorderExportLicence ON EICCCertificate.TransactionId = BorderExportLicence.Id
		INNER JOIN EICCNo ON BorderExportLicence.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=(CASE WHEN @ProductGroupId=0 THEN EICCCertificate.ProductGroupId ELSE @ProductGroupId END)
		AND EICCCertificate.ProductItemId=(CASE WHEN @ProductItemId=0 THEN EICCCertificate.ProductItemId ELSE @ProductItemId END)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,BorderImportLicence.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN BorderImportLicence ON EICCCertificate.TransactionId = BorderImportLicence.Id
		INNER JOIN EICCNo ON BorderImportLicence.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=(CASE WHEN @ProductGroupId=0 THEN EICCCertificate.ProductGroupId ELSE @ProductGroupId END)
		AND EICCCertificate.ProductItemId=(CASE WHEN @ProductItemId=0 THEN EICCCertificate.ProductItemId ELSE @ProductItemId END)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,BorderExportPermit.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN BorderExportPermit ON EICCCertificate.TransactionId = BorderExportPermit.Id
		INNER JOIN EICCNo ON BorderExportPermit.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=(CASE WHEN @ProductGroupId=0 THEN EICCCertificate.ProductGroupId ELSE @ProductGroupId END)
		AND EICCCertificate.ProductItemId=(CASE WHEN @ProductItemId=0 THEN EICCCertificate.ProductItemId ELSE @ProductItemId END)
		UNION ALL
		SELECT EICCCertificate.Id EICCId,BorderImportPermit.Id,EICCCertificate.FormType,ApplyType, ApplicationNo,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCCertificate.EICCDate,CONVERT(varchar,EICCCertificate.EICCDate,103) sEICCDate,EICCStatus,EICCCertificate.CreatedDate,
		PaThaKa.PaThaKaNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,EICCCertificate.Remark
		FROM EICCCertificate
		INNER JOIN BorderImportPermit ON EICCCertificate.TransactionId = BorderImportPermit.Id
		INNER JOIN EICCNo ON BorderImportPermit.EICCNoId = EICCNo.Id
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		WHERE EICCCertificate.Status=@EICCStatus 
		AND (EICCCertificate.EICCDate>=@EICCDate AND EICCCertificate.EICCDate<=@EICCDate)
		AND EICCCertificate.ProductGroupId=(CASE WHEN @ProductGroupId=0 THEN EICCCertificate.ProductGroupId ELSE @ProductGroupId END)
		AND EICCCertificate.ProductItemId=(CASE WHEN @ProductItemId=0 THEN EICCCertificate.ProductItemId ELSE @ProductItemId END)
		) tmp
		WHERE tmp.FormType LIKE (CASE WHEN @FormType='' THEN tmp.FormType ELSE @FormType+'%' END)
		ORDER BY tmp.CreatedDate
	END


	
END

GO

/* dbo.sp_EICCSubmitBorderLicencePermitList */

-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_EICCSubmitCertificateList 'Pending',3
CREATE PROCEDURE [dbo].[sp_EICCSubmitBorderLicencePermitList] 
	-- Add the parameters for the stored procedure here
	@EICCStatus nvarchar(50),
	@UserId int
AS
BEGIN

	IF(@EICCStatus<>'Approved')
	BEGIN
		SELECT * FROM
		(SELECT BorderExportLicence.Id,'Export Licence' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM BorderExportLicence
		INNER JOIN EICCNo ON BorderExportLicence.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT BorderImportLicence.Id,'Import Licence' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM BorderImportLicence
		INNER JOIN EICCNo ON BorderImportLicence.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT BorderExportPermit.Id,'Export Permit' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM BorderExportPermit
		INNER JOIN EICCNo ON BorderExportPermit.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT BorderImportPermit.Id,'Import Permit' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM BorderImportPermit
		INNER JOIN EICCNo ON BorderImportPermit.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		) tmp
		ORDER BY tmp.ApplicationDate
	END
	ELSE
	BEGIN
		SELECT * FROM
		(SELECT BorderExportLicence.Id,'Export Licence' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM BorderExportLicence
		INNER JOIN EICCNo ON BorderExportLicence.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		UNION ALL
		SELECT BorderImportLicence.Id,'Import Licence' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM BorderImportLicence
		INNER JOIN EICCNo ON BorderImportLicence.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		UNION ALL
		SELECT BorderExportPermit.Id,'Export Permit' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM BorderExportPermit
		INNER JOIN EICCNo ON BorderExportPermit.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		UNION ALL
		SELECT BorderImportPermit.Id,'Import Permit' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM BorderImportPermit
		INNER JOIN EICCNo ON BorderImportPermit.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		) tmp
		ORDER BY tmp.ApplicationDate
	END


	
END

GO

/* dbo.sp_EICCSubmitCertificateList */

-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_EICCSubmitCertificateList 'Pending',3
CREATE PROCEDURE [dbo].[sp_EICCSubmitCertificateList] 
	-- Add the parameters for the stored procedure here
	@EICCStatus nvarchar(50),
	@UserId int
AS
BEGIN

	IF(@EICCStatus<>'Approved')
	BEGIN
		SELECT * FROM
		(SELECT BusinessServiceAgencyRegistration.Id,'Business Service Agency' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM BusinessServiceAgencyRegistration
		INNER JOIN PaThaKa ON BusinessServiceAgencyRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON BusinessServiceAgencyRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT DutyFreeShopRegistration.Id,'Duty Free Shop' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM DutyFreeShopRegistration
		INNER JOIN PaThaKa ON DutyFreeShopRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON DutyFreeShopRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT PaThaKaRegistration.Id,'Pa Tha Ka' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKaRegistration.CompanyRegistrationNo,PaThaKaRegistration.CompanyName,
		CASE WHEN (SELECT EndDate FROM PaThaKa WHERE PaThaKa.CompanyRegistrationNo = PaThaKaRegistration.CompanyRegistrationNo)=null THEN '-'
		ELSE (SELECT EndDate FROM PaThaKa WHERE PaThaKa.CompanyRegistrationNo = PaThaKaRegistration.CompanyRegistrationNo) END sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM PaThaKaRegistration
		INNER JOIN EICCNo ON PaThaKaRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT ReExportRegistration.Id,'Re-Export' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM ReExportRegistration
		INNER JOIN PaThaKa ON ReExportRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON ReExportRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT SaleCenterRegistration.Id,RegistrationType FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM SaleCenterRegistration
		INNER JOIN PaThaKa ON SaleCenterRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON SaleCenterRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT ShowRoomRegistration.Id,RegistrationType FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM ShowRoomRegistration
		INNER JOIN PaThaKa ON ShowRoomRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON ShowRoomRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT WholeSaleRetailRegistration.Id,RegistrationType FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM WholeSaleRetailRegistration
		INNER JOIN PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON WholeSaleRetailRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT WineImportationRegistration.Id,'Wine Importation' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM WineImportationRegistration
		INNER JOIN PaThaKa ON WineImportationRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON WineImportationRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1) tmp
		ORDER BY tmp.ApplicationDate
	END
	ELSE
	BEGIN
		SELECT * FROM
		(SELECT BusinessServiceAgencyRegistration.Id,'Business Service Agency' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM BusinessServiceAgencyRegistration
		INNER JOIN PaThaKa ON BusinessServiceAgencyRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON BusinessServiceAgencyRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT DutyFreeShopRegistration.Id,'Duty Free Shop' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM DutyFreeShopRegistration
		INNER JOIN PaThaKa ON DutyFreeShopRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON DutyFreeShopRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		UNION ALL
		SELECT PaThaKaRegistration.Id,'Pa Tha Ka' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKaRegistration.CompanyRegistrationNo,PaThaKaRegistration.CompanyName,
		CASE WHEN (SELECT EndDate FROM PaThaKa WHERE PaThaKa.CompanyRegistrationNo = PaThaKaRegistration.CompanyRegistrationNo)=null THEN '-'
		ELSE (SELECT EndDate FROM PaThaKa WHERE PaThaKa.CompanyRegistrationNo = PaThaKaRegistration.CompanyRegistrationNo) END sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM PaThaKaRegistration
		INNER JOIN EICCNo ON PaThaKaRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		UNION ALL
		SELECT ReExportRegistration.Id,'Re-Export' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM ReExportRegistration
		INNER JOIN PaThaKa ON ReExportRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON ReExportRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		UNION ALL
		SELECT SaleCenterRegistration.Id,RegistrationType FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM SaleCenterRegistration
		INNER JOIN PaThaKa ON SaleCenterRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON SaleCenterRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		UNION ALL
		SELECT ShowRoomRegistration.Id,RegistrationType FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM ShowRoomRegistration
		INNER JOIN PaThaKa ON ShowRoomRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON ShowRoomRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		UNION ALL
		SELECT WholeSaleRetailRegistration.Id,RegistrationType FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM WholeSaleRetailRegistration
		INNER JOIN PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON WholeSaleRetailRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		UNION ALL
		SELECT WineImportationRegistration.Id,'Wine Importation' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		PaThaKa.CompanyRegistrationNo,Pathaka.CompanyName,CONVERT(varchar,PaThaKa.EndDate,103) sEndDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM WineImportationRegistration
		INNER JOIN PaThaKa ON WineImportationRegistration.PaThaKaId = PaThaKa.Id
		INNER JOIN EICCNo ON WineImportationRegistration.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		) tmp
		ORDER BY tmp.ApplicationDate
	END


	
END

GO

/* dbo.sp_EICCSubmitLicencePermitList */

-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_EICCSubmitCertificateList 'Pending',3
CREATE PROCEDURE [dbo].[sp_EICCSubmitLicencePermitList] 
	-- Add the parameters for the stored procedure here
	@EICCStatus nvarchar(50),
	@UserId int
AS
BEGIN

	IF(@EICCStatus<>'Approved')
	BEGIN
		SELECT * FROM
		(SELECT ExportLicence.Id,'Export Licence' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM ExportLicence
		INNER JOIN EICCNo ON ExportLicence.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT ImportLicence.Id,'Import Licence' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM ImportLicence
		INNER JOIN EICCNo ON ImportLicence.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT ExportPermit.Id,'Export Permit' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM ExportPermit
		INNER JOIN EICCNo ON ExportPermit.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		UNION ALL
		SELECT ImportPermit.Id,'Import Permit' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM ImportPermit
		INNER JOIN EICCNo ON ImportPermit.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		) tmp
		ORDER BY tmp.ApplicationDate
	END
	ELSE
	BEGIN
		SELECT * FROM
		(SELECT ExportLicence.Id,'Export Licence' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM ExportLicence
		INNER JOIN EICCNo ON ExportLicence.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		UNION ALL
		SELECT ImportLicence.Id,'Import Licence' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM ImportLicence
		INNER JOIN EICCNo ON ImportLicence.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		UNION ALL
		SELECT ExportPermit.Id,'Export Permit' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM ExportPermit
		INNER JOIN EICCNo ON ExportPermit.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		UNION ALL
		SELECT ImportPermit.Id,'Import Permit' FormType,ApplyType,ApplicationNo,ApplicationDate,CONVERT(varchar,ApplicationDate,103) sApplicationDate,
		EICCNo.Code EICCNo,EICCDate,CONVERT(varchar,EICCDate,103) sEICCDate,EICCStatus
		FROM ImportPermit
		INNER JOIN EICCNo ON ImportPermit.EICCNoId = EICCNo.Id
		WHERE EICCStatus=@EICCStatus AND ApproveUserId=@UserId
		AND IsEICCSubmit=1
		AND IsApprove=0
		) tmp
		ORDER BY tmp.ApplicationDate
	END


	
END

GO

/* dbo.sp_EVCycleShowRoomRegistrationReport */

CREATE PROCEDURE [dbo].[sp_EVCycleShowRoomRegistrationReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@PaymentType nvarchar(50),
	@ApplyType nvarchar(50),
	@RegistrationType nvarchar(200)
AS
BEGIN
	SELECT EVCycleShowRoomRegistration.CreatedDate Date,PaThaKa.CompanyRegistrationNo,EVCycleShowRoomRegistration.ShowRoomNo,PaThaKa.CompanyName,
	UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
	EVCycleShowRoomRegistration.ShowRoomNo,EVCycleShowRoomRegistration.Name,dbo.fn_GetNRCNo(EVCycleShowRoomRegistration.NRCType,EVCycleShowRoomRegistration.NRCPrefixId,EVCycleShowRoomRegistration.NRCPrefixCodeId,EVCycleShowRoomRegistration.NRCNo) NRCNo,
	CASE WHEN EVCycleShowRoomRegistration.BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=EVCycleShowRoomRegistration.BusinessServiceAgencyId) END BusinessServiceAgencyNo,
	ShowRoomUnitLevel,ShowRoomStreetNumberStreetName,ShowRoomQuarterCityTownship,ShowRoomState,ShowRoomCountry,ShowRoomPostalCode,
	ShowRoomUnitLevel2,ShowRoomStreetNumberStreetName2,ShowRoomQuarterCityTownship2,ShowRoomState2,ShowRoomCountry2,ShowRoomPostalCode2,
	ShowRoomUnitLevel3,ShowRoomStreetNumberStreetName3,ShowRoomQuarterCityTownship3,ShowRoomState3,ShowRoomCountry3,ShowRoomPostalCode3,
	ShowRoomUnitLevel4,ShowRoomStreetNumberStreetName4,ShowRoomQuarterCityTownship4,ShowRoomState4,ShowRoomCountry4,ShowRoomPostalCode4,
	ShowRoomUnitLevel5,ShowRoomStreetNumberStreetName5,ShowRoomQuarterCityTownship5,ShowRoomState5,ShowRoomCountry5,ShowRoomPostalCode5,
	PaymentType,VoucherNo,VoucherDate,AccountTransaction.TotalAmount as TotalAmount
	FROM EVCycleShowRoomRegistration
	INNER JOIN PaThaKa ON EVCycleShowRoomRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN AccountTransaction ON EVCycleShowRoomRegistration.Id = AccountTransaction.TransactionId
	WHERE EVCycleShowRoomRegistration.ApplyType=@ApplyType AND EVCycleShowRoomRegistration.Status='Approved' AND IsPayment=1
	AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
	AND (EVCycleShowRoomRegistration.CreatedDate>=@FromDate AND EVCycleShowRoomRegistration.CreatedDate<=@ToDate)
	AND (EVCycleShowRoomRegistration.RegistrationType=@RegistrationType)
END

GO

/* dbo.sp_EVCycleShowRoomReport */


-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
Create PROCEDURE [dbo].[sp_EVCycleShowRoomReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@Date datetime,
	@ApplyType nvarchar(20),
	@FormType nvarchar(50),
	@Type nvarchar(20) --Summary,Detail
AS
BEGIN
	IF(@Type='Summary')
	BEGIN
		SELECT Count(Id) ApplicationCount,'New' ApplyType FROM EVCycleShowRoomRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='New' AND Status='Approved'
		AND RegistrationType=@FormType
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Cancel' ApplyType FROM EVCycleShowRoomRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Cancel' AND Status='Approved'
		AND RegistrationType=@FormType
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Extension' ApplyType FROM EVCycleShowRoomRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Extension' AND Status='Approved'
		AND RegistrationType=@FormType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Valid' ApplyType FROM EVCycleShowRoom
		WHERE (EndDate>@Date)
		AND RegistrationType=@FormType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Invalid' ApplyType FROM EVCycleShowRoom
		WHERE (EndDate<@Date)
		AND RegistrationType=@FormType
	END
	ELSE
	BEGIN

		IF(@ApplyType='Valid')
		BEGIN
			SELECT PaThaKa.CompanyRegistrationNo,EVCycleShowRoom.ShowRoomNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			EVCycleShowRoom.Name,dbo.fn_GetNRCNo(EVCycleShowRoom.NRCType,EVCycleShowRoom.NRCPrefixId,EVCycleShowRoom.NRCPrefixCodeId,EVCycleShowRoom.NRCNo) NRCNo,
			CASE WHEN BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel ShowRoomUnitLevel,StreetNumberStreetName ShowRoomStreetNumberStreetName,QuarterCityTownship ShowRoomQuarterCityTownship,State ShowRoomState,Country ShowRoomCountry,PostalCode ShowRoomPostalCode,
			EVCycleShowRoom.IssuedDate,EVCycleShowRoom.EndDate
			FROM EVCycleShowRoom
			INNER JOIN PaThaKa ON EVCycleShowRoom.PaThaKaId = PaThaKa.Id
			WHERE EVCycleShowRoom.EndDate>@Date
			AND RegistrationType=@FormType
		END
		ELSE IF(@ApplyType='Invalid')
		BEGIN

			SELECT PaThaKa.CompanyRegistrationNo,EVCycleShowRoom.ShowRoomNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			EVCycleShowRoom.Name,dbo.fn_GetNRCNo(EVCycleShowRoom.NRCType,EVCycleShowRoom.NRCPrefixId,EVCycleShowRoom.NRCPrefixCodeId,EVCycleShowRoom.NRCNo) NRCNo,
			CASE WHEN BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel ShowRoomUnitLevel,StreetNumberStreetName ShowRoomStreetNumberStreetName,QuarterCityTownship ShowRoomQuarterCityTownship,State ShowRoomState,Country ShowRoomCountry,PostalCode ShowRoomPostalCode,
			EVCycleShowRoom.IssuedDate,EVCycleShowRoom.EndDate
			FROM EVCycleShowRoom
			INNER JOIN PaThaKa ON EVCycleShowRoom.PaThaKaId = PaThaKa.Id
			WHERE EVCycleShowRoom.EndDate<@Date
			AND RegistrationType=@FormType
		END
		ELSE
		BEGIN
			SELECT PaThaKa.CompanyRegistrationNo,EVCycleShowRoom.ShowRoomNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			EVCycleShowRoom.Name,dbo.fn_GetNRCNo(EVCycleShowRoom.NRCType,EVCycleShowRoom.NRCPrefixId,EVCycleShowRoom.NRCPrefixCodeId,EVCycleShowRoom.NRCNo) NRCNo,
			CASE WHEN EVCycleShowRoom.BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=EVCycleShowRoom.BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel ShowRoomUnitLevel,StreetNumberStreetName ShowRoomStreetNumberStreetName,QuarterCityTownship ShowRoomQuarterCityTownship,State ShowRoomState,Country ShowRoomCountry,PostalCode ShowRoomPostalCode,
			EVCycleShowRoom.IssuedDate,EVCycleShowRoom.EndDate
			FROM EVCycleShowRoom
			INNER JOIN PaThaKa ON EVCycleShowRoom.PaThaKaId = PaThaKa.Id
			INNER JOIN EVCycleShowRoomRegistration ON EVCycleShowRoom.ShowRoomNo = EVCycleShowRoomRegistration.ShowRoomNo
			WHERE ApplyType=@ApplyType AND EVCycleShowRoomRegistration.Status='Approved' AND EVCycleShowRoom.RegistrationType=@FormType
			AND (EVCycleShowRoomRegistration.CreatedDate>=@FromDate AND EVCycleShowRoomRegistration.CreatedDate<=@ToDate)	
		END

		
	END
END

GO

/* dbo.sp_EVShowRoomRegistrationReport */

CREATE PROCEDURE [dbo].[sp_EVShowRoomRegistrationReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@PaymentType nvarchar(50),
	@ApplyType nvarchar(50),
	@RegistrationType nvarchar(200)
AS
BEGIN
	SELECT EVShowRoomRegistration.CreatedDate Date,PaThaKa.CompanyRegistrationNo,EVShowRoomRegistration.ShowRoomNo,PaThaKa.CompanyName,
	UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
	EVShowRoomRegistration.ShowRoomNo,EVShowRoomRegistration.Name,dbo.fn_GetNRCNo(EVShowRoomRegistration.NRCType,EVShowRoomRegistration.NRCPrefixId,EVShowRoomRegistration.NRCPrefixCodeId,EVShowRoomRegistration.NRCNo) NRCNo,
	CASE WHEN EVShowRoomRegistration.BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=EVShowRoomRegistration.BusinessServiceAgencyId) END BusinessServiceAgencyNo,
	ShowRoomUnitLevel,ShowRoomStreetNumberStreetName,ShowRoomQuarterCityTownship,ShowRoomState,ShowRoomCountry,ShowRoomPostalCode,
	ShowRoomUnitLevel2,ShowRoomStreetNumberStreetName2,ShowRoomQuarterCityTownship2,ShowRoomState2,ShowRoomCountry2,ShowRoomPostalCode2,
	ShowRoomUnitLevel3,ShowRoomStreetNumberStreetName3,ShowRoomQuarterCityTownship3,ShowRoomState3,ShowRoomCountry3,ShowRoomPostalCode3,
	ShowRoomUnitLevel4,ShowRoomStreetNumberStreetName4,ShowRoomQuarterCityTownship4,ShowRoomState4,ShowRoomCountry4,ShowRoomPostalCode4,
	ShowRoomUnitLevel5,ShowRoomStreetNumberStreetName5,ShowRoomQuarterCityTownship5,ShowRoomState5,ShowRoomCountry5,ShowRoomPostalCode5,
	PaymentType,VoucherNo,VoucherDate,AccountTransaction.TotalAmount as TotalAmount
	FROM EVShowRoomRegistration
	INNER JOIN PaThaKa ON EVShowRoomRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN AccountTransaction ON EVShowRoomRegistration.Id = AccountTransaction.TransactionId
	WHERE EVShowRoomRegistration.ApplyType=@ApplyType AND EVShowRoomRegistration.Status='Approved' AND IsPayment=1
	AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
	AND (EVShowRoomRegistration.CreatedDate>=@FromDate AND EVShowRoomRegistration.CreatedDate<=@ToDate)
	AND (EVShowRoomRegistration.RegistrationType=@RegistrationType)
END

GO

/* dbo.sp_EVShowRoomReport */


-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_EVShowRoomReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@Date datetime,
	@ApplyType nvarchar(20),
	@FormType nvarchar(50),
	@Type nvarchar(20) --Summary,Detail
AS
BEGIN
	IF(@Type='Summary')
	BEGIN
		SELECT Count(Id) ApplicationCount,'New' ApplyType FROM EVShowRoomRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='New' AND Status='Approved'
		AND RegistrationType=@FormType
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Cancel' ApplyType FROM EVShowRoomRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Cancel' AND Status='Approved'
		AND RegistrationType=@FormType
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Extension' ApplyType FROM EVShowRoomRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Extension' AND Status='Approved'
		AND RegistrationType=@FormType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Valid' ApplyType FROM EVShowRoom
		WHERE (EndDate>@Date)
		AND RegistrationType=@FormType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Invalid' ApplyType FROM EVShowRoom
		WHERE (EndDate<@Date)
		AND RegistrationType=@FormType
	END
	ELSE
	BEGIN

		IF(@ApplyType='Valid')
		BEGIN
			SELECT PaThaKa.CompanyRegistrationNo,EVShowRoom.ShowRoomNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			EVShowRoom.Name,dbo.fn_GetNRCNo(EVShowRoom.NRCType,EVShowRoom.NRCPrefixId,EVShowRoom.NRCPrefixCodeId,EVShowRoom.NRCNo) NRCNo,
			CASE WHEN BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel ShowRoomUnitLevel,StreetNumberStreetName ShowRoomStreetNumberStreetName,QuarterCityTownship ShowRoomQuarterCityTownship,State ShowRoomState,Country ShowRoomCountry,PostalCode ShowRoomPostalCode,
			EVShowRoom.IssuedDate,EVShowRoom.EndDate
			FROM EVShowRoom
			INNER JOIN PaThaKa ON EVShowRoom.PaThaKaId = PaThaKa.Id
			WHERE EVShowRoom.EndDate>@Date
			AND RegistrationType=@FormType
		END
		ELSE IF(@ApplyType='Invalid')
		BEGIN

			SELECT PaThaKa.CompanyRegistrationNo,EVShowRoom.ShowRoomNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			EVShowRoom.Name,dbo.fn_GetNRCNo(EVShowRoom.NRCType,EVShowRoom.NRCPrefixId,EVShowRoom.NRCPrefixCodeId,EVShowRoom.NRCNo) NRCNo,
			CASE WHEN BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel ShowRoomUnitLevel,StreetNumberStreetName ShowRoomStreetNumberStreetName,QuarterCityTownship ShowRoomQuarterCityTownship,State ShowRoomState,Country ShowRoomCountry,PostalCode ShowRoomPostalCode,
			EVShowRoom.IssuedDate,EVShowRoom.EndDate
			FROM EVShowRoom
			INNER JOIN PaThaKa ON EVShowRoom.PaThaKaId = PaThaKa.Id
			WHERE EVShowRoom.EndDate<@Date
			AND RegistrationType=@FormType
		END
		ELSE
		BEGIN
			SELECT PaThaKa.CompanyRegistrationNo,EVShowRoom.ShowRoomNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			EVShowRoom.Name,dbo.fn_GetNRCNo(EVShowRoom.NRCType,EVShowRoom.NRCPrefixId,EVShowRoom.NRCPrefixCodeId,EVShowRoom.NRCNo) NRCNo,
			CASE WHEN EVShowRoom.BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=EVShowRoom.BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel ShowRoomUnitLevel,StreetNumberStreetName ShowRoomStreetNumberStreetName,QuarterCityTownship ShowRoomQuarterCityTownship,State ShowRoomState,Country ShowRoomCountry,PostalCode ShowRoomPostalCode,
			EVShowRoom.IssuedDate,EVShowRoom.EndDate
			FROM EVShowRoom
			INNER JOIN PaThaKa ON EVShowRoom.PaThaKaId = PaThaKa.Id
			INNER JOIN EVShowRoomRegistration ON EVShowRoom.ShowRoomNo = EVShowRoomRegistration.ShowRoomNo
			WHERE ApplyType=@ApplyType AND EVShowRoomRegistration.Status='Approved' AND EVShowRoom.RegistrationType=@FormType
			AND (EVShowRoomRegistration.CreatedDate>=@FromDate AND EVShowRoomRegistration.CreatedDate<=@ToDate)	
		END

		
	END
END

GO

/* dbo.sp_ExportLicenceDetailReport */
-- =============================================  
-- Author:  Name  
-- Create date:   
-- Description:   
-- =============================================  
 --exec sp_ExportLicenceDetailReport 'Oversea','2021-01-07 00:00:00','2021-01-07 23:59:59',0,0,0,0,0,'',0
CREATE PROCEDURE [dbo].[sp_ExportLicenceDetailReport]   
 -- Add the parameters for the stored procedure here  
 @Type nvarchar(20),  -- Oversea,Border
 @FromDate datetime,
 @ToDate datetime,
 @PaThaKaTypeId int,  
 @ExportImportSectionId int,  
 @ExportImportMethodId int,  
 @ExportImportIncotermId int,
 @BuyerCountryId int,
 @CompanyRegistrationNo nvarchar(50),
 @SakhanId int
AS  
BEGIN  
   
 IF(@Type='Oversea')  
 BEGIN  
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  ExportImportSectionId,ExportImportMethodId,ExportImportIncotermId,BuyerCountryId, 
  section.Code SectionCode,section.Name SectionName,ExportLicenceNo LicenceNo,ExportLicence.IssuedDate LicenceDate,  
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  BuyerName,BuyerAddress,buyerCountry.Name BuyerCountry,  
  (  
   SELECT ','+portofDischarge.Name  
   FROM PortOfDischarge portofDischarge  
   WHERE ','+ExportLicence.PortofExportId+',' LIKE '%,'+CAST(portofDischarge.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as PortofExport,PortofDischarge,  
  LastDate,method.Name MethodName,
  (  
   SELECT ','+countries.Name  
   FROM Countries countries  
   WHERE ','+ExportLicence.DestinationCountryId+',' LIKE '%,'+CAST(countries.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as DestinationCountry,
  consignedCountry.Name ConsignedCountry,countryofOrigin.Name CountryofOrigin,  
  HSCode.Code HSCode,HSCode.Description+' '+ExportLicenceItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  ExportLicence.Remark Conditions,ExportLicence.ApproveDate
  FROM ExportLicence  
  INNER JOIN PaThaKa ON PaThaKa.Id = ExportLicence.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN ExportLicenceItem ON ExportLicence.Id = ExportLicenceItem.ExportLicenceId  
  INNER JOIN Unit unit ON ExportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = ExportLicence.ExportImportSectionId  
  INNER JOIN Countries buyerCountry ON buyerCountry.Id  = ExportLicence.BuyerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = ExportLicence.ExportImportMethodId  
  INNER JOIN Countries consignedCountry ON consignedCountry.Id  = ExportLicence.ConsignedCountryId  
  INNER JOIN Countries countryofOrigin ON countryofOrigin.Id  = ExportLicence.CountryofOriginId  
  --INNER JOIN Countries destinationCountry ON DestinationCountry.Id  = ExportLicence.DestinationCountryId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = ExportLicence.ExportImportIncotermId  
  WHERE ApplyType='New' 
  AND ExportLicence.Status='Approved'  
  AND (ExportLicence.CreatedDate>=@FromDate AND ExportLicence.CreatedDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND ExportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then ExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND ExportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then ExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND ExportLicence.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then ExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
  ORDER BY ExportLicence.LicenceDate
 END  
 ELSE IF(@Type='Border')  
 BEGIN  
    SELECT * FROM
  (SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,ExportImportSectionId,ExportImportMethodId,ExportImportIncotermId,BuyerCountryId, 
  section.Code SectionCode,section.Name SectionName,ExportLicenceNo LicenceNo,BorderExportLicence.IssuedDate LicenceDate,  
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  BuyerName,BuyerAddress,buyerCountry.Name BuyerCountry,  
  (  
   SELECT ','+portofDischarge.Name  
   FROM PortOfDischarge portofDischarge  
   WHERE ','+BorderExportLicence.PortofExportId+',' LIKE '%,'+CAST(portofDischarge.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as PortofExport,PortofDischarge,  
  LastDate,method.Name MethodName,
  (  
   SELECT ','+countries.Name  
   FROM Countries countries  
   WHERE ','+BorderExportLicence.DestinationCountryId+',' LIKE '%,'+CAST(countries.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as DestinationCountry,
  consignedCountry.Name ConsignedCountry,countryofOrigin.Name CountryofOrigin,  
  HSCode.Code HSCode,HSCode.Description+' '+ISNULL(BorderExportLicenceItem.Description,'') HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency ,
  BorderExportLicence.Remark Conditions,BorderExportLicence.ApproveDate
  FROM BorderExportLicence  
  INNER JOIN PaThaKa ON PaThaKa.Id = BorderExportLicence.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId  
  INNER JOIN Unit unit ON BorderExportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = BorderExportLicence.ExportImportSectionId  
  INNER JOIN Countries buyerCountry ON buyerCountry.Id  = BorderExportLicence.BuyerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = BorderExportLicence.ExportImportMethodId  
  INNER JOIN Countries consignedCountry ON consignedCountry.Id  = BorderExportLicence.ConsignedCountryId  
  INNER JOIN Countries countryofOrigin ON countryofOrigin.Id  = BorderExportLicence.CountryofOriginId  
  --INNER JOIN Countries destinationCountry ON DestinationCountry.Id  = BorderExportLicence.DestinationCountryId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = BorderExportLicence.ExportImportIncotermId  
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderExportLicence.SakhanId
  WHERE ApplyType='New' 
  AND BorderExportLicence.Status='Approved'  AND BorderExportLicence.CardType='Pa Tha Ka'
  AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND BorderExportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then BorderExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND BorderExportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then BorderExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND BorderExportLicence.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then BorderExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
  AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
  UNION ALL
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,ExportImportSectionId,ExportImportMethodId,ExportImportIncotermId,BuyerCountryId, 
  section.Code SectionCode,section.Name SectionName,ExportLicenceNo LicenceNo,BorderExportLicence.IssuedDate LicenceDate,  
  IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  BuyerName,BuyerAddress,buyerCountry.Name BuyerCountry,  
  (  
   SELECT ','+portofDischarge.Name  
   FROM PortOfDischarge portofDischarge  
   WHERE ','+BorderExportLicence.PortofExportId+',' LIKE '%,'+CAST(portofDischarge.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as PortofExport,PortofDischarge,  
  LastDate,method.Name MethodName,
  (  
   SELECT ','+countries.Name  
   FROM Countries countries  
   WHERE ','+BorderExportLicence.DestinationCountryId+',' LIKE '%,'+CAST(countries.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as DestinationCountry,
  consignedCountry.Name ConsignedCountry,countryofOrigin.Name CountryofOrigin,  
  HSCode.Code HSCode,HSCode.Description+' '+ISNULL(BorderExportLicenceItem.Description,'') HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  BorderExportLicence.Remark Conditions,BorderExportLicence.ApproveDate
  FROM BorderExportLicence  
  INNER JOIN IndividualTrading ON IndividualTrading.Id = BorderExportLicence.IndividualTradingId
  INNER JOIN PaThaKaType paThaKaType ON IndividualTrading.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId  
  INNER JOIN Unit unit ON BorderExportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = BorderExportLicence.ExportImportSectionId  
  INNER JOIN Countries buyerCountry ON buyerCountry.Id  = BorderExportLicence.BuyerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = BorderExportLicence.ExportImportMethodId  
  INNER JOIN Countries consignedCountry ON consignedCountry.Id  = BorderExportLicence.ConsignedCountryId  
  INNER JOIN Countries countryofOrigin ON countryofOrigin.Id  = BorderExportLicence.CountryofOriginId  
  --INNER JOIN Countries destinationCountry ON DestinationCountry.Id  = BorderExportLicence.DestinationCountryId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = BorderExportLicence.ExportImportIncotermId  
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderExportLicence.SakhanId
  WHERE ApplyType='New' 
  AND BorderExportLicence.Status='Approved'  AND BorderExportLicence.CardType='Individual Trading'
  AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
  AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND BorderExportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then BorderExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND BorderExportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then BorderExportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND BorderExportLicence.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then BorderExportLicence.BuyerCountryId ELSE @BuyerCountryId END)
  AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
  )tmp

  ORDER BY tmp.LicenceDate
 END  
  
   
END

GO

/* dbo.sp_ExportPermitDetailReport */
-- =============================================  
-- Author:  Name  
-- Create date:   
-- Description:   
-- =============================================  
 --exec [sp_ExportPermitDetailReport] 'Oversea','2020-07-01 00:00:00','2020-07-13 23:59:59',0,0,0,0,0,''
CREATE PROCEDURE [dbo].[sp_ExportPermitDetailReport]   
 -- Add the parameters for the stored procedure here  
 @Type nvarchar(20),  -- Oversea,Border
 @FromDate datetime,
 @ToDate datetime,
 @PaThaKaTypeId int,  
 @ExportImportSectionId int,  
 @BuyerCountryId int,
 @CompanyRegistrationNo nvarchar(50),
 @SakhanId int
AS  
BEGIN  
   
 IF(@Type='Oversea')  
 BEGIN  
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  ExportImportSectionId,BuyerCountryId, 
  section.Code SectionCode,section.Name SectionName,ExportPermitNo LicenceNo,ExportPermit.IssuedDate LicenceDate,  
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  ConsigneeName,ConsigneeAddress,buyerCountry.Name BuyerCountry,  
  (  
   SELECT ','+portofDischarge.Name  
   FROM PortOfDischarge portofDischarge  
   WHERE ','+ExportPermit.PortofExportId+',' LIKE '%,'+CAST(portofDischarge.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as PortofExport,PortofDischarge,
  (  
   SELECT ','+countries.Name  
   FROM Countries countries  
   WHERE ','+ExportPermit.DestinationCountryId+',' LIKE '%,'+CAST(countries.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as DestinationCountry,
  LastDate,consignedCountry.Name ConsignedCountry,
  --countryofOrigin.Name CountryofOrigin,
  (  
   SELECT ','+countries.Name  
   FROM Countries countries  
   WHERE ','+ExportPermit.CountryofOriginId+',' LIKE '%,'+CAST(countries.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,
  HSCode.Code HSCode,HSCode.Description+' '+ExportPermitItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  dbo.fn_GetNRCNo(ExportPermit.NRCType,ExportPermit.NRCPrefixId,ExportPermit.NRCPrefixCodeId,ExportPermit.NRCNo) NRCNo,
  PermitType,ExportPermit.Remark Conditions,ExportPermit.ApproveDate
  FROM ExportPermit  
  INNER JOIN PaThaKa ON PaThaKa.Id = ExportPermit.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN ExportPermitItem ON ExportPermit.Id = ExportPermitItem.ExportPermitId  
  INNER JOIN Unit unit ON ExportPermitItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = ExportPermit.ExportImportSectionId  
  INNER JOIN Countries buyerCountry ON buyerCountry.Id  = ExportPermit.BuyerCountryId  
  INNER JOIN Countries consignedCountry ON consignedCountry.Id  = ExportPermit.ConsignedCountryId  
  --INNER JOIN Countries countryofOrigin ON countryofOrigin.Id  = ExportPermit.CountryofOriginId  
  WHERE ApplyType='New' 
  AND ExportPermit.Status='Approved'  
  AND (ExportPermit.CreatedDate>=@FromDate AND ExportPermit.CreatedDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND ExportPermit.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then ExportPermit.BuyerCountryId ELSE @BuyerCountryId END)
 END  
 ELSE IF(@Type='Border')  
 BEGIN  
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,ExportImportSectionId,BuyerCountryId, 
  section.Code SectionCode,section.Name SectionName,ExportPermitNo LicenceNo,BorderExportPermit.IssuedDate LicenceDate,  
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  ConsigneeName,ConsigneeAddress,buyerCountry.Name BuyerCountry,  
  (  
   SELECT ','+portofDischarge.Name  
   FROM PortOfDischarge portofDischarge  
   WHERE ','+BorderExportPermit.PortofExportId+',' LIKE '%,'+CAST(portofDischarge.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as PortofExport,PortofDischarge,
  (  
   SELECT ','+countries.Name  
   FROM Countries countries  
   WHERE ','+BorderExportPermit.DestinationCountryId+',' LIKE '%,'+CAST(countries.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as DestinationCountry,
  LastDate,consignedCountry.Name ConsignedCountry,
  --countryofOrigin.Name CountryofOrigin,  
  (  
   SELECT ','+countries.Name  
   FROM Countries countries  
   WHERE ','+BorderExportPermit.CountryofOriginId+',' LIKE '%,'+CAST(countries.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,
  HSCode.Code HSCode,HSCode.Description+' '+BorderExportPermitItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  dbo.fn_GetNRCNo(BorderExportPermit.NRCType,BorderExportPermit.NRCPrefixId,BorderExportPermit.NRCPrefixCodeId,BorderExportPermit.NRCNo) NRCNo,
  PermitType,BorderExportPermit.Remark Conditions,BorderExportPermit.ApproveDate
  FROM BorderExportPermit  
  INNER JOIN PaThaKa ON PaThaKa.Id = BorderExportPermit.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderExportPermitItem ON BorderExportPermit.Id = BorderExportPermitItem.BorderExportPermitId  
  INNER JOIN Unit unit ON BorderExportPermitItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON BorderExportPermitItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = BorderExportPermit.ExportImportSectionId  
  INNER JOIN Countries buyerCountry ON buyerCountry.Id  = BorderExportPermit.BuyerCountryId  
  INNER JOIN Countries consignedCountry ON consignedCountry.Id  = BorderExportPermit.ConsignedCountryId  
  --INNER JOIN Countries countryofOrigin ON countryofOrigin.Id  = BorderExportPermit.CountryofOriginId  
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderExportPermit.SakhanId
  WHERE ApplyType='New' 
  AND BorderExportPermit.Status='Approved'  
  AND (BorderExportPermit.CreatedDate>=@FromDate AND BorderExportPermit.CreatedDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND BorderExportPermit.BuyerCountryId=(CASE WHEN @BuyerCountryId=0 then BorderExportPermit.BuyerCountryId ELSE @BuyerCountryId END)
  AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END)
 END  
  
   
END

GO

/* dbo.sp_ExtensionReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_ExportLicenceAmendReport ''
CREATE PROCEDURE [dbo].[sp_ExtensionReport] 
	-- Add the parameters for the stored procedure here
	@FormType nvarchar(50),
	@FromDate datetime,
	@ToDate datetime,
	@ExportImportSectionId int,
	@CompanyRegistrationNo nvarchar(10),
	@SakhanId int
AS
BEGIN
	IF(@FormType='Export Licence')
	BEGIN
		SELECT ExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,ExportLicence.LastDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(SUM(ExportLicenceItem.Amount),0) FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Amount
		FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType='Extension' AND ExportLicence.Status='Approved'
		AND (ExportLicence.CreatedDate>=@FromDate AND ExportLicence.CreatedDate<=@ToDate)
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Import Licence')
	BEGIN
		SELECT ImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,ImportLicence.LastDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Currency,
		(SELECT top 1 ISNULL(SUM(ImportLicenceItem.Amount),0) FROM ImportLicenceItem
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Amount
		FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType='Extension' AND ImportLicence.Status='Approved'
		AND (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Export Permit')
	BEGIN
		SELECT ExportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,CONVERT(varchar,ExportPermit.LastDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ExportPermitItem
		INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Currency,
		(SELECT top 1 ISNULL(SUM(ExportPermitItem.Amount),0) FROM ExportPermitItem
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Amount
		FROM ExportPermit
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType='Extension' AND ExportPermit.Status='Approved'
		AND (ExportPermit.CreatedDate>=@FromDate AND ExportPermit.CreatedDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Import Permit')
	BEGIN
		SELECT ImportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,CONVERT(varchar,ImportPermit.LastDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ImportPermitItem
		INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Currency,
		(SELECT top 1 ISNULL(SUM(ImportPermitItem.Amount),0) FROM ImportPermitItem
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Amount
		FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType='Extension' AND ImportPermit.Status='Approved'
		AND (ImportPermit.CreatedDate>=@FromDate AND ImportPermit.CreatedDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Border Export Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(SUM(BorderExportLicenceItem.Amount),0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportLicence
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Extension' AND BorderExportLicence.Status='Approved' AND CardType='Pa Tha Ka'
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
		IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(SUM(BorderExportLicenceItem.Amount),0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Extension' AND BorderExportLicence.Status='Approved' AND CardType='Individual Trading'
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		)tmp
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Import Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT top 1 ISNULL(SUM(BorderImportLicenceItem.Amount),0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Extension' AND BorderImportLicence.Status='Approved' AND CardType='Pa Tha Ka'
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
		IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT top 1 ISNULL(SUM(BorderImportLicenceItem.Amount),0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='Extension' AND BorderImportLicence.Status='Approved' AND CardType='Individual Trading'
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		)tmp
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Export Permit')
	BEGIN
		SELECT BorderExportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,CONVERT(varchar,BorderExportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportPermitItem
		INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Currency,
		(SELECT top 1 ISNULL(SUM(BorderExportPermitItem.Amount),0) FROM BorderExportPermitItem
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportPermit
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		WHERE ApplyType='Extension' AND BorderExportPermit.Status='Approved'
		AND (BorderExportPermit.CreatedDate>=@FromDate AND BorderExportPermit.CreatedDate<=@ToDate)
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END)
	END
	ELSE IF(@FormType='Border Import Permit')
	BEGIN
		SELECT BorderImportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,CONVERT(varchar,BorderImportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportPermitItem
		INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Currency,
		(SELECT top 1 ISNULL(SUM(BorderImportPermitItem.Amount),0) FROM BorderImportPermitItem
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType='Extension' AND BorderImportPermit.Status='Approved'
		AND (BorderImportPermit.CreatedDate>=@FromDate AND BorderImportPermit.CreatedDate<=@ToDate)
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
	END

	
END

GO

/* dbo.sp_GetChekApproveNotiList */
-- =============================================
-- Author:      Name
-- Create Date: 
-- Description: 
-- =============================================
--exec sp_GetChekApproveNotiList 3,'','Check User'
CREATE PROCEDURE [dbo].[sp_GetChekApproveNotiList]
(
    -- Add the parameters for the stored procedure here
   @UserId int,
   @FormType nvarchar(200),
   @UserType nvarchar(200) -- Check User,Approve User
)
AS
BEGIN
    IF(@UserType='Check User')
	BEGIN
		SELECT * FROM
		(SELECT 'Border Export Licence' FormType FROM BorderExportLicence
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Border Export Permit' FormType FROM BorderExportPermit
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Border Import Licence' FormType FROM BorderImportLicence
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Border Import Permit' FormType FROM BorderImportPermit
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Export Licence' FormType FROM ExportLicence
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Export Permit' FormType FROM ExportPermit
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Import Licence' FormType FROM ImportLicence
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Import Permit' FormType FROM ImportPermit
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Pa Tha Ka' FormType FROM PaThaKaRegistration
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Pa Tha Ka' FormType FROM PaThaKaBind
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Business Service Agency' FormType FROM BusinessServiceAgencyRegistration
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Duty Free Shop' FormType FROM DutyFreeShopRegistration
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Individual Trading' FormType FROM IndividualTradingRegistration
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Re-Export' FormType FROM ReExportRegistration
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Sale Center' FormType FROM SaleCenterRegistration
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Show Room' FormType FROM ShowRoomRegistration
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending'
		UNION ALL
		SELECT 'Whole Sale' FormType FROM WholeSaleRetailRegistration
		WHERE CheckUserId=@UserId AND IsCheck=0 AND RegistrationType='Whole Sale' AND Status='Pending'
		UNION ALL
		SELECT 'Retail' FormType FROM WholeSaleRetailRegistration
		WHERE CheckUserId=@UserId AND IsCheck=0 AND RegistrationType='Retail'
		UNION ALL
		SELECT 'Whole Sale and Retail' FormType FROM WholeSaleRetailRegistration
		WHERE CheckUserId=@UserId AND IsCheck=0 AND RegistrationType='Whole Sale and Retail' AND Status='Pending'
		UNION ALL
		SELECT 'Wine Importation' FormType FROM WineImportationRegistration
		WHERE CheckUserId=@UserId AND IsCheck=0 AND Status='Pending')tmp
		WHERE tmp.FormType=(CASE WHEN @FormType='' THEN tmp.FormType ELSE @FormType END)
	END
	ELSE
	BEGIN
		SELECT * FROM
		(SELECT 'Border Export Licence' FormType FROM BorderExportLicence
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Border Export Permit' FormType FROM BorderExportPermit
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Border Import Licence' FormType FROM BorderImportLicence
		WHERE ApproveUserId=@UserId AND IsApprove=0
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Border Import Permit' FormType FROM BorderImportPermit
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Export Licence' FormType FROM ExportLicence
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Export Permit' FormType FROM ExportPermit
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Import Licence' FormType FROM ImportLicence
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Import Permit' FormType FROM ImportPermit
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Pa Tha Ka' FormType FROM PaThaKaRegistration
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Pa Tha Ka' FormType FROM PaThaKaBind
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		UNION ALL
		SELECT 'Business Service Agency' FormType FROM BusinessServiceAgencyRegistration
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Duty Free Shop' FormType FROM DutyFreeShopRegistration
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Re-Export' FormType FROM ReExportRegistration
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Sale Center' FormType FROM SaleCenterRegistration
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Show Room' FormType FROM ShowRoomRegistration
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Whole Sale' FormType FROM WholeSaleRetailRegistration
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND RegistrationType='Whole Sale' AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Retail' FormType FROM WholeSaleRetailRegistration
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND RegistrationType='Retail' AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Whole Sale and Retail' FormType FROM WholeSaleRetailRegistration
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND RegistrationType='Whole Sale and Retail' AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved')
		UNION ALL
		SELECT 'Wine Importation' FormType FROM WineImportationRegistration
		WHERE ApproveUserId=@UserId AND IsApprove=0 AND Status='Pending'
		AND (EICCStatus IS NULL OR EICCStatus='Reject' OR EICCStatus='Approved'))tmp
		WHERE tmp.FormType=(CASE WHEN @FormType='' THEN tmp.FormType ELSE @FormType END)
	END
END

GO

/* dbo.sp_HSCodeReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_HSCodeReport '2020-06-01 00:00:00','2020-07-14 23:59:59','Export Licence','Start',''
CREATE PROCEDURE [dbo].[sp_HSCodeReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@FormType nvarchar(50),
	@FilterType nvarchar(20),  --Start,End,
	@HSCode nvarchar(50),
	@SakhanId int
AS
BEGIN

	IF(@FormType='Export Licence')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			ExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM ExportLicence
			INNER JOIN ExportLicenceItem ON ExportLicence.Id = ExportLicenceItem.ExportLicenceId
			INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND ExportLicence.Status='Approved'
			AND (ExportLicence.LicenceDate>=@FromDate AND ExportLicence.LicenceDate<=@ToDate)
			ORDER BY HSCode.Id
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ExportLicence
				INNER JOIN ExportLicenceItem ON ExportLicence.Id = ExportLicenceItem.ExportLicenceId
				INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ExportLicence.Status='Approved'
				AND (ExportLicence.LicenceDate>=@FromDate AND ExportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				ORDER BY HSCode.Id
			END
			ELSE
			BEGIN
				SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ExportLicence
				INNER JOIN ExportLicenceItem ON ExportLicence.Id = ExportLicenceItem.ExportLicenceId
				INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ExportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ExportLicence.Status='Approved'
				AND (ExportLicence.LicenceDate>=@FromDate AND ExportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				ORDER BY HSCode.Id
			END
		END
	END
	ELSE IF(@FormType='Import Licence')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			ImportLicence.ImportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM ImportLicence
			INNER JOIN ImportLicenceItem ON ImportLicence.Id = ImportLicenceItem.ImportLicenceId
			INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND ImportLicence.Status='Approved'
			AND (ImportLicence.LicenceDate>=@FromDate AND ImportLicence.LicenceDate<=@ToDate)
			ORDER BY HSCode.Id
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ImportLicence.ImportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ImportLicence
				INNER JOIN ImportLicenceItem ON ImportLicence.Id = ImportLicenceItem.ImportLicenceId
				INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ImportLicence.Status='Approved'
				AND (ImportLicence.LicenceDate>=@FromDate AND ImportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				ORDER BY HSCode.Id
			END
			ELSE
			BEGIN
				SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ImportLicence.ImportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ImportLicence
				INNER JOIN ImportLicenceItem ON ImportLicence.Id = ImportLicenceItem.ImportLicenceId
				INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ImportLicence.Status='Approved'
				AND (ImportLicence.LicenceDate>=@FromDate AND ImportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				ORDER BY HSCode.Id
			END
		END
	END
	ELSE IF(@FormType='Export Permit')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT section.Code sectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			ExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM ExportPermit
			INNER JOIN ExportPermitItem ON ExportPermit.Id = ExportPermitItem.ExportPermitId
			INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND ExportPermit.Status='Approved'
			AND (ExportPermit.LicenceDate>=@FromDate AND ExportPermit.LicenceDate<=@ToDate)
			ORDER BY HSCode.Id
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ExportPermit
				INNER JOIN ExportPermitItem ON ExportPermit.Id = ExportPermitItem.ExportPermitId
				INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ExportPermit.Status='Approved'
				AND (ExportPermit.LicenceDate>=@FromDate AND ExportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				ORDER BY HSCode.Id
			END
			ELSE
			BEGIN
				SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ExportPermit
				INNER JOIN ExportPermitItem ON ExportPermit.Id = ExportPermitItem.ExportPermitId
				INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ExportPermit.Status='Approved'
				AND (ExportPermit.LicenceDate>=@FromDate AND ExportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				ORDER BY HSCode.Id
			END
		END
	END
	ELSE IF(@FormType='Import Permit')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			ImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM ImportPermit
			INNER JOIN ImportPermitItem ON ImportPermit.Id = ImportPermitItem.ImportPermitId
			INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND ImportPermit.Status='Approved'
			AND (ImportPermit.LicenceDate>=@FromDate AND ImportPermit.LicenceDate<=@ToDate)
			ORDER BY HSCode.Id
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ImportPermit
				INNER JOIN ImportPermitItem ON ImportPermit.Id = ImportPermitItem.ImportPermitId
				INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ImportPermit.Status='Approved'
				AND (ImportPermit.LicenceDate>=@FromDate AND ImportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				ORDER BY HSCode.Id
			END
			ELSE
			BEGIN
				SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				ImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM ImportPermit
				INNER JOIN ImportPermitItem ON ImportPermit.Id = ImportPermitItem.ImportPermitId
				INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND ImportPermit.Status='Approved'
				AND (ImportPermit.LicenceDate>=@FromDate AND ImportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				ORDER BY HSCode.Id
			END
		END
	END
	ELSE IF(@FormType='Border Export Licence')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT * FROM
			(SELECT BorderExportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			BorderExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM BorderExportLicence
			INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
			INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Pa Tha Ka'
			AND (BorderExportLicence.LicenceDate>=@FromDate AND BorderExportLicence.LicenceDate<=@ToDate)
			AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
			UNION ALL
			SELECT BorderExportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			BorderExportLicence.ExportLicenceNo LicenceNo,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName
			FROM BorderExportLicence
			INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
			INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
			INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Individual Trading'
			AND (BorderExportLicence.LicenceDate>=@FromDate AND BorderExportLicence.LicenceDate<=@ToDate)
			AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
			)tmp
			ORDER BY tmp.HSCodeId
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT * FROM
				(SELECT BorderExportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderExportLicence
				INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
				INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Pa Tha Ka'
				AND (BorderExportLicence.LicenceDate>=@FromDate AND BorderExportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
				UNION ALL
				SELECT BorderExportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderExportLicence.ExportLicenceNo LicenceNo,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName
				FROM BorderExportLicence
				INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
				INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
				INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Individual Trading'
				AND (BorderExportLicence.LicenceDate>=@FromDate AND BorderExportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END))tmp
				ORDER BY tmp.HSCodeId
			END
			ELSE
			BEGIN
				SELECT * FROM
				(SELECT BorderExportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderExportLicence.ExportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderExportLicence
				INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
				INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Pa Tha Ka'
				AND (BorderExportLicence.LicenceDate>=@FromDate AND BorderExportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
				UNION ALL
				SELECT BorderExportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderExportLicence.ExportLicenceNo LicenceNo,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName
				FROM BorderExportLicence
				INNER JOIN BorderExportLicenceItem ON BorderExportLicence.Id = BorderExportLicenceItem.BorderExportLicenceId
				INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
				INNER JOIN HSCode ON BorderExportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Individual Trading'
				AND (BorderExportLicence.LicenceDate>=@FromDate AND BorderExportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportLicence.SakhanId ELSE @SakhanId END)
				)tmp
				ORDER BY tmp.HSCodeId
			END
		END
	END
	ELSE IF(@FormType='Border Import Licence')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT * FROM
			(SELECT BorderImportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			BorderImportLicence.ImportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM BorderImportLicence
			INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId
			INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Pa Tha Ka'
			AND (BorderImportLicence.LicenceDate>=@FromDate AND BorderImportLicence.LicenceDate<=@ToDate)
			AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
			UNION ALL
			SELECT BorderImportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			BorderImportLicence.ImportLicenceNo LicenceNo,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName
			FROM BorderImportLicence
			INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId
			INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
			INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Individual Trading'
			AND (BorderImportLicence.LicenceDate>=@FromDate AND BorderImportLicence.LicenceDate<=@ToDate)
			AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END))tmp
			ORDER BY tmp.HSCodeId
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT * FROM
				(SELECT BorderImportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderImportLicence.ImportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderImportLicence
				INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId
				INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Pa Tha Ka'
				AND (BorderImportLicence.LicenceDate>=@FromDate AND BorderImportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
				UNION ALL
				SELECT BorderImportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderImportLicence.ImportLicenceNo LicenceNo,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName
				FROM BorderImportLicence
				INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId
				INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
				INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Individual Trading'
				AND (BorderImportLicence.LicenceDate>=@FromDate AND BorderImportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
				)tmp
				ORDER BY tmp.HSCodeId
			END
			ELSE
			BEGIN
				SELECT * FROM
				(SELECT BorderImportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderImportLicence.ImportLicenceNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderImportLicence
				INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId
				INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Pa Tha Ka'
				AND (BorderImportLicence.LicenceDate>=@FromDate AND BorderImportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
				UNION ALL
				SELECT BorderImportLicence.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderImportLicence.ImportLicenceNo LicenceNo,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName
				FROM BorderImportLicence
				INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId
				INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
				INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Individual Trading'
				AND (BorderImportLicence.LicenceDate>=@FromDate AND BorderImportLicence.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportLicence.SakhanId ELSE @SakhanId END)
				)tmp
				ORDER BY tmp.HSCodeId
			END
		END
	END
	ELSE IF(@FormType='Border Export Permit')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT BorderExportPermit.SakhanId SakhanId,section.Code sectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			BorderExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM BorderExportPermit
			INNER JOIN BorderExportPermitItem ON BorderExportPermit.Id = BorderExportPermitItem.BorderExportPermitId
			INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON BorderExportPermitItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND BorderExportPermit.Status='Approved'
			AND (BorderExportPermit.LicenceDate>=@FromDate AND BorderExportPermit.LicenceDate<=@ToDate)
			AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportPermit.SakhanId ELSE @SakhanId END)
			ORDER BY HSCode.Id
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT BorderExportPermit.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderExportPermit
				INNER JOIN BorderExportPermitItem ON BorderExportPermit.Id = BorderExportPermitItem.BorderExportPermitId
				INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderExportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderExportPermit.Status='Approved'
				AND (BorderExportPermit.LicenceDate>=@FromDate AND BorderExportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportPermit.SakhanId ELSE @SakhanId END)
				ORDER BY HSCode.Id
			END
			ELSE
			BEGIN
				SELECT BorderExportPermit.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderExportPermit
				INNER JOIN BorderExportPermitItem ON BorderExportPermit.Id = BorderExportPermitItem.BorderExportPermitId
				INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderExportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderExportPermit.Status='Approved'
				AND (BorderExportPermit.LicenceDate>=@FromDate AND BorderExportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderExportPermit.SakhanId ELSE @SakhanId END)
				ORDER BY HSCode.Id
			END
		END
	END
	ELSE IF(@FormType='Border Import Permit')
	BEGIN
		IF(@HSCode='')
		BEGIN
			SELECT BorderImportPermit.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
			BorderImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
			FROM BorderImportPermit
			INNER JOIN BorderImportPermitItem ON BorderImportPermit.Id = BorderImportPermitItem.BorderImportPermitId
			INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
			INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id
			INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
			INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
			WHERE ApplyType='New' AND BorderImportPermit.Status='Approved'
			AND (BorderImportPermit.LicenceDate>=@FromDate AND BorderImportPermit.LicenceDate<=@ToDate)
			AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportPermit.SakhanId ELSE @SakhanId END)
			ORDER BY HSCode.Id
		END
		ELSE
		BEGIN
			IF(@FilterType='Start')
			BEGIN
				SELECT BorderImportPermit.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderImportPermit
				INNER JOIN BorderImportPermitItem ON BorderImportPermit.Id = BorderImportPermitItem.BorderImportPermitId
				INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderImportPermit.Status='Approved'
				AND (BorderImportPermit.LicenceDate>=@FromDate AND BorderImportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE @HSCode+'%'
				AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportPermit.SakhanId ELSE @SakhanId END)
				ORDER BY HSCode.Id
			END
			ELSE
			BEGIN
				SELECT BorderImportPermit.SakhanId SakhanId,section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
				BorderImportPermit.ImportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
				FROM BorderImportPermit
				INNER JOIN BorderImportPermitItem ON BorderImportPermit.Id = BorderImportPermitItem.BorderImportPermitId
				INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
				INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id
				INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
				INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
				WHERE ApplyType='New' AND BorderImportPermit.Status='Approved'
				AND (BorderImportPermit.LicenceDate>=@FromDate AND BorderImportPermit.LicenceDate<=@ToDate)
				AND HSCode.Code LIKE '%'+@HSCode
				AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 THEN BorderImportPermit.SakhanId ELSE @SakhanId END)
				ORDER BY HSCode.Id
			END
		END
	END
END

GO

/* dbo.sp_HSCodeSearch */
CREATE PROCEDURE [dbo].[sp_HSCodeSearch]
(
    -- Add the parameters for the stored procedure here
    @Type nvarchar(20), --Export,Import
	@ExportImportSectionId int,
	@LicenceType nvarchar(50)
)
AS
BEGIN
   Declare @HSCodeType nvarchar(50)
   Declare @ExportImportSectionCode nvarchar(10)

   SET @HSCodeType=@LicenceType

   

   IF(@ExportImportSectionId>0)
   BEGIN
	SET @ExportImportSectionCode=(SELECT TOP 1 Code FROM ExportImportSection WHERE Id=@ExportImportSectionId)
   END

   IF(@Type like 'Export%')
   BEGIN
		SELECT HSCode.Id as Id,HSCode.Code as Code,HSCode.Description as Description,
		'' as GroupDescription,
		ExportLicenceType LicenceType,ExportSection Section,Unit.Code as UnitCode FROM HSCode
		left Join Unit on Unit.id=HSCode.UnitId
		WHERE HSCode.Year='2022'
		AND ExportLicenceType=(CASE WHEN @HSCodeType='' THEN ExportLicenceType ELSE @HSCodeType END)
		AND ExportSection LIKE (CASE WHEN @ExportImportSectionId=0 THEN ExportSection ELSE '%''+@ExportImportSectionCode+''%' END)  
		AND ExportProhibited='No'
		AND ExportRestricted='No'
   END
   ELSE
  BEGIN
		SELECT HSCode.Id as Id,HSCode.Code as Code,HSCode.Description as Description,
        GroupCode.description as GroupDescription,
        ImportLicenceType LicenceType --,ImportSection Section
		,CASE WHEN @ExportImportSectionId=0 THEN ImportSection ELSE @ExportImportSectionCode END as Section
		,Unit.Code as UnitCode 
        FROM HSCode
        left Join Unit on Unit.id=HSCode.UnitId
        left join GroupCode on HSCode.ImportGroupCode=GroupCode.groupCode
		WHERE HSCode.Year='2022' 
		AND ImportLicenceType=(CASE WHEN @HSCodeType='' THEN ImportLicenceType ELSE @HSCodeType END)
		AND ImportSection LIKE (CASE WHEN @ExportImportSectionId=0 THEN ImportSection ELSE '%'+@ExportImportSectionCode+'%' END)
		AND ImportProhibited='No'
		AND ImportRestricted='No'
   END
END

GO

/* dbo.sp_ImportLicenceDaily_Detail_Report */
-- =============================================  
-- Author:  Name  
-- Create date:   
-- Description:   
-- =============================================  
 --exec sp_ReportExportLicenceDetail 'Oversea','2020-07-01 00:00:00','2020-07-13 23:59:59',0,0,0,0,0,''
CREATE PROCEDURE [dbo].[sp_ImportLicenceDaily_Detail_Report]   
 -- Add the parameters for the stored procedure here  
 @Type nvarchar(20),  -- Oversea,Border
 @FromDate datetime,
 @ToDate datetime,
 @PaThaKaTypeId int,  
 @ExportImportSectionId int,  
 @ExportImportMethodId int,  
 @ExportImportIncotermId int,
 @SellerCountryId int,
 @CompanyRegistrationNo nvarchar(50),
 @SakhanId int
AS  
BEGIN  
   
 IF(@Type='Oversea')  
 BEGIN  
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  ExportImportSectionId,ExportImportMethodId,ExportImportIncotermId,SellerCountryId, 
  section.Code SectionCode,section.Name SectionName,ImportLicenceNo LicenceNo,ImportLicence.IssuedDate LicenceDate,  
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  SellerName,SellerAddress,sellerCountry.Name SellerCountry,  
  PortofDischarge,LastDate,method.Name MethodName,  
  HSCode.Code HSCode,ImportLicenceItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  ImportLicence.Remark Conditions
  FROM ImportLicence  
  INNER JOIN PaThaKa ON PaThaKa.Id = ImportLicence.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN ImportLicenceItem ON ImportLicence.Id = ImportLicenceItem.ImportLicenceId  
  INNER JOIN Unit unit ON ImportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = ImportLicence.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = ImportLicence.SellerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = ImportLicence.ExportImportMethodId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = ImportLicence.ExportImportIncotermId  
  WHERE ApplyType='New' 
  AND ImportLicence.Status='Approved' AND ImportLicence.ImportLicenceNo <> ''  
  AND (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND ImportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then ImportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND ImportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then ImportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND ImportLicence.SellerCountryId=(CASE WHEN @SellerCountryId=0 then ImportLicence.SellerCountryId ELSE @SellerCountryId END)
  ORDER BY ImportLicence.LicenceDate
 END  
 IF(@Type='Border')  
 BEGIN  
  SELECT * FROM
  (SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  Sakhan.Id SakhanId,Sakhan.Code SakhanCode,Sakhan.Name SakhanName,ExportImportSectionId,ExportImportMethodId,ExportImportIncotermId,SellerCountryId, 
  section.Code SectionCode,section.Name SectionName,ImportLicenceNo LicenceNo,BorderImportLicence.IssuedDate LicenceDate,  
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  SellerName,SellerAddress,sellerCountry.Name SellerCountry,  
  PortofDischarge,LastDate,method.Name MethodName,
   (  
   SELECT ','+consignedCountry.Name  
   FROM Countries consignedCountry  
   WHERE ','+BorderImportLicence.ConsignedCountryId+',' LIKE '%,'+CAST(consignedCountry.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as ConsignedCountry,
   (  
   SELECT ','+countryofOrigin.Name  
   FROM Countries countryofOrigin  
   WHERE ','+BorderImportLicence.CountryofOriginId+',' LIKE '%,'+CAST(countryofOrigin.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,  
  HSCode.Code HSCode,BorderImportLicenceItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  BorderImportLicence.Remark Conditions
  FROM BorderImportLicence  
  INNER JOIN PaThaKa ON PaThaKa.Id = BorderImportLicence.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId  
  INNER JOIN Unit unit ON BorderImportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = BorderImportLicence.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = BorderImportLicence.SellerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = BorderImportLicence.ExportImportMethodId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = BorderImportLicence.ExportImportIncotermId  
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderImportLicence.SakhanId
  WHERE ApplyType='New' 
  AND BorderImportLicence.Status='Approved'  AND BorderImportLicence.CardType='Pa Tha Ka'
  AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND BorderImportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then BorderImportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND BorderImportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then BorderImportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND BorderImportLicence.SellerCountryId=(CASE WHEN @SellerCountryId=0 then BorderImportLicence.SellerCountryId ELSE @SellerCountryId END)
  AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
  UNION ALL
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  Sakhan.Id SakhanId,Sakhan.Code SakhanCode,Sakhan.Name SakhanName,ExportImportSectionId,ExportImportMethodId,ExportImportIncotermId,SellerCountryId, 
  section.Code SectionCode,section.Name SectionName,ImportLicenceNo LicenceNo,BorderImportLicence.IssuedDate LicenceDate,  
  IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  SellerName,SellerAddress,sellerCountry.Name SellerCountry,  
  PortofDischarge,LastDate,method.Name MethodName,
   (  
   SELECT ','+consignedCountry.Name  
   FROM Countries consignedCountry  
   WHERE ','+BorderImportLicence.ConsignedCountryId+',' LIKE '%,'+CAST(consignedCountry.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as ConsignedCountry,
   (  
   SELECT ','+countryofOrigin.Name  
   FROM Countries countryofOrigin  
   WHERE ','+BorderImportLicence.CountryofOriginId+',' LIKE '%,'+CAST(countryofOrigin.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,  
  HSCode.Code HSCode,BorderImportLicenceItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  BorderImportLicence.Remark Conditions
  FROM BorderImportLicence  
  INNER JOIN IndividualTrading ON IndividualTrading.Id = BorderImportLicence.IndividualTradingId
  INNER JOIN PaThaKaType paThaKaType ON IndividualTrading.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId  
  INNER JOIN Unit unit ON BorderImportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = BorderImportLicence.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = BorderImportLicence.SellerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = BorderImportLicence.ExportImportMethodId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = BorderImportLicence.ExportImportIncotermId  
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderImportLicence.SakhanId
  WHERE ApplyType='New' 
  AND BorderImportLicence.Status='Approved'  AND BorderImportLicence.CardType='Individual Trading'
  AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
  AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND BorderImportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then BorderImportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND BorderImportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then BorderImportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND BorderImportLicence.SellerCountryId=(CASE WHEN @SellerCountryId=0 then BorderImportLicence.SellerCountryId ELSE @SellerCountryId END)
  AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END))tmp
  ORDER BY tmp.LicenceDate
 END  
   
END

GO

/* dbo.sp_ImportLicenceDetailReport */
-- =============================================  
-- Author:  Name  
-- Create date:   
-- Description:   
-- =============================================  
 --exec sp_ReportExportLicenceDetail 'Oversea','2020-07-01 00:00:00','2020-07-13 23:59:59',0,0,0,0,0,''
CREATE PROCEDURE [dbo].[sp_ImportLicenceDetailReport]   
 -- Add the parameters for the stored procedure here  
 @Type nvarchar(20),  -- Oversea,Border
 @FromDate datetime,
 @ToDate datetime,
 @PaThaKaTypeId int,  
 @ExportImportSectionId int,  
 @ExportImportMethodId int,  
 @ExportImportIncotermId int,
 @SellerCountryId int,
 @CompanyRegistrationNo nvarchar(50),
 @SakhanId int
AS  
BEGIN  
   
 IF(@Type='Oversea')  
 BEGIN  
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  ExportImportSectionId,ExportImportMethodId,ExportImportIncotermId,SellerCountryId, 
  section.Code SectionCode,section.Name SectionName,ImportLicenceNo LicenceNo,ImportLicence.IssuedDate LicenceDate,  
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  SellerName,SellerAddress,sellerCountry.Name SellerCountry,  
  PortofDischarge,LastDate,method.Name MethodName,
   (  
   SELECT ','+consignedCountry.Name  
   FROM Countries consignedCountry  
   WHERE ','+ImportLicence.ConsignedCountryId+',' LIKE '%,'+CAST(consignedCountry.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as ConsignedCountry,
   (  
   SELECT ','+countryofOrigin.Name  
   FROM Countries countryofOrigin  
   WHERE ','+ImportLicence.CountryofOriginId+',' LIKE '%,'+CAST(countryofOrigin.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,  
  HSCode.Code HSCode,ImportLicenceItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  ImportLicence.Remark Conditions, ImportLicence.ApplicationNo,ImportLicence.ApplicationDate,
  ImportLicence.FESCNo,ImportLicence.CommodityType,ImportLicence.ApproveDate
  FROM ImportLicence  
  INNER JOIN PaThaKa ON PaThaKa.Id = ImportLicence.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN ImportLicenceItem ON ImportLicence.Id = ImportLicenceItem.ImportLicenceId  
  INNER JOIN Unit unit ON ImportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = ImportLicence.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = ImportLicence.SellerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = ImportLicence.ExportImportMethodId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = ImportLicence.ExportImportIncotermId  
  WHERE ApplyType='New' 
  AND ImportLicence.Status='Approved' AND ImportLicence.ImportLicenceNo <> ''  
  AND (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND ImportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then ImportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND ImportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then ImportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND ImportLicence.SellerCountryId=(CASE WHEN @SellerCountryId=0 then ImportLicence.SellerCountryId ELSE @SellerCountryId END)
  ORDER BY ImportLicence.LicenceDate
 END  
 IF(@Type='Border')  
 BEGIN  
  SELECT * FROM
  (SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  Sakhan.Id SakhanId,Sakhan.Code SakhanCode,Sakhan.Name SakhanName,ExportImportSectionId,ExportImportMethodId,ExportImportIncotermId,SellerCountryId, 
  section.Code SectionCode,section.Name SectionName,ImportLicenceNo LicenceNo,BorderImportLicence.IssuedDate LicenceDate,  
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  SellerName,SellerAddress,sellerCountry.Name SellerCountry,  
  PortofDischarge,LastDate,method.Name MethodName,
   (  
   SELECT ','+consignedCountry.Name  
   FROM Countries consignedCountry  
   WHERE ','+BorderImportLicence.ConsignedCountryId+',' LIKE '%,'+CAST(consignedCountry.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as ConsignedCountry,
   (  
   SELECT ','+countryofOrigin.Name  
   FROM Countries countryofOrigin  
   WHERE ','+BorderImportLicence.CountryofOriginId+',' LIKE '%,'+CAST(countryofOrigin.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,  
  HSCode.Code HSCode,BorderImportLicenceItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  BorderImportLicence.Remark Conditions,BorderImportLicence.ApplicationNo,
  BorderImportLicence.ApplicationDate,BorderImportLicence.FESCNo,
  BorderImportLicence.CommodityType,BorderImportLicence.ApproveDate
  FROM BorderImportLicence  
  INNER JOIN PaThaKa ON PaThaKa.Id = BorderImportLicence.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId  
  INNER JOIN Unit unit ON BorderImportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = BorderImportLicence.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = BorderImportLicence.SellerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = BorderImportLicence.ExportImportMethodId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = BorderImportLicence.ExportImportIncotermId  
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderImportLicence.SakhanId
  WHERE ApplyType='New' 
  AND BorderImportLicence.Status='Approved'  AND BorderImportLicence.CardType='Pa Tha Ka'
  AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND BorderImportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then BorderImportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND BorderImportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then BorderImportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND BorderImportLicence.SellerCountryId=(CASE WHEN @SellerCountryId=0 then BorderImportLicence.SellerCountryId ELSE @SellerCountryId END)
  AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
  UNION ALL
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  Sakhan.Id SakhanId,Sakhan.Code SakhanCode,Sakhan.Name SakhanName,ExportImportSectionId,ExportImportMethodId,ExportImportIncotermId,SellerCountryId, 
  section.Code SectionCode,section.Name SectionName,ImportLicenceNo LicenceNo,BorderImportLicence.IssuedDate LicenceDate,  
  IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  SellerName,SellerAddress,sellerCountry.Name SellerCountry,  
  PortofDischarge,LastDate,method.Name MethodName,
   (  
   SELECT ','+consignedCountry.Name  
   FROM Countries consignedCountry  
   WHERE ','+BorderImportLicence.ConsignedCountryId+',' LIKE '%,'+CAST(consignedCountry.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as ConsignedCountry,
   (  
   SELECT ','+countryofOrigin.Name  
   FROM Countries countryofOrigin  
   WHERE ','+BorderImportLicence.CountryofOriginId+',' LIKE '%,'+CAST(countryofOrigin.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,  
  HSCode.Code HSCode,BorderImportLicenceItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  BorderImportLicence.Remark Conditions,BorderImportLicence.ApplicationNo,
    BorderImportLicence.ApplicationDate,BorderImportLicence.FESCNo,
	  BorderImportLicence.CommodityType,BorderImportLicence.ApproveDate
  FROM BorderImportLicence  
  INNER JOIN IndividualTrading ON IndividualTrading.Id = BorderImportLicence.IndividualTradingId
  INNER JOIN PaThaKaType paThaKaType ON IndividualTrading.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId  
  INNER JOIN Unit unit ON BorderImportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = BorderImportLicence.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = BorderImportLicence.SellerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = BorderImportLicence.ExportImportMethodId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = BorderImportLicence.ExportImportIncotermId  
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderImportLicence.SakhanId
  WHERE ApplyType='New' 
  AND BorderImportLicence.Status='Approved'  AND BorderImportLicence.CardType='Individual Trading'
  AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
  AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND BorderImportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then BorderImportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND BorderImportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then BorderImportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND BorderImportLicence.SellerCountryId=(CASE WHEN @SellerCountryId=0 then BorderImportLicence.SellerCountryId ELSE @SellerCountryId END)
  AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END))tmp
  ORDER BY tmp.LicenceDate
 END  
   
END

GO

/* dbo.sp_ImportLicencePendingDetailReport */
-- =============================================  
-- Author:  Name  
-- Create date:   
-- Description:   
-- =============================================  
 --exec sp_ReportExportLicenceDetail 'Oversea','2020-07-01 00:00:00','2020-07-13 23:59:59',0,0,0,0,0,''
CREATE PROCEDURE [dbo].[sp_ImportLicencePendingDetailReport]   
 -- Add the parameters for the stored procedure here  
 @Type nvarchar(20),  -- Oversea,Border
 @FromDate datetime,
 @ToDate datetime,
 @PaThaKaTypeId int,  
 @ExportImportSectionId int,  
 @ExportImportMethodId int,  
 @ExportImportIncotermId int,
 @SellerCountryId int,
 @CompanyRegistrationNo nvarchar(50),
 @SakhanId int
AS  
BEGIN  
   
 IF(@Type='Oversea')  
 BEGIN  
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  ExportImportSectionId,ExportImportMethodId,ExportImportIncotermId,SellerCountryId, 
  section.Code SectionCode,section.Name SectionName,ImportLicenceNo LicenceNo,ImportLicence.IssuedDate LicenceDate,  
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  SellerName,SellerAddress,sellerCountry.Name SellerCountry,  
  PortofDischarge,LastDate,method.Name MethodName,
   (  
   SELECT ','+consignedCountry.Name  
   FROM Countries consignedCountry  
   WHERE ','+ImportLicence.ConsignedCountryId+',' LIKE '%,'+CAST(consignedCountry.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as ConsignedCountry,
   (  
   SELECT ','+countryofOrigin.Name  
   FROM Countries countryofOrigin  
   WHERE ','+ImportLicence.CountryofOriginId+',' LIKE '%,'+CAST(countryofOrigin.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,  
  HSCode.Code HSCode,ImportLicenceItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  ImportLicence.Remark Conditions, ImportLicence.ApplicationNo,ImportLicence.ApplicationDate,
  ImportLicence.FESCNo,ImportLicence.CommodityType
  FROM ImportLicence  
  INNER JOIN PaThaKa ON PaThaKa.Id = ImportLicence.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN ImportLicenceItem ON ImportLicence.Id = ImportLicenceItem.ImportLicenceId  
  INNER JOIN Unit unit ON ImportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON ImportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = ImportLicence.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = ImportLicence.SellerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = ImportLicence.ExportImportMethodId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = ImportLicence.ExportImportIncotermId  
  WHERE ApplyType='New' 
  AND ImportLicence.Status='Pending'   
  AND (ImportLicence.ApplicationDate>=@FromDate AND ImportLicence.ApplicationDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND ImportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then ImportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND ImportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then ImportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND ImportLicence.SellerCountryId=(CASE WHEN @SellerCountryId=0 then ImportLicence.SellerCountryId ELSE @SellerCountryId END)
  ORDER BY ImportLicence.LicenceDate
 END  
 IF(@Type='Border')  
 BEGIN  
  SELECT * FROM
  (SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  Sakhan.Id SakhanId,Sakhan.Code SakhanCode,Sakhan.Name SakhanName,ExportImportSectionId,ExportImportMethodId,ExportImportIncotermId,SellerCountryId, 
  section.Code SectionCode,section.Name SectionName,ImportLicenceNo LicenceNo,BorderImportLicence.IssuedDate LicenceDate,  
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  SellerName,SellerAddress,sellerCountry.Name SellerCountry,  
  PortofDischarge,LastDate,method.Name MethodName,
   (  
   SELECT ','+consignedCountry.Name  
   FROM Countries consignedCountry  
   WHERE ','+BorderImportLicence.ConsignedCountryId+',' LIKE '%,'+CAST(consignedCountry.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as ConsignedCountry,
   (  
   SELECT ','+countryofOrigin.Name  
   FROM Countries countryofOrigin  
   WHERE ','+BorderImportLicence.CountryofOriginId+',' LIKE '%,'+CAST(countryofOrigin.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,  
  HSCode.Code HSCode,BorderImportLicenceItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  BorderImportLicence.Remark Conditions,BorderImportLicence.ApplicationNo,
  BorderImportLicence.ApplicationDate,BorderImportLicence.FESCNo,
  BorderImportLicence.CommodityType
  FROM BorderImportLicence  
  INNER JOIN PaThaKa ON PaThaKa.Id = BorderImportLicence.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId  
  INNER JOIN Unit unit ON BorderImportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = BorderImportLicence.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = BorderImportLicence.SellerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = BorderImportLicence.ExportImportMethodId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = BorderImportLicence.ExportImportIncotermId  
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderImportLicence.SakhanId
  WHERE ApplyType='New' 
  AND BorderImportLicence.Status='Pending'  AND BorderImportLicence.CardType='Pa Tha Ka'
  AND (BorderImportLicence.ApplicationDate>=@FromDate AND BorderImportLicence.ApplicationDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND BorderImportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then BorderImportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND BorderImportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then BorderImportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND BorderImportLicence.SellerCountryId=(CASE WHEN @SellerCountryId=0 then BorderImportLicence.SellerCountryId ELSE @SellerCountryId END)
  AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
  UNION ALL
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  Sakhan.Id SakhanId,Sakhan.Code SakhanCode,Sakhan.Name SakhanName,ExportImportSectionId,ExportImportMethodId,ExportImportIncotermId,SellerCountryId, 
  section.Code SectionCode,section.Name SectionName,ImportLicenceNo LicenceNo,BorderImportLicence.IssuedDate LicenceDate,  
  IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  SellerName,SellerAddress,sellerCountry.Name SellerCountry,  
  PortofDischarge,LastDate,method.Name MethodName,
   (  
   SELECT ','+consignedCountry.Name  
   FROM Countries consignedCountry  
   WHERE ','+BorderImportLicence.ConsignedCountryId+',' LIKE '%,'+CAST(consignedCountry.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as ConsignedCountry,
   (  
   SELECT ','+countryofOrigin.Name  
   FROM Countries countryofOrigin  
   WHERE ','+BorderImportLicence.CountryofOriginId+',' LIKE '%,'+CAST(countryofOrigin.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,  
  HSCode.Code HSCode,BorderImportLicenceItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  BorderImportLicence.Remark Conditions,BorderImportLicence.ApplicationNo,
    BorderImportLicence.ApplicationDate,BorderImportLicence.FESCNo,
	  BorderImportLicence.CommodityType
  FROM BorderImportLicence  
  INNER JOIN IndividualTrading ON IndividualTrading.Id = BorderImportLicence.IndividualTradingId
  INNER JOIN PaThaKaType paThaKaType ON IndividualTrading.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderImportLicenceItem ON BorderImportLicence.Id = BorderImportLicenceItem.BorderImportLicenceId  
  INNER JOIN Unit unit ON BorderImportLicenceItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON BorderImportLicenceItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = BorderImportLicence.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = BorderImportLicence.SellerCountryId  
  INNER JOIN ExportImportMethod method ON method.Id  = BorderImportLicence.ExportImportMethodId  
  INNER JOIN ExportImportIncoterm incoterm ON incoterm.Id  = BorderImportLicence.ExportImportIncotermId  
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderImportLicence.SakhanId
  WHERE ApplyType='New' 
  AND BorderImportLicence.Status='Pending'  AND BorderImportLicence.CardType='Individual Trading'
  AND (BorderImportLicence.ApplicationDate>=@FromDate AND BorderImportLicence.ApplicationDate<=@ToDate)
  AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND BorderImportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then BorderImportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
  AND BorderImportLicence.ExportImportIncotermId=(CASE WHEN @ExportImportIncotermId=0 then BorderImportLicence.ExportImportIncotermId ELSE @ExportImportIncotermId END)
  AND BorderImportLicence.SellerCountryId=(CASE WHEN @SellerCountryId=0 then BorderImportLicence.SellerCountryId ELSE @SellerCountryId END)
  AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END))tmp
  ORDER BY tmp.LicenceDate
 END  
   
END

GO

/* dbo.sp_ImportPermitDetailReport */
-- =============================================  
-- Author:  Name  
-- Create date:   
-- Description:   
-- =============================================  
 --exec [sp_ExportPermitDetailReport] 'Oversea','2020-07-01 00:00:00','2020-07-13 23:59:59',0,0,0,0,0,''
CREATE PROCEDURE [dbo].[sp_ImportPermitDetailReport]   
 -- Add the parameters for the stored procedure here  
 @Type nvarchar(20),  -- Oversea,Border
 @FromDate datetime,
 @ToDate datetime,
 @PaThaKaTypeId int,  
 @ExportImportSectionId int,  
 @SellerCountryId int,
 @CompanyRegistrationNo nvarchar(50),
 @SakhanId int
AS  
BEGIN  
   
 IF(@Type='Oversea')  
 BEGIN  
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  ExportImportSectionId,SellerCountryId, 
  section.Code SectionCode,section.Name SectionName,ImportPermitNo LicenceNo,ImportPermit.IssuedDate LicenceDate,  
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  AuthorisedAgentName,AuthorisedAgentAddress,sellerCountry.Name SellerCountry,  
  (  
   SELECT ','+portofShipment.Name  
   FROM PortOfDischarge portofShipment  
   WHERE ','+ImportPermit.PortofShipmentId+',' LIKE '%,'+CAST(portofShipment.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as PortofShipment,
  PortofDischarge,
  (  
   SELECT ','+countries.Name  
   FROM Countries countries  
   WHERE ','+ImportPermit.CountryofOriginId+',' LIKE '%,'+CAST(countries.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,LastDate,
  HSCode.Code HSCode,ImportPermitItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  dbo.fn_GetNRCNo(ImportPermit.NRCType,ImportPermit.NRCPrefixId,ImportPermit.NRCPrefixCodeId,ImportPermit.NRCNo) NRCNo,
  PermitType,ImportPermit.Remark Conditions,ImportPermit.ApproveDate
  FROM ImportPermit  
  INNER JOIN PaThaKa ON PaThaKa.Id = ImportPermit.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN ImportPermitItem ON ImportPermit.Id = ImportPermitItem.ImportPermitId  
  INNER JOIN Unit unit ON ImportPermitItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON ImportPermitItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = ImportPermit.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = ImportPermit.SellerCountryId  
  WHERE ApplyType='New' 
  AND ImportPermit.Status='Approved'  
  AND (ImportPermit.CreatedDate>=@FromDate AND ImportPermit.CreatedDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND ImportPermit.SellerCountryId=(CASE WHEN @SellerCountryId=0 then ImportPermit.SellerCountryId ELSE @SellerCountryId END)
 END  
 ELSE IF(@Type='Border')  
 BEGIN  
  SELECT paThaKaType.Id PaThaKaTypeId,paThaKaType.Code PaThaKaTypeCode,paThaKaType.Description PaThaKaTypeName,
  sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,ExportImportSectionId,SellerCountryId, 
  section.Code SectionCode,section.Name SectionName,ImportPermitNo LicenceNo,BorderImportPermit.IssuedDate LicenceDate,  
  CompanyRegistrationNo,CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
  AuthorisedAgentName,AuthorisedAgentAddress,sellerCountry.Name SellerCountry,  
  (  
   SELECT ','+portofShipment.Name  
   FROM PortOfDischarge portofShipment  
   WHERE ','+BorderImportPermit.PortofShipmentId+',' LIKE '%,'+CAST(portofShipment.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as PortofShipment,
  PortofDischarge,
  (  
   SELECT ','+countries.Name  
   FROM Countries countries  
   WHERE ','+BorderImportPermit.CountryofOriginId+',' LIKE '%,'+CAST(countries.Id as nvarchar(20)) +',%'  
   for xml path(''), type  
  ).value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin,LastDate,
  HSCode.Code HSCode,BorderImportPermitItem.Description HSDescription,  
  unit.Code Unit,Price,Quantity,Amount,currency.Code Currency,
  dbo.fn_GetNRCNo(BorderImportPermit.NRCType,BorderImportPermit.NRCPrefixId,BorderImportPermit.NRCPrefixCodeId,BorderImportPermit.NRCNo) NRCNo,
  PermitType,BorderImportPermit.Remark Conditions,BorderImportPermit.ApproveDate
  FROM BorderImportPermit  
  INNER JOIN PaThaKa ON PaThaKa.Id = BorderImportPermit.PaThaKaId
  INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
  INNER JOIN BorderImportPermitItem ON BorderImportPermit.Id = BorderImportPermitItem.BorderImportPermitId  
  INNER JOIN Unit unit ON BorderImportPermitItem.UnitId = unit.Id  
  INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id  
  INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id  
  INNER JOIN ExportImportSection section ON section.Id  = BorderImportPermit.ExportImportSectionId  
  INNER JOIN Countries sellerCountry ON sellerCountry.Id  = BorderImportPermit.SellerCountryId  
  INNER JOIN Sakhan sakhan ON sakhan.Id = BorderImportPermit.SakhanId
  WHERE ApplyType='New' 
  AND BorderImportPermit.Status='Approved'  
  AND (BorderImportPermit.CreatedDate>=@FromDate AND BorderImportPermit.CreatedDate<=@ToDate)
  AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
  AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)
  AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
  AND BorderImportPermit.SellerCountryId=(CASE WHEN @SellerCountryId=0 then BorderImportPermit.SellerCountryId ELSE @SellerCountryId END)
  AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
 END  
   
END

GO

/* dbo.sp_LicencePermitSearch */
-- =============================================
-- Author:      Name
-- Create Date: 
-- Description: 
-- =============================================
--exec sp_LicencePermitSearch 'OVSIL12021000002'
CREATE PROCEDURE [dbo].[sp_LicencePermitSearch]
(
    -- Add the parameters for the stored procedure here
    @LicenceNo nvarchar(100)
)
AS
BEGIN
    SELECT TOP 1 Id,FormType FROM 
	(SELECT Id,'Export Licence' FormType,ExportLicenceNo LicenceNo,CreatedDate FROM ExportLicence 
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Export Licence' FormType,OldExportLicenceNo LicenceNo,CreatedDate FROM ExportLicence 
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Export Permit' FormType,ExportPermitNo LicenceNo,CreatedDate FROM ExportPermit
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Export Permit' FormType,OldExportPermitNo LicenceNo,CreatedDate FROM ExportPermit 
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Import Licence' FormType,ImportLicenceNo LicenceNo,CreatedDate FROM ImportLicence
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Import Licence' FormType,OldImportLicenceNo LicenceNo,CreatedDate FROM ImportLicence 
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Import Permit' FormType,ImportPermitNo LicenceNo,CreatedDate FROM ImportPermit 
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Import Permit' FormType,OldImportPermitNo LicenceNo,CreatedDate FROM ImportPermit
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Border Export Licence' FormType,ExportLicenceNo LicenceNo,CreatedDate FROM BorderExportLicence 
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Border Export Licence' FormType,OldExportLicenceNo LicenceNo,CreatedDate FROM BorderExportLicence 
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Border Export Permit' FormType,ExportPermitNo LicenceNo,CreatedDate FROM BorderExportPermit 
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Border Export Permit' FormType,OldExportPermitNo LicenceNo,CreatedDate FROM BorderExportPermit 
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Border Import Licence' FormType,ImportLicenceNo LicenceNo,CreatedDate FROM BorderImportLicence 
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Border Import Licence' FormType,OldImportLicenceNo LicenceNo,CreatedDate FROM BorderImportLicence 
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Border Import Permit' FormType,ImportPermitNo LicenceNo,CreatedDate FROM BorderImportPermit 
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Border Import Permit' FormType,OldImportPermitNo LicenceNo,CreatedDate FROM BorderImportPermit 
	WHERE Status='Approved' AND ApplyType<>'New'
	)tmp
	WHERE tmp.LicenceNo=@LicenceNo
	ORDER BY CreatedDate DESC
END

GO

/* dbo.sp_LicencePermitSearch_old */
-- =============================================
-- Author:      Name
-- Create Date: 
-- Description: 
-- =============================================
--exec sp_LicencePermitSearch 'OVSIL12021000002'
CREATE PROCEDURE [dbo].[sp_LicencePermitSearch_old]
(
    -- Add the parameters for the stored procedure here
    @LicenceNo nvarchar(100)
)
AS
BEGIN
    SELECT TOP 1 Id,FormType FROM 
	(SELECT Id,'Export Licence' FormType,ExportLicenceNo LicenceNo,CreatedDate FROM ExportLicence WITH(INDEX(IX_ExportLicence_LicenceNo))
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Export Licence' FormType,OldExportLicenceNo LicenceNo,CreatedDate FROM ExportLicence WITH(INDEX(IX_ExportLicence_LicenceNo))
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Export Permit' FormType,ExportPermitNo LicenceNo,CreatedDate FROM ExportPermit WITH(INDEX(IX_ExportPermit_PermitNo))
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Export Permit' FormType,OldExportPermitNo LicenceNo,CreatedDate FROM ExportPermit WITH(INDEX(IX_ExportPermit_PermitNo))
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Import Licence' FormType,ImportLicenceNo LicenceNo,CreatedDate FROM ImportLicence WITH(INDEX(IX_ImportLicence_LicenceNo))
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Import Licence' FormType,OldImportLicenceNo LicenceNo,CreatedDate FROM ImportLicence WITH(INDEX(IX_ImportLicence_LicenceNo))
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Import Permit' FormType,ImportPermitNo LicenceNo,CreatedDate FROM ImportPermit WITH(INDEX(IX_ImportPermit_PermitNo))
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Import Permit' FormType,OldImportPermitNo LicenceNo,CreatedDate FROM ImportPermit WITH(INDEX(IX_ImportPermit_PermitNo))
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Border Export Licence' FormType,ExportLicenceNo LicenceNo,CreatedDate FROM BorderExportLicence WITH(INDEX(IX_BorderExportLicence_LicenceNo))
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Border Export Licence' FormType,OldExportLicenceNo LicenceNo,CreatedDate FROM BorderExportLicence WITH(INDEX(IX_BorderExportLicence_LicenceNo))
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Border Export Permit' FormType,ExportPermitNo LicenceNo,CreatedDate FROM BorderExportPermit WITH(INDEX(IX_BorderExportPermit_PermitNo))
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Border Export Permit' FormType,OldExportPermitNo LicenceNo,CreatedDate FROM BorderExportPermit WITH(INDEX(IX_BorderExportPermit_PermitNo))
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Border Import Licence' FormType,ImportLicenceNo LicenceNo,CreatedDate FROM BorderImportLicence WITH(INDEX(IX_BorderImportLicence_LicenceNo))
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Border Import Licence' FormType,OldImportLicenceNo LicenceNo,CreatedDate FROM BorderImportLicence WITH(INDEX(IX_BorderImportLicence_LicenceNo))
	WHERE Status='Approved' AND ApplyType<>'New'
	UNION ALL
	SELECT Id,'Border Import Permit' FormType,ImportPermitNo LicenceNo,CreatedDate FROM BorderImportPermit WITH(INDEX(IX_BorderImportPermit_PermitNo))
	WHERE Status='Approved' AND ApplyType='New'
	UNION ALL 
	SELECT Id,'Border Import Permit' FormType,OldImportPermitNo LicenceNo,CreatedDate FROM BorderImportPermit WITH(INDEX(IX_BorderImportPermit_PermitNo))
	WHERE Status='Approved' AND ApplyType<>'New'
	)tmp
	WHERE tmp.LicenceNo=@LicenceNo
	ORDER BY CreatedDate DESC
END

GO

/* dbo.sp_MemberRegistrationReport */
-- =============================================
-- Author:      Name
-- Create Date: 
-- Description: 
-- =============================================
--EXEC sp_MemberRegistrationReport '2020-11-01 00:00:00','2020-11-03 23:59:59','All'
CREATE PROCEDURE [dbo].[sp_MemberRegistrationReport]
(
   @FromDate datetime,
   @ToDate datetime,
   @ApplyType nvarchar(50)
)
AS
BEGIN
	IF(@ApplyType='All')
	BEGIN
		SELECT * FROM
		(SELECT MemberRegistration.Id,MemberRegistration.ApplyType,MemberCode,Email,FullName,Mobile1,Mobile2,Mobile3,
		dbo.fn_GetNRCNo(NRCType,NRCPrefixId,NRCPrefixCodeId,NRCNo) NRCNo,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State.Name State,Countries.Name Country,PostalCode,
		IssuedDate,StartDate,EndDate
		FROM MemberRegistration
		INNER JOIN State ON MemberRegistration.StateId = State.Id
		INNER JOIN Countries ON MemberRegistration.CountryId = Countries.Id
		WHERE (MemberRegistration.IssuedDate>=@FromDate AND MemberRegistration.IssuedDate<=@ToDate)
		AND MemberRegistration.ApplyType='New'
		AND MemberRegistration.Status='Approved'
		UNION ALL
		SELECT MemberRegistration.Id,MemberRegistration.ApplyType,MemberCode,Email,FullName,Mobile1,Mobile2,Mobile3,
		dbo.fn_GetNRCNo(NRCType,NRCPrefixId,NRCPrefixCodeId,NRCNo) NRCNo,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State.Name State,Countries.Name Country,PostalCode,
		ExtensionDate IssuedDate,StartDate,EndDate
		FROM MemberRegistration
		INNER JOIN State ON MemberRegistration.StateId = State.Id
		INNER JOIN Countries ON MemberRegistration.CountryId = Countries.Id
		WHERE (MemberRegistration.ExtensionDate>=@FromDate AND MemberRegistration.ExtensionDate<=@ToDate)
		AND MemberRegistration.ApplyType='Extension'
		AND MemberRegistration.Status='Approved')tmp
		ORDER BY tmp.IssuedDate
	END
	ELSE IF(@ApplyType='New')
	BEGIN
		SELECT * FROM
		(SELECT MemberRegistration.Id,MemberRegistration.ApplyType,MemberCode,Email,FullName,Mobile1,Mobile2,Mobile3,
		dbo.fn_GetNRCNo(NRCType,NRCPrefixId,NRCPrefixCodeId,NRCNo) NRCNo,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State.Name State,Countries.Name Country,PostalCode,
		IssuedDate,StartDate,EndDate
		FROM MemberRegistration
		INNER JOIN State ON MemberRegistration.StateId = State.Id
		INNER JOIN Countries ON MemberRegistration.CountryId = Countries.Id
		WHERE (MemberRegistration.IssuedDate>=@FromDate AND MemberRegistration.IssuedDate<=@ToDate)
		AND MemberRegistration.ApplyType='New'
		AND MemberRegistration.Status='Approved'
		)tmp
		ORDER BY tmp.IssuedDate
	END
	ELSE IF(@ApplyType='Extension')
	BEGIN
		SELECT * FROM
		(SELECT MemberRegistration.Id,MemberRegistration.ApplyType,MemberCode,Email,FullName,Mobile1,Mobile2,Mobile3,
		dbo.fn_GetNRCNo(NRCType,NRCPrefixId,NRCPrefixCodeId,NRCNo) NRCNo,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State.Name State,Countries.Name Country,PostalCode,
		IssuedDate,StartDate,EndDate
		FROM MemberRegistration
		INNER JOIN State ON MemberRegistration.StateId = State.Id
		INNER JOIN Countries ON MemberRegistration.CountryId = Countries.Id
		WHERE (MemberRegistration.ExtensionDate>=@FromDate AND MemberRegistration.ExtensionDate<=@ToDate)
		AND MemberRegistration.ApplyType='Extension'
		AND MemberRegistration.Status='Approved'
		)tmp
		ORDER BY tmp.IssuedDate
	END
    
END

GO

/* dbo.sp_MPUReport */

-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_MPUReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@FormType nvarchar(200),
	@PaymentType nvarchar(200)
AS
 IF (@FromDate < '2025-11-15')
    BEGIN
       
	SELECT MPUPaymentTransaction.Id,Sakhan,TransactionDateTime,
	ISNULL((SELECT TOP 1 CompanyName FROM PaThaka 
	WHERE PaThaka.PaThaKaNo=MPUPaymentTransaction.PaThaKaNo OR PaThaka.CompanyRegistrationNo=MPUPaymentTransaction.PaThaKaNo),'') CompanyName,
	MPUPaymentTransaction.PaThaKaNo CompanyRegistrationNo,dbo.MPUPaymentTransaction.ApplicationNo,
	MPUPaymentTransaction.MerchantId,AccountNo,InvoiceNo,ApprovalCode,TransactionRefNo,TransactionAmount,
	MOCAmount,IMAmount,dbo.MPUPaymentTransaction.FormType,dbo.MPUPaymentTransaction.ApplyType,
	(SELECT TOP 1 VoucherNo FROM AccountTransaction 
	Where MPUPaymentTransaction.TransactionId= AccountTransaction.TransactionId 
	AND AccountTransaction.TotalAmount<>3000
	order by CreatedDate desc
	) VoucherNo
	FROM dbo.MPUPaymentTransaction
	WHERE dbo.MPUPaymentTransaction.ResponseCode='00'
	AND (TransactionDateTime>=@FromDate AND TransactionDateTime<=@ToDate)
	AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	--AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	AND dbo.MPUPaymentTransaction.PaymentType=@PaymentType
	AND MPUPaymentTransaction.MOCAmount<>3000
UNION
	SELECT MPUPaymentTransaction.Id,Sakhan,TransactionDateTime,
	ISNULL((SELECT TOP 1 CompanyName FROM PaThaka 
	WHERE PaThaka.PaThaKaNo=MPUPaymentTransaction.PaThaKaNo OR PaThaka.CompanyRegistrationNo=MPUPaymentTransaction.PaThaKaNo),'') CompanyName,
	MPUPaymentTransaction.PaThaKaNo CompanyRegistrationNo,dbo.MPUPaymentTransaction.ApplicationNo,
	MPUPaymentTransaction.MerchantId,AccountNo,InvoiceNo,ApprovalCode,TransactionRefNo,TransactionAmount,
	MOCAmount,IMAmount,dbo.MPUPaymentTransaction.FormType,dbo.MPUPaymentTransaction.ApplyType,
	(SELECT TOP 1 VoucherNo FROM AccountTransaction 
	Where MPUPaymentTransaction.TransactionId= AccountTransaction.TransactionId 
	AND AccountTransaction.TotalAmount=3000
	order by CreatedDate desc
	) VoucherNo
	FROM dbo.MPUPaymentTransaction
	WHERE dbo.MPUPaymentTransaction.ResponseCode='00'
	AND (TransactionDateTime>=@FromDate AND TransactionDateTime<=@ToDate)
	AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	--AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	AND dbo.MPUPaymentTransaction.PaymentType=@PaymentType
	AND MPUPaymentTransaction.MOCAmount=3000
	ORDER BY TransactionDateTime

    END
    ELSE IF (@FromDate >= '2025-11-15')
   BEGIN
	SELECT MPUPaymentTransaction.Id,Sakhan,TransactionDateTime,
	ISNULL((SELECT TOP 1 CompanyName FROM PaThaka 
	WHERE PaThaka.PaThaKaNo=MPUPaymentTransaction.PaThaKaNo OR PaThaka.CompanyRegistrationNo=MPUPaymentTransaction.PaThaKaNo),'') CompanyName,
	MPUPaymentTransaction.PaThaKaNo CompanyRegistrationNo,dbo.MPUPaymentTransaction.ApplicationNo,
	MPUPaymentTransaction.MerchantId,AccountNo,InvoiceNo,ApprovalCode,TransactionRefNo,TransactionAmount,
	MOCAmount,IMAmount,dbo.MPUPaymentTransaction.FormType,dbo.MPUPaymentTransaction.ApplyType,
	(SELECT TOP 1 VoucherNo FROM AccountTransaction 
	Where MPUPaymentTransaction.TransactionId= AccountTransaction.TransactionId 
	AND AccountTransaction.TotalAmount<>10000
	order by CreatedDate desc
	) VoucherNo
	FROM dbo.MPUPaymentTransaction
	WHERE dbo.MPUPaymentTransaction.ResponseCode='00'
	AND (TransactionDateTime>=@FromDate AND TransactionDateTime<=@ToDate)
	AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	--AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	AND dbo.MPUPaymentTransaction.PaymentType=@PaymentType
	AND MPUPaymentTransaction.MOCAmount<>10000
UNION
	SELECT MPUPaymentTransaction.Id,Sakhan,TransactionDateTime,
	ISNULL((SELECT TOP 1 CompanyName FROM PaThaka 
	WHERE PaThaka.PaThaKaNo=MPUPaymentTransaction.PaThaKaNo OR PaThaka.CompanyRegistrationNo=MPUPaymentTransaction.PaThaKaNo),'') CompanyName,
	MPUPaymentTransaction.PaThaKaNo CompanyRegistrationNo,dbo.MPUPaymentTransaction.ApplicationNo,
	MPUPaymentTransaction.MerchantId,AccountNo,InvoiceNo,ApprovalCode,TransactionRefNo,TransactionAmount,
	MOCAmount,IMAmount,dbo.MPUPaymentTransaction.FormType,dbo.MPUPaymentTransaction.ApplyType,
	(SELECT TOP 1 VoucherNo FROM AccountTransaction 
	Where MPUPaymentTransaction.TransactionId= AccountTransaction.TransactionId 
	AND AccountTransaction.TotalAmount=10000
	order by CreatedDate desc
	) VoucherNo
	FROM dbo.MPUPaymentTransaction
	WHERE dbo.MPUPaymentTransaction.ResponseCode='00'
	AND (TransactionDateTime>=@FromDate AND TransactionDateTime<=@ToDate)
	AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	--AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	AND dbo.MPUPaymentTransaction.PaymentType=@PaymentType
	AND MPUPaymentTransaction.MOCAmount=10000
	ORDER BY TransactionDateTime
END
    


GO

/* dbo.sp_MPUReport_Seperated_OnineFee */

-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_MPUReport_Seperated_OnineFee] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@FormType nvarchar(200),
	@PaymentType nvarchar(200),
	@ReportType nvarchar(200)
AS

   
   BEGIN
   --ONlineFee
	SELECT MPUPaymentTransaction.Id,Sakhan,TransactionDateTime,
	ISNULL((SELECT TOP 1 CompanyName FROM PaThaka 
	WHERE PaThaka.PaThaKaNo=MPUPaymentTransaction.PaThaKaNo OR PaThaka.CompanyRegistrationNo=MPUPaymentTransaction.PaThaKaNo),'') CompanyName,
	MPUPaymentTransaction.PaThaKaNo CompanyRegistrationNo,dbo.MPUPaymentTransaction.ApplicationNo,
	MPUPaymentTransaction.MerchantId,AccountNo,InvoiceNo,ApprovalCode,TransactionRefNo,TransactionAmount,
	MOCAmount,IMAmount,dbo.MPUPaymentTransaction.FormType,dbo.MPUPaymentTransaction.ApplyType,
	(SELECT TOP 1 VoucherNo FROM AccountTransaction 
	JOin AccountTransactionDetail as AD on Ad.AccountTransactionId = AccountTransaction.Id
	Where MPUPaymentTransaction.TransactionId= AccountTransaction.TransactionId 
	AND AccountTransaction.TotalAmount =10000
	AND AD.AccountTitleId=1
	order by CreatedDate desc
	) VoucherNo
	FROM dbo.MPUPaymentTransaction
	WHERE dbo.MPUPaymentTransaction.ResponseCode='00'
	AND (TransactionDateTime>=@FromDate AND TransactionDateTime<=@ToDate)
	AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	--AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	AND dbo.MPUPaymentTransaction.PaymentType=@PaymentType
	--AND MPUPaymentTransaction.MOCAmount<>10000
	AND MPUPaymentTransaction.ApplicationNo = ''
	AND (dbo.MPUPaymentTransaction.FormType = 'Border Import Licence' or 
	dbo.MPUPaymentTransaction.FormType = 'Import Licence' or
	dbo.MPUPaymentTransaction.FormType = 'Import Permit' or
	dbo.MPUPaymentTransaction.FormType = 'Border Import Permit')
UNION
	SELECT MPUPaymentTransaction.Id,Sakhan,TransactionDateTime,
	ISNULL((SELECT TOP 1 CompanyName FROM PaThaka 
	WHERE PaThaka.PaThaKaNo=MPUPaymentTransaction.PaThaKaNo OR PaThaka.CompanyRegistrationNo=MPUPaymentTransaction.PaThaKaNo),'') CompanyName,
	MPUPaymentTransaction.PaThaKaNo CompanyRegistrationNo,dbo.MPUPaymentTransaction.ApplicationNo,
	MPUPaymentTransaction.MerchantId,AccountNo,InvoiceNo,ApprovalCode,TransactionRefNo,TransactionAmount,
	MOCAmount,IMAmount,dbo.MPUPaymentTransaction.FormType,dbo.MPUPaymentTransaction.ApplyType,
	--(SELECT TOP 1 VoucherNo FROM AccountTransaction 
	--Where MPUPaymentTransaction.TransactionId= AccountTransaction.TransactionId 
	
	--order by CreatedDate desc
	--) VoucherNo
	(SELECT TOP 1 VoucherNo FROM AccountTransaction 
	JOin AccountTransactionDetail as AD on Ad.AccountTransactionId = AccountTransaction.Id
	Where MPUPaymentTransaction.TransactionId= AccountTransaction.TransactionId 
	AND AccountTransaction.TotalAmount =10000
	AND AD.AccountTitleId=1
	order by CreatedDate desc
	) VoucherNo
	FROM dbo.MPUPaymentTransaction
	WHERE dbo.MPUPaymentTransaction.ResponseCode='00'
	AND (TransactionDateTime>=@FromDate AND TransactionDateTime<=@ToDate)
	AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	--AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	AND dbo.MPUPaymentTransaction.PaymentType=@PaymentType
	--AND MPUPaymentTransaction.MOCAmount=10000
	AND dbo.MPUPaymentTransaction.ApplicationNo <> ''
	AND (dbo.MPUPaymentTransaction.FormType = 'Border Import Licence' or 
	dbo.MPUPaymentTransaction.FormType = 'Import Licence' or
	dbo.MPUPaymentTransaction.FormType = 'Import Permit' or
	dbo.MPUPaymentTransaction.FormType = 'Border Import Permit')
	ORDER BY TransactionDateTime
END
    


GO

/* dbo.sp_MPUReport_V3 */

-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_MPUReport_V3] 
    @FromDate    DATETIME,
    @ToDate      DATETIME,
    @FormType    NVARCHAR(200),
    @PaymentType NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    WITH MPU AS (
        SELECT m.*,
               ROW_NUMBER() OVER (
                   PARTITION BY m.TransactionId
                   ORDER BY m.TransactionDateTime, m.Id
               ) AS rn
        FROM MPUPaymentTransaction m
    ),
    ACC AS (
        SELECT a.TransactionId,
               a.VoucherNo,
               a.TotalAmount,
               a.PaymentDate,
               ROW_NUMBER() OVER (
                   PARTITION BY a.TransactionId
                   --ORDER BY a.PaymentDate, a.Id
				   ORDER BY a.CreatedDate, a.Id
               ) AS rn
        FROM AccountTransaction a
    )
    SELECT
        m.Id,
        m.Sakhan,
        m.TransactionDateTime,
        ISNULL((
            SELECT TOP 1 p.CompanyName
            FROM PaThaka p
            WHERE p.PaThaKaNo = m.PaThaKaNo
               OR p.CompanyRegistrationNo = m.PaThaKaNo
        ), '') AS CompanyName,
        m.PaThaKaNo AS CompanyRegistrationNo,
        m.ApplicationNo,
        m.MerchantId,
        m.AccountNo,
        m.InvoiceNo,
        m.ApprovalCode,
        m.TransactionRefNo,
        m.TransactionAmount,
        m.MOCAmount,
        m.IMAmount,
        m.FormType,
        m.ApplyType,
        a.VoucherNo,
        a.TotalAmount,
        a.PaymentDate
    FROM MPU m
    LEFT JOIN ACC a
        ON m.TransactionId = a.TransactionId
       AND m.rn = a.rn
    WHERE m.TransactionDateTime >= @FromDate
      AND m.TransactionDateTime <=  @ToDate
      AND m.ResponseCode = '00'
      AND a.VoucherNo IS NOT NULL
      AND m.FormType LIKE (CASE WHEN @FormType='' THEN m.FormType+'%' ELSE @FormType+'%' END)
	--AND dbo.MPUPaymentTransaction.FormType LIKE (CASE WHEN @FormType='' THEN dbo.MPUPaymentTransaction.FormType+'%' ELSE @FormType+'%' END)
	AND m.PaymentType=@PaymentType
    ORDER BY m.TransactionId, m.TransactionDateTime;
END;


GO

/* dbo.sp_MPUReportV2 */
CREATE PROCEDURE [dbo].[sp_MPUReportV2]
(
    -- Add the parameters for the stored procedure here
    @FromDate datetime,
	@ToDate datetime,
    @FormType nvarchar(50),
	@PaymentType nvarchar(200)
)
AS
BEGIN
	SELECT 
	ISNULL(
	(SELECT TOP 1 Code FROM Sakhan 
	WHERE Sakhan.Id=tmp.SakhanId),'') Sakhan,
	TransactionDateTime,
	ISNULL(
	(SELECT TOP 1 CompanyName FROM PaThaka 
	WHERE PaThaka.CompanyRegistrationNo=tmp.CompanyRegistrationNo),'') CompanyName,
	PaThaKaNo CompanyRegistrationNo,
	ApplicationNo,
	MerchantId,
	AccountNo,
	InvoiceNo,
	ApprovalCode,
	TransactionRefNo,
	TransactionAmount,
	MOCAmount,
	IMAmount,
	MPUPaymentTransaction.FormType,
	ApplyType,
	tmp.VoucherNo
	FROM
    (
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,'' CompanyRegistrationNo,VoucherNo,'' CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Member' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN MemberRegistration ON AccountTransaction.TransactionId = MemberRegistration.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKaRegistration.CompanyRegistrationNo,VoucherNo,CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Pa Tha Ka' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN PaThaKaRegistration ON AccountTransaction.TransactionId = PaThaKaRegistration.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Business Service Agency' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BusinessServiceAgencyRegistration ON AccountTransaction.TransactionId = BusinessServiceAgencyRegistration.Id
	INNER JOIN PaThaKa ON BusinessServiceAgencyRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Duty Free Shop' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN DutyFreeShopRegistration ON AccountTransaction.TransactionId = DutyFreeShopRegistration.Id
	INNER JOIN PaThaKa ON DutyFreeShopRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Re-Export' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ReExportRegistration ON AccountTransaction.TransactionId = ReExportRegistration.Id
	INNER JOIN PaThaKa ON ReExportRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,RegistrationType FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN SaleCenterRegistration ON AccountTransaction.TransactionId = SaleCenterRegistration.Id
	INNER JOIN PaThaKa ON SaleCenterRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,RegistrationType FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ShowRoomRegistration ON AccountTransaction.TransactionId = ShowRoomRegistration.Id
	INNER JOIN PaThaKa ON ShowRoomRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,RegistrationType FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN WholeSaleRetailRegistration ON AccountTransaction.TransactionId = WholeSaleRetailRegistration.Id
	INNER JOIN PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Wine Imporation' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN WineImportationRegistration ON AccountTransaction.TransactionId = WineImportationRegistration.Id
	INNER JOIN PaThaKa ON WineImportationRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Export Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ExportLicence ON AccountTransaction.TransactionId = ExportLicence.Id
	INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Import Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ImportLicence ON AccountTransaction.TransactionId = ImportLicence.Id and AccountTransaction.TotalAmount<>3000 and VoucherNo is not null
	INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	and AccountTransaction.TotalAmount<>3000 
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Import Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ImportLicence ON AccountTransaction.TransactionId = ImportLicence.Id and AccountTransaction.TotalAmount=3000  and VoucherNo is not null 
	INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	and AccountTransaction.TotalAmount=3000
	--MPU Report Error
	Union All
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Import Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN DeleteData ON AccountTransaction.TransactionId = DeleteData.Id and AccountTransaction.TotalAmount<>3000 and VoucherNo is not null
	INNER JOIN PaThaKa ON DeleteData.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	and AccountTransaction.TotalAmount<>3000 
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Import Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN DeleteData ON AccountTransaction.TransactionId = DeleteData.Id and AccountTransaction.TotalAmount=3000  and VoucherNo is not null 
	INNER JOIN PaThaKa ON DeleteData.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	and AccountTransaction.TotalAmount=3000
	--
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Export Permit' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ExportPermit ON AccountTransaction.TransactionId = ExportPermit.Id
	INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Import Permit' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ImportPermit ON AccountTransaction.TransactionId = ImportPermit.Id AND AccountTransaction.TotalAmount=3000
	INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,0 SakhanId,'NPT' LocationCode,'Import Permit' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ImportPermit ON AccountTransaction.TransactionId = ImportPermit.Id AND AccountTransaction.TotalAmount<>3000
	INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Export Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderExportLicence ON AccountTransaction.TransactionId = BorderExportLicence.Id
	INNER JOIN Sakhan ON BorderExportLicence.SakhanId = Sakhan.Id
	INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND CardType='Pa Tha Ka'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,IndividualTrading.TINNo CompanyRegistrationNo,VoucherNo,IndividualTrading.Name CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Export Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderExportLicence ON AccountTransaction.TransactionId = BorderExportLicence.Id
	INNER JOIN Sakhan ON BorderExportLicence.SakhanId = Sakhan.Id
	INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
	WHERE IsPayment=1 AND CardType='Individual Trading'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Import Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderImportLicence ON AccountTransaction.TransactionId = BorderImportLicence.Id and AccountTransaction.TotalAmount=3000
	INNER JOIN Sakhan ON BorderImportLicence.SakhanId = Sakhan.Id
	INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND CardType='Pa Tha Ka'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,IndividualTrading.TINNo CompanyRegistrationNo,VoucherNo,IndividualTrading.Name CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Import Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderImportLicence ON AccountTransaction.TransactionId = BorderImportLicence.Id and AccountTransaction.TotalAmount=3000
	INNER JOIN Sakhan ON BorderImportLicence.SakhanId = Sakhan.Id
	INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
	WHERE IsPayment=1 AND CardType='Individual Trading'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Import Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderImportLicence ON AccountTransaction.TransactionId = BorderImportLicence.Id and AccountTransaction.TotalAmount<>3000
	INNER JOIN Sakhan ON BorderImportLicence.SakhanId = Sakhan.Id
	INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND CardType='Pa Tha Ka'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,IndividualTrading.TINNo CompanyRegistrationNo,VoucherNo,IndividualTrading.Name CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Import Licence' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderImportLicence ON AccountTransaction.TransactionId = BorderImportLicence.Id and AccountTransaction.TotalAmount<>3000
	INNER JOIN Sakhan ON BorderImportLicence.SakhanId = Sakhan.Id
	INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
	WHERE IsPayment=1 AND CardType='Individual Trading'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Export Permit' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderExportPermit ON AccountTransaction.TransactionId = BorderExportPermit.Id
	INNER JOIN Sakhan ON BorderExportPermit.SakhanId = Sakhan.Id
	INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Import Permit' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderImportPermit ON AccountTransaction.TransactionId = BorderImportPermit.Id AND AccountTransaction.TotalAmount<>3000
	INNER JOIN Sakhan ON BorderImportPermit.SakhanId = Sakhan.Id
	INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT AccountTransaction.Id,VoucherDate,AccountTransaction.TransactionId,AccountTransaction.TotalAmount,PaymentDate,PaThaKa.CompanyRegistrationNo,VoucherNo,PaThaKa.CompanyName, AccountTitle.Description TransactionTitle,
	Amount,AccountTitle.Code AccountTitleCode,AccountTitle.SortOrder,Sakhan.Id SakhanId,Sakhan.Code LocationCode,'Border Import Permit' FormType
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderImportPermit ON AccountTransaction.TransactionId = BorderImportPermit.Id AND AccountTransaction.TotalAmount=3000
	INNER JOIN Sakhan ON BorderImportPermit.SakhanId = Sakhan.Id
	INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate))tmp
	Left Join MPUPaymentTransaction on tmp.TransactionId=MPUPaymentTransaction.TransactionId 
	and tmp.TotalAmount=MPUPaymentTransaction.MOCAmount
	--inner Join MPUPaymentTransaction on MPUPaymentTransaction.TransactionId=tmp.TransactionId 
	--AND MPUPaymentTransaction.ResponseCode='00'
	WHERE tmp.FormType = (CASE WHEN @FormType='' THEN tmp.FormType ELSE @FormType END)
	--AND tmp.SakhanId = (CASE WHEN @SakhanId='' THEN tmp.SakhanId ELSE @SakhanId END)
	--AND dbo.MPUPaymentTransaction.PaymentType=@PaymentType
	ORDER BY tmp.PaymentDate,tmp.SortOrder
END

GO

/* dbo.sp_NewReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_ExportLicenceAmendReport ''
CREATE PROCEDURE [dbo].[sp_NewReport] 
	-- Add the parameters for the stored procedure here
	@FormType nvarchar(50),
	@FromDate datetime,
	@ToDate datetime,
	@ExportImportSectionId int,
	@CompanyRegistrationNo nvarchar(10),
	@SakhanId int,
	@auto nvarchar(50)
	--@quota nvarchar(50)
AS
BEGIN
	IF(@FormType='Export Licence')
	BEGIN
		SELECT ExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,ExportLicence.LastDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Currency,
		(SELECT ISNULL(SUM(ExportLicenceItem.Amount),0) FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Amount , ExportLicence.auto
		FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType='New' AND ExportLicence.Status='Approved'
		AND (ExportLicence.CreatedDate>=@FromDate AND ExportLicence.CreatedDate<=@ToDate)
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND ExportLicence.auto=(CASE WHEN @auto='' then ExportLicence.auto ELSE @auto END)
	END
	ELSE IF(@FormType='Import Licence')
	BEGIN
		SELECT ImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,ImportLicence.LastDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Currency,
		(SELECT ISNULL(SUM(ImportLicenceItem.Amount),0) FROM ImportLicenceItem
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Amount, ImportLicence.auto , ImportLicence.quota ,ImportLicence.CommodityType
		FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType='New' AND ImportLicence.Status='Approved'
		AND (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		--AND ImportLicence.auto=(CASE WHEN @auto='' then ImportLicence.auto ELSE @auto END)
		--AND ImportLicence.quota=(CASE WHEN quota='' then ImportLicence.quota ELSE @quota END)
	END
	ELSE IF(@FormType='Export Permit')
	BEGIN
		SELECT ExportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,CONVERT(varchar,ExportPermit.LastDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ExportPermitItem
		INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Currency,
		(SELECT  ISNULL(SUM(ExportPermitItem.Amount),0) FROM ExportPermitItem
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Amount
		FROM ExportPermit
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType='New' AND ExportPermit.Status='Approved'
		AND (ExportPermit.CreatedDate>=@FromDate AND ExportPermit.CreatedDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Import Permit')
	BEGIN
		SELECT ImportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,CONVERT(varchar,ImportPermit.LastDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ImportPermitItem
		INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Currency,
		(SELECT  ISNULL(SUM(ImportPermitItem.Amount),0) FROM ImportPermitItem
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Amount
		FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType='New' AND ImportPermit.Status='Approved'
		AND (ImportPermit.CreatedDate>=@FromDate AND ImportPermit.CreatedDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Border Export Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(SELECT  ISNULL(SUM(BorderExportLicenceItem.Amount),0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Amount, BorderExportLicence.auto,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportLicence
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND CardType='Pa Tha Ka'
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		AND BorderExportLicence.auto=(CASE WHEN @auto='' then BorderExportLicence.auto ELSE @auto END)
		UNION ALL
		SELECT BorderExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
		IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(SELECT  ISNULL(SUM(BorderExportLicenceItem.Amount),0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Amount, BorderExportLicence.auto,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND CardType='Individual Trading'
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		AND BorderExportLicence.auto=(CASE WHEN @auto='' then BorderExportLicence.auto ELSE @auto END)
		)tmp
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Import Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT ISNULL(SUM(BorderImportLicenceItem.Amount),0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount, BorderImportLicence.auto, BorderImportLicence.quota,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND CardType='Pa Tha Ka'
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		--AND BorderImportLicence.auto=(CASE WHEN @auto='' then BorderImportLicence.auto ELSE @auto END)
		--AND BorderImportLicence.quota=(CASE WHEN @quota='' then BorderImportLicence.quota ELSE @quota END)
		UNION ALL
		SELECT BorderImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
		IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT  ISNULL(SUM(BorderImportLicenceItem.Amount),0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount, BorderImportLicence.auto, BorderImportLicence.quota,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND CardType='Individual Trading'
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
			--AND BorderImportLicence.auto=(CASE WHEN @auto='' then BorderImportLicence.auto ELSE @auto END)
			--AND BorderImportLicence.quota=(CASE WHEN @quota='' then BorderImportLicence.quota ELSE @quota END)
		)tmp
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Export Permit')
	BEGIN
		SELECT BorderExportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,CONVERT(varchar,BorderExportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportPermitItem
		INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Currency,
		(SELECT ISNULL(SUM(BorderExportPermitItem.Amount),0) FROM BorderExportPermitItem
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportPermit
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		WHERE ApplyType='New' AND BorderExportPermit.Status='Approved'
		AND (BorderExportPermit.CreatedDate>=@FromDate AND BorderExportPermit.CreatedDate<=@ToDate)
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END)
	END
	ELSE IF(@FormType='Border Import Permit')
	BEGIN
		SELECT BorderImportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,CONVERT(varchar,BorderImportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportPermitItem
		INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Currency,
		(SELECT ISNULL(SUM(BorderImportPermitItem.Amount),0) FROM BorderImportPermitItem
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType='New' AND BorderImportPermit.Status='Approved'
		AND (BorderImportPermit.CreatedDate>=@FromDate AND BorderImportPermit.CreatedDate<=@ToDate)
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
	END

	
END

GO

/* dbo.sp_NewReport_old */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_ExportLicenceNewReport ''
CREATE PROCEDURE [dbo].[sp_NewReport_old] 
	-- Add the parameters for the stored procedure here
	@FormType nvarchar(50),
	@FromDate datetime,
	@ToDate datetime,
	@ExportImportSectionId int,
	 --@ExportImportMethodId int,  
	@CompanyRegistrationNo nvarchar(10),
	@SakhanId int,
	@auto nvarchar(50)
AS
BEGIN
	IF(@FormType='Export Licence')
	BEGIN
		SELECT ExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,ExportLicence.LastDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(ExportLicenceItem.Amount,0) FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Amount,
		ExportLicence.auto
		--method.Name MethodName
		FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		INNER JOIN ExportImportMethod method ON method.Id  = ExportLicence.ExportImportMethodId 
		WHERE ApplyType='New' AND ExportLicence.Status='Approved'
		AND (ExportLicence.CreatedDate>=@FromDate AND ExportLicence.CreatedDate<=@ToDate)
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND ExportLicence.auto=(CASE WHEN @auto='' then ExportLicence.auto ELSE @auto END)
		--AND ExportLicence.ExportImportMethodId=(CASE WHEN @ExportImportMethodId=0 then ExportLicence.ExportImportMethodId ELSE @ExportImportMethodId END)
		--ANd ExportLicence.Remark like (CASE WHEN @ExportImportMethodId=3 then  N'%ဤလိုင်စင်သည် Tradenet 2.0 စနစ်မှ CMP  စနစ်ဖြင့် ခွင့်ပြုထားသော လိုင်စင်ဖြစ်ပါသည်%' ELSE N'%%'END)
	END
	ELSE IF(@FormType='Import Licence')
	BEGIN
		SELECT ImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,ImportLicence.LastDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Currency,
		(SELECT top 1 ISNULL(ImportLicenceItem.Amount,0) FROM ImportLicenceItem
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Amount
		FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE ApplyType='New' AND ImportLicence.Status='Approved'
		AND (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Export Permit')
	BEGIN
		SELECT ExportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,CONVERT(varchar,ExportPermit.LastDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ExportPermitItem
		INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Currency,
		(SELECT top 1 ISNULL(ExportPermitItem.Amount,0) FROM ExportPermitItem
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Amount
		FROM ExportPermit
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType='New' AND ExportPermit.Status='Approved'
		AND (ExportPermit.CreatedDate>=@FromDate AND ExportPermit.CreatedDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Import Permit')
	BEGIN
		SELECT ImportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,CONVERT(varchar,ImportPermit.LastDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM ImportPermitItem
		INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Currency,
		(SELECT top 1 ISNULL(ImportPermitItem.Amount,0) FROM ImportPermitItem
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Amount
		FROM ImportPermit
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		WHERE ApplyType='New' AND ImportPermit.Status='Approved'
		AND (ImportPermit.CreatedDate>=@FromDate AND ImportPermit.CreatedDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	END
	ELSE IF(@FormType='Border Export Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(BorderExportLicenceItem.Amount,0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Amount,BorderExportLicence.auto,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportLicence
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND CardType='Pa Tha Ka'
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		--AND BorderExportLicence.auto=(CASE WHEN @auto='' then BorderExportLicence.auto ELSE @auto END)
		UNION ALL
		SELECT BorderExportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sDate,
		IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(SELECT top 1 ISNULL(BorderExportLicenceItem.Amount,0) FROM BorderExportLicenceItem
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Amount,BorderExportLicence.auto,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportLicence
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='New' AND BorderExportLicence.Status='Approved' AND CardType='Individual Trading'
		AND (BorderExportLicence.CreatedDate>=@FromDate AND BorderExportLicence.CreatedDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
			--AND BorderExportLicence.auto=(CASE WHEN @auto='' then BorderExportLicence.auto ELSE @auto END)
		)tmp
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Import Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT top 1 ISNULL(BorderImportLicenceItem.Amount,0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND CardType='Pa Tha Ka'
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderImportLicence.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sDate,
		IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT top 1 ISNULL(BorderImportLicenceItem.Amount,0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportLicence
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId = IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		WHERE ApplyType='New' AND BorderImportLicence.Status='Approved' AND CardType='Individual Trading'
		AND (BorderImportLicence.CreatedDate>=@FromDate AND BorderImportLicence.CreatedDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		)tmp
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Export Permit')
	BEGIN
		SELECT BorderExportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,CONVERT(varchar,BorderExportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderExportPermitItem
		INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Currency,
		(SELECT top 1 ISNULL(BorderExportPermitItem.Amount,0) FROM BorderExportPermitItem
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderExportPermit
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		WHERE ApplyType='New' AND BorderExportPermit.Status='Approved'
		AND (BorderExportPermit.CreatedDate>=@FromDate AND BorderExportPermit.CreatedDate<=@ToDate)
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END)
	END
	ELSE IF(@FormType='Border Import Permit')
	BEGIN
		SELECT BorderImportPermit.CreatedDate Date,section.Code SectionCode,section.Name SectionName,OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,CONVERT(varchar,BorderImportPermit.CreatedDate,103) sDate,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
		(SELECT top 1 currency.Code FROM BorderImportPermitItem
		INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Currency,
		(SELECT top 1 ISNULL(BorderImportPermitItem.Amount,0) FROM BorderImportPermitItem
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Amount,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName
		FROM BorderImportPermit
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		WHERE ApplyType='New' AND BorderImportPermit.Status='Approved'
		AND (BorderImportPermit.CreatedDate>=@FromDate AND BorderImportPermit.CreatedDate<=@ToDate)
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
	END

	
END

GO

/* dbo.sp_NotificationDataList */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_NotificationDataList] 
	-- Add the parameters for the stored procedure here
	
AS
BEGIN
	SELECT Id,'Pa Tha Ka' FormType,MemberId,EndDate,Email, DATEADD(DAY,-90,EndDate) as WarningDate FROM PaThaKa
	WHERE MemberId is not null
	AND (-DATEDIFF(day,EndDate,CURRENT_TIMESTAMP)<=90)
	UNION ALL
	SELECT Id,'Individual Trading' FormType,MemberId,EndDate,Email, DATEADD(DAY,-90,EndDate) as WarningDate FROM IndividualTrading
	WHERE MemberId is not null
	AND (-DATEDIFF(day,EndDate,CURRENT_TIMESTAMP)<=90)
	UNION ALL
	SELECT Id,'Member' FormType,Id as MemberId,EndDate,Email, DATEADD(DAY,-90,EndDate) as WarningDate FROM Member
	WHERE (-DATEDIFF(day,EndDate,CURRENT_TIMESTAMP)<=90)
END

GO

/* dbo.sp_OGARecommendationHistoryReport */
-- =============================================
-- Author:      Name
-- Create Date: 
-- Description: 
-- =============================================
CREATE PROCEDURE [dbo].[sp_OGARecommendationHistoryReport]
(
    -- Add the parameters for the stored procedure here
    @OGARecommendationId char(36)
)
AS
BEGIN
  SELECT * FROM
  (SELECT OGARecommendationHistory.Id,ISNULL(CONVERT(varchar,OGARecommendationHistory.CreatedDate,103),'-') sDate,ExportLicenceNo LicenceNo,Type,OGARecommendationHistory.Remark,Balance,users.FullName,users.Position,OGARecommendationHistory.CreatedDate,
  ReferenceNo,OGADepartment.EnglishName OGADepartmentName,OGASection.EnglishName OGASectionName
  FROM OGARecommendationHistory
  INNER JOIN OGARecommendation ON OGARecommendationHistory.OGARecommendationId = OGARecommendation.Id
  INNER JOIN OGADepartment ON OGARecommendation.OGADepartmentId = OGADepartment.Id
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.ID
  INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
  INNER JOIN ExportLicence ON OGARecommendationHistory.LicencePermitId = ExportLicence.Id
  WHERE OGARecommendationId=@OGARecommendationId and Type='Export Licence'
  UNION ALL
  SELECT OGARecommendationHistory.Id,ISNULL(CONVERT(varchar,OGARecommendationHistory.CreatedDate,103),'-') sDate,ImportLicenceNo LicenceNo,Type,OGARecommendationHistory.Remark,Balance,users.FullName,users.Position,OGARecommendationHistory.CreatedDate,
  ReferenceNo,OGADepartment.EnglishName OGADepartmentName,OGASection.EnglishName OGASectionName
  FROM OGARecommendationHistory
  INNER JOIN OGARecommendation ON OGARecommendationHistory.OGARecommendationId = OGARecommendation.Id
  INNER JOIN OGADepartment ON OGARecommendation.OGADepartmentId = OGADepartment.Id
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.ID
  INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
  INNER JOIN ImportLicence ON OGARecommendationHistory.LicencePermitId = ImportLicence.Id
  WHERE OGARecommendationId=@OGARecommendationId and Type='Import Licence'
  UNION ALL
  SELECT OGARecommendationHistory.Id,ISNULL(CONVERT(varchar,OGARecommendationHistory.CreatedDate,103),'-') sDate,ExportPermitNo LicenceNo,Type,OGARecommendationHistory.Remark,Balance,users.FullName,users.Position,OGARecommendationHistory.CreatedDate,
  ReferenceNo,OGADepartment.EnglishName OGADepartmentName,OGASection.EnglishName OGASectionName
  FROM OGARecommendationHistory
  INNER JOIN OGARecommendation ON OGARecommendationHistory.OGARecommendationId = OGARecommendation.Id
  INNER JOIN OGADepartment ON OGARecommendation.OGADepartmentId = OGADepartment.Id
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.ID
  INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
  INNER JOIN ExportPermit ON OGARecommendationHistory.LicencePermitId = ExportPermit.Id
  WHERE OGARecommendationId=@OGARecommendationId and Type='Export Permit'
  UNION ALL
  SELECT OGARecommendationHistory.Id,ISNULL(CONVERT(varchar,OGARecommendationHistory.CreatedDate,103),'-') sDate,ImportPermitNo LicenceNo,Type,OGARecommendationHistory.Remark,Balance,users.FullName,users.Position,OGARecommendationHistory.CreatedDate,
  ReferenceNo,OGADepartment.EnglishName OGADepartmentName,OGASection.EnglishName OGASectionName
  FROM OGARecommendationHistory
  INNER JOIN OGARecommendation ON OGARecommendationHistory.OGARecommendationId = OGARecommendation.Id
  INNER JOIN OGADepartment ON OGARecommendation.OGADepartmentId = OGADepartment.Id
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.ID
  INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
  INNER JOIN ImportPermit ON OGARecommendationHistory.LicencePermitId = ImportPermit.Id
  WHERE OGARecommendationId=@OGARecommendationId and Type='Import Permit'
  UNION ALL
  SELECT OGARecommendationHistory.Id,ISNULL(CONVERT(varchar,OGARecommendationHistory.CreatedDate,103),'-') sDate,ExportLicenceNo LicenceNo,Type,OGARecommendationHistory.Remark,Balance,users.FullName,users.Position,OGARecommendationHistory.CreatedDate,
  ReferenceNo,OGADepartment.EnglishName OGADepartmentName,OGASection.EnglishName OGASectionName
  FROM OGARecommendationHistory
  INNER JOIN OGARecommendation ON OGARecommendationHistory.OGARecommendationId = OGARecommendation.Id
  INNER JOIN OGADepartment ON OGARecommendation.OGADepartmentId = OGADepartment.Id
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.ID
  INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
  INNER JOIN BorderExportLicence ON OGARecommendationHistory.LicencePermitId = BorderExportLicence.Id
  WHERE OGARecommendationId=@OGARecommendationId and Type='Border Export Licence'
  UNION ALL
  SELECT OGARecommendationHistory.Id,ISNULL(CONVERT(varchar,OGARecommendationHistory.CreatedDate,103),'-') sDate,ImportLicenceNo LicenceNo,Type,OGARecommendationHistory.Remark,Balance,users.FullName,users.Position,OGARecommendationHistory.CreatedDate,
  ReferenceNo,OGADepartment.EnglishName OGADepartmentName,OGASection.EnglishName OGASectionName
  FROM OGARecommendationHistory
  INNER JOIN OGARecommendation ON OGARecommendationHistory.OGARecommendationId = OGARecommendation.Id
  INNER JOIN OGADepartment ON OGARecommendation.OGADepartmentId = OGADepartment.Id
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.ID
  INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
  INNER JOIN BorderImportLicence ON OGARecommendationHistory.LicencePermitId = BorderImportLicence.Id
  WHERE OGARecommendationId=@OGARecommendationId and Type='Border Import Licence'
  UNION ALL
  SELECT OGARecommendationHistory.Id,ISNULL(CONVERT(varchar,OGARecommendationHistory.CreatedDate,103),'-') sDate,ExportPermitNo LicenceNo,Type,OGARecommendationHistory.Remark,Balance,users.FullName,users.Position,OGARecommendationHistory.CreatedDate,
  ReferenceNo,OGADepartment.EnglishName OGADepartmentName,OGASection.EnglishName OGASectionName
  FROM OGARecommendationHistory
  INNER JOIN OGARecommendation ON OGARecommendationHistory.OGARecommendationId = OGARecommendation.Id
  INNER JOIN OGADepartment ON OGARecommendation.OGADepartmentId = OGADepartment.Id
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.ID
  INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
  INNER JOIN BorderExportPermit ON OGARecommendationHistory.LicencePermitId = BorderExportPermit.Id
  WHERE OGARecommendationId=@OGARecommendationId and Type='Border Export Permit'
  UNION ALL
  SELECT OGARecommendationHistory.Id,ISNULL(CONVERT(varchar,OGARecommendationHistory.CreatedDate,103),'-') sDate,ImportPermitNo LicenceNo,Type,OGARecommendationHistory.Remark,Balance,users.FullName,users.Position,OGARecommendationHistory.CreatedDate,
  ReferenceNo,OGADepartment.EnglishName OGADepartmentName,OGASection.EnglishName OGASectionName
  FROM OGARecommendationHistory
  INNER JOIN OGARecommendation ON OGARecommendationHistory.OGARecommendationId = OGARecommendation.Id
  INNER JOIN OGADepartment ON OGARecommendation.OGADepartmentId = OGADepartment.Id
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.ID
  INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
  INNER JOIN BorderImportPermit ON OGARecommendationHistory.LicencePermitId = BorderImportPermit.Id
  WHERE OGARecommendationId=@OGARecommendationId and Type='Border Import Permit')tmp
  ORDER BY tmp.CreatedDate
  
END

GO

/* dbo.sp_OGARecommendationListReport */
-- =============================================
-- Author:      Name
-- Create Date: 
-- Description: 
-- =============================================
CREATE PROCEDURE [dbo].[sp_OGARecommendationListReport]
(
    -- Add the parameters for the stored procedure here
    @FromDate datetime,
    @ToDate datetime,
	@OGADepartmentId int,
	@OGASectionId int,
	@CompanyRegistrationNo nvarchar(50),
	@ReferenceNo nvarchar(200)
)
AS
BEGIN
   SELECT OGARecommendation.Id,ISNULL(CONVERT(varchar,OGARecommendation.CreatedDate,103),'-') sDate,CompanyRegistrationNo,OGARecommendation.OGADepartmentId,OGARecommendation.OGASectionId,
	OGADepartment.EnglishName OGADepartmentName,OGASection.EnglishName OGAsectionName,ReferenceNo,FromDate,ToDate,
	ISNULL(CONVERT(varchar,FromDate,103),'-') sFromDate,ISNULL(CONVERT(varchar,ToDate,103),'-') sToDate,Allowance,
	CASE WHEN IsClosed=1 THEN 'Yes' else 'No' END Terminate,CASE WHEN IsUsedOnce=1 THEN 'Yes' else 'No' END IsUsedOnce
	FROM OGARecommendation
	INNER JOIN PaThaKa ON OGARecommendation.PaThaKaId = PaThaKa.Id
	INNER JOIN OGADepartment ON OGARecommendation.OGADepartmentId = OGADepartment.Id
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.ID
	WHERE (OGARecommendation.CreatedDate>=@FromDate AND OGARecommendation.CreatedDate<=@ToDate)
	AND OGARecommendation.OGADepartmentId=(CASE WHEN @OGADepartmentId=0 THEN OGARecommendation.OGADepartmentId ELSE @OGADepartmentId END)
	AND OGARecommendation.OGASectionId=(CASE WHEN @OGASectionId=0 THEN OGARecommendation.OGASectionId ELSE @OGASectionId END)
	AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' THEN PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
	AND OGARecommendation.ReferenceNo=(CASE WHEN @ReferenceNo='' THEN OGARecommendation.ReferenceNo ELSE @ReferenceNo END)
	ORDER BY OGARecommendation.CreatedDate,OGADepartment.SortOrder,OGASection.SortOrder
END

GO

/* dbo.sp_OGARecommendationReport */
-- =============================================
-- Author:      Name
-- Create Date: 
-- Description: 
-- =============================================
CREATE PROCEDURE [dbo].[sp_OGARecommendationReport]
(
    -- Add the parameters for the stored procedure here
    @OGADepartmentId int,
	@OGASectionId int,
	@FromDate datetime,
	@ToDate datetime,
	@ReferenceNo nvarchar(200)
)
AS
BEGIN
    SELECT * FROM
	(SELECT OGARecommendation.Id,OGASectionId,OGASection.EnglishName OGASectionName,ReferenceNo,SarNo,SarDate,
	PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,
	Allowance,ExportLicence.ExportLicenceNo LicenceNo,Type FormType,OGARecommendationHistory.Remark,Balance,Users.FullName,Users.Position,
	CONVERT(varchar,OGARecommendationHistory.CreatedDate,103) sDate
	FROM OGARecommendation
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.Id
	INNER JOIN PaThaKa ON OGARecommendation.PaThaKaId = PaThaKa.Id
	INNER JOIN OGARecommendationHistory ON OGARecommendation.Id = OGARecommendationHistory.OGARecommendationId
	INNER JOIN ExportLicence ON OGARecommendationHistory.LicencePermitId = ExportLicence.Id
	INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
	WHERE (SarDate>=@FromDate AND SarDate<=@ToDate)
	AND OGARecommendation.OGADepartmentId=@OGADepartmentId
	AND OGARecommendation.OGASectionId=(CASE WHEN @OGASectionId=0 THEN OGARecommendation.OGASectionId ELSE @OGASectionId END)
	AND OGARecommendation.ReferenceNo=(CASE WHEN @ReferenceNo=0 THEN OGARecommendation.ReferenceNo ELSE @ReferenceNo END)
	UNION ALL
	SELECT OGARecommendation.Id,OGASectionId,OGASection.EnglishName OGASectionName,ReferenceNo,SarNo,SarDate,
	PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,
	Allowance,ImportLicence.ImportLicenceNo LicenceNo,Type FormType,OGARecommendationHistory.Remark,Balance,Users.FullName,Users.Position,
	CONVERT(varchar,OGARecommendationHistory.CreatedDate,103) sDate
	FROM OGARecommendation
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.Id
	INNER JOIN PaThaKa ON OGARecommendation.PaThaKaId = PaThaKa.Id
	INNER JOIN OGARecommendationHistory ON OGARecommendation.Id = OGARecommendationHistory.OGARecommendationId
	INNER JOIN ImportLicence ON OGARecommendationHistory.LicencePermitId = ImportLicence.Id
	INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
	WHERE (SarDate>=@FromDate AND SarDate<=@ToDate)
	AND OGARecommendation.OGADepartmentId=@OGADepartmentId
	AND OGARecommendation.OGASectionId=(CASE WHEN @OGASectionId=0 THEN OGARecommendation.OGASectionId ELSE @OGASectionId END)
	AND OGARecommendation.ReferenceNo=(CASE WHEN @ReferenceNo=0 THEN OGARecommendation.ReferenceNo ELSE @ReferenceNo END)
	UNION ALL
	SELECT OGARecommendation.Id,OGASectionId,OGASection.EnglishName OGASectionName,ReferenceNo,SarNo,SarDate,
	PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,
	Allowance,ExportPermit.ExportPermitNo LicenceNo,Type FormType,OGARecommendationHistory.Remark,Balance,Users.FullName,Users.Position,
	CONVERT(varchar,OGARecommendationHistory.CreatedDate,103) sDate
	FROM OGARecommendation
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.Id
	INNER JOIN PaThaKa ON OGARecommendation.PaThaKaId = PaThaKa.Id
	INNER JOIN OGARecommendationHistory ON OGARecommendation.Id = OGARecommendationHistory.OGARecommendationId
	INNER JOIN ExportPermit ON OGARecommendationHistory.LicencePermitId = ExportPermit.Id
	INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
	WHERE (SarDate>=@FromDate AND SarDate<=@ToDate)
	AND OGARecommendation.OGADepartmentId=@OGADepartmentId
	AND OGARecommendation.OGASectionId=(CASE WHEN @OGASectionId=0 THEN OGARecommendation.OGASectionId ELSE @OGASectionId END)
	AND OGARecommendation.ReferenceNo=(CASE WHEN @ReferenceNo=0 THEN OGARecommendation.ReferenceNo ELSE @ReferenceNo END)
	UNION ALL
	SELECT OGARecommendation.Id,OGASectionId,OGASection.EnglishName OGASectionName,ReferenceNo,SarNo,SarDate,
	PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,
	Allowance,ImportPermit.ImportPermitNo LicenceNo,Type FormType,OGARecommendationHistory.Remark,Balance,Users.FullName,Users.Position,
	CONVERT(varchar,OGARecommendationHistory.CreatedDate,103) sDate
	FROM OGARecommendation
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.Id
	INNER JOIN PaThaKa ON OGARecommendation.PaThaKaId = PaThaKa.Id
	INNER JOIN OGARecommendationHistory ON OGARecommendation.Id = OGARecommendationHistory.OGARecommendationId
	INNER JOIN ImportPermit ON OGARecommendationHistory.LicencePermitId = ImportPermit.Id
	INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
	WHERE (SarDate>=@FromDate AND SarDate<=@ToDate)
	AND OGARecommendation.OGADepartmentId=@OGADepartmentId
	AND OGARecommendation.OGASectionId=(CASE WHEN @OGASectionId=0 THEN OGARecommendation.OGASectionId ELSE @OGASectionId END)
	AND OGARecommendation.ReferenceNo=(CASE WHEN @ReferenceNo=0 THEN OGARecommendation.ReferenceNo ELSE @ReferenceNo END)
	UNION ALL
	SELECT OGARecommendation.Id,OGASectionId,OGASection.EnglishName OGASectionName,ReferenceNo,SarNo,SarDate,
	PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,
	Allowance,BorderExportLicence.ExportLicenceNo LicenceNo,Type FormType,OGARecommendationHistory.Remark,Balance,Users.FullName,Users.Position,
	CONVERT(varchar,OGARecommendationHistory.CreatedDate,103) sDate
	FROM OGARecommendation
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.Id
	INNER JOIN PaThaKa ON OGARecommendation.PaThaKaId = PaThaKa.Id
	INNER JOIN OGARecommendationHistory ON OGARecommendation.Id = OGARecommendationHistory.OGARecommendationId
	INNER JOIN BorderExportLicence ON OGARecommendationHistory.LicencePermitId = BorderExportLicence.Id
	INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
	WHERE (SarDate>=@FromDate AND SarDate<=@ToDate)
	AND OGARecommendation.OGADepartmentId=@OGADepartmentId
	AND OGARecommendation.OGASectionId=(CASE WHEN @OGASectionId=0 THEN OGARecommendation.OGASectionId ELSE @OGASectionId END)
	AND OGARecommendation.ReferenceNo=(CASE WHEN @ReferenceNo=0 THEN OGARecommendation.ReferenceNo ELSE @ReferenceNo END)
	UNION ALL
	SELECT OGARecommendation.Id,OGASectionId,OGASection.EnglishName OGASectionName,ReferenceNo,SarNo,SarDate,
	PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,
	Allowance,BorderImportLicence.ImportLicenceNo LicenceNo,Type FormType,OGARecommendationHistory.Remark,Balance,Users.FullName,Users.Position,
	CONVERT(varchar,OGARecommendationHistory.CreatedDate,103) sDate
	FROM OGARecommendation
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.Id
	INNER JOIN PaThaKa ON OGARecommendation.PaThaKaId = PaThaKa.Id
	INNER JOIN OGARecommendationHistory ON OGARecommendation.Id = OGARecommendationHistory.OGARecommendationId
	INNER JOIN BorderImportLicence ON OGARecommendationHistory.LicencePermitId = BorderImportLicence.Id
	INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
	WHERE (SarDate>=@FromDate AND SarDate<=@ToDate)
	AND OGARecommendation.OGADepartmentId=@OGADepartmentId
	AND OGARecommendation.OGASectionId=(CASE WHEN @OGASectionId=0 THEN OGARecommendation.OGASectionId ELSE @OGASectionId END)
	AND OGARecommendation.ReferenceNo=(CASE WHEN @ReferenceNo=0 THEN OGARecommendation.ReferenceNo ELSE @ReferenceNo END)
	UNION ALL
	SELECT OGARecommendation.Id,OGASectionId,OGASection.EnglishName OGASectionName,ReferenceNo,SarNo,SarDate,
	PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,
	Allowance,BorderExportPermit.ExportPermitNo LicenceNo,Type FormType,OGARecommendationHistory.Remark,Balance,Users.FullName,Users.Position,
	CONVERT(varchar,OGARecommendationHistory.CreatedDate,103) sDate
	FROM OGARecommendation
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.Id
	INNER JOIN PaThaKa ON OGARecommendation.PaThaKaId = PaThaKa.Id
	INNER JOIN OGARecommendationHistory ON OGARecommendation.Id = OGARecommendationHistory.OGARecommendationId
	INNER JOIN BorderExportPermit ON OGARecommendationHistory.LicencePermitId = BorderExportPermit.Id
	INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
	WHERE (SarDate>=@FromDate AND SarDate<=@ToDate)
	AND OGARecommendation.OGADepartmentId=@OGADepartmentId
	AND OGARecommendation.OGASectionId=(CASE WHEN @OGASectionId=0 THEN OGARecommendation.OGASectionId ELSE @OGASectionId END)
	AND OGARecommendation.ReferenceNo=(CASE WHEN @ReferenceNo=0 THEN OGARecommendation.ReferenceNo ELSE @ReferenceNo END)
	UNION ALL
	SELECT OGARecommendation.Id,OGASectionId,OGASection.EnglishName OGASectionName,ReferenceNo,SarNo,SarDate,
	PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,
	Allowance,BorderImportPermit.ImportPermitNo LicenceNo,Type FormType,OGARecommendationHistory.Remark,Balance,Users.FullName,Users.Position,
	CONVERT(varchar,OGARecommendationHistory.CreatedDate,103) sDate
	FROM OGARecommendation
	INNER JOIN OGASection ON OGARecommendation.OGASectionId = OGASection.Id
	INNER JOIN PaThaKa ON OGARecommendation.PaThaKaId = PaThaKa.Id
	INNER JOIN OGARecommendationHistory ON OGARecommendation.Id = OGARecommendationHistory.OGARecommendationId
	INNER JOIN BorderImportPermit ON OGARecommendationHistory.LicencePermitId = BorderImportPermit.Id
	INNER JOIN Users ON OGARecommendationHistory.MOCUserId = Users.Id
	WHERE (SarDate>=@FromDate AND SarDate<=@ToDate)
	AND OGARecommendation.OGADepartmentId=@OGADepartmentId
	AND OGARecommendation.OGASectionId=(CASE WHEN @OGASectionId=0 THEN OGARecommendation.OGASectionId ELSE @OGASectionId END)
	AND OGARecommendation.ReferenceNo=(CASE WHEN @ReferenceNo=0 THEN OGARecommendation.ReferenceNo ELSE @ReferenceNo END))tmp
	ORDER BY tmp.SarDate
END

GO

/* dbo.sp_OnlineFeesReport */
-- =============================================
-- Author:      Name
-- Create Date: 
-- Description: 
-- =============================================
--exec sp_OnlineFeesReport '2020-11-01 00:00','2020-11-03','',0
CREATE PROCEDURE [dbo].[sp_OnlineFeesReport]
(
    -- Add the parameters for the stored procedure here
    @FromDate datetime,
	@ToDate datetime,
	@FormType nvarchar(50),
	@SakhanId int
)
AS
BEGIN
    SELECT * FROM
	(
	SELECT 0 SakhanId,VoucherDate,MemberRegistration.ApplicationNo CompanyRegistrationNo,'' CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN MemberRegistration ON AccountTransaction.TransactionId = MemberRegistration.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT 0 SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+BusinessServiceAgencyRegistration.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BusinessServiceAgencyRegistration ON AccountTransaction.TransactionId = BusinessServiceAgencyRegistration.Id
	INNER JOIN PaThaKa ON BusinessServiceAgencyRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT 0 SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+DutyFreeShopRegistration.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN DutyFreeShopRegistration ON AccountTransaction.TransactionId = DutyFreeShopRegistration.Id
	INNER JOIN PaThaKa ON DutyFreeShopRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT 0 SakhanId,VoucherDate,PaThaKaRegistration.CompanyRegistrationNo+'@'+PaThaKaRegistration.ApplicationNo CompanyRegistrationNo,PaThaKaRegistration.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN PaThaKaRegistration ON AccountTransaction.TransactionId = PaThaKaRegistration.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT 0 SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+ReExportRegistration.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ReExportRegistration ON AccountTransaction.TransactionId = ReExportRegistration.Id
	INNER JOIN PaThaKa ON ReExportRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT 0 SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+SaleCenterRegistration.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN SaleCenterRegistration ON AccountTransaction.TransactionId = SaleCenterRegistration.Id
	INNER JOIN PaThaKa ON SaleCenterRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT 0 SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+ShowRoomRegistration.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ShowRoomRegistration ON AccountTransaction.TransactionId = ShowRoomRegistration.Id
	INNER JOIN PaThaKa ON ShowRoomRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT 0 SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+WholeSaleRetailRegistration.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN WholeSaleRetailRegistration ON AccountTransaction.TransactionId = WholeSaleRetailRegistration.Id
	INNER JOIN PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT 0 SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+WineImportationRegistration.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN WineImportationRegistration ON AccountTransaction.TransactionId = WineImportationRegistration.Id
	INNER JOIN PaThaKa ON WineImportationRegistration.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+BorderExportLicence.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderExportLicence ON AccountTransaction.TransactionId = BorderExportLicence.Id
	INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+BorderExportPermit.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderExportPermit ON AccountTransaction.TransactionId = BorderExportPermit.Id
	INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+BorderImportLicence.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderImportLicence ON AccountTransaction.TransactionId = BorderImportLicence.Id
	INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+BorderImportPermit.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN BorderImportPermit ON AccountTransaction.TransactionId = BorderImportPermit.Id
	INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT 0 SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+ExportLicence.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ExportLicence ON AccountTransaction.TransactionId = ExportLicence.Id
	INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT 0 SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+ExportPermit.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ExportPermit ON AccountTransaction.TransactionId = ExportPermit.Id
	INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT 0 SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+ImportLicence.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ImportLicence ON AccountTransaction.TransactionId = ImportLicence.Id
	INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate)
	UNION ALL
	SELECT 0 SakhanId,VoucherDate,PaThaKa.CompanyRegistrationNo+'@'+ImportPermit.ApplicationNo CompanyRegistrationNo,PaThaKa.CompanyName,
	AccountTransaction.TransactionFormType FormType,Amount,'' Remark
	FROM AccountTransaction
	INNER JOIN AccountTransactionDetail ON AccountTransaction.Id = AccountTransactionDetail.AccountTransactionId
	INNER JOIN AccountTitle ON AccountTransactionDetail.AccountTitleId = AccountTitle.Id
	INNER JOIN ImportPermit ON AccountTransaction.TransactionId = ImportPermit.Id
	INNER JOIN PaThaKa ON ImportPermit.PaThaKaId = PaThaKa.Id
	WHERE IsPayment=1 AND AccountTitle.FormType='Online Fees'
	AND (VoucherDate>=@FromDate AND VoucherDate<=@ToDate))tmp
	WHERE tmp.FormType LIKE (CASE WHEN @FormType='' THEN tmp.FormType+'%' ELSE @FormType+'%' END)
	AND tmp.SakhanId = (CASE WHEN @SakhanId=0 THEN tmp.SakhanId ELSE @SakhanId END)
	ORDER BY tmp.VoucherDate,tmp.FormType
END

GO

/* dbo.sp_PaThaKaAllReport */
CREATE PROCEDURE [dbo].[sp_PaThaKaAllReport] 
	-- Add the parameters for the stored procedure here
	@BusinessTypeId int,
	@LineofBusinessId int,
	@State nvarchar(200),
	@Status nvarchar(50)
AS
BEGIN
	SELECT CompanyRegistrationNo,CompanyName,OwnerName,
	dbo.fn_GetNRCNo(OwnerNRCType,OwnerNRCPrefixId,OwnerNRCPrefixCodeId,OwnerNRCNo) as OwnerNRC,
	CompanyRegistrationDate,EndDate, 
	businessType.Name BusinessType,lineofBusiness.Name LineofBusiness,
	UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
	Mobile1,Mobile2,Mobile3,Fax,Email,Capital,currency.Code Currency,
	cardFees.Terms,DecisionDate,decisionCode.Name DecisionName,decisionCode.Position DecisionPosition,Status,MICPermitNo
	FROM PaThaKa
	INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
	INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
	INNER JOIN Currency currency ON PaThaKa.CurrencyId = Currency.Id
	INNER JOIN CardRegistrationFees cardFees ON PaThaKa.CardRegistrationFeesId = cardFees.Id
	INNER JOIN DecisionCode decisionCode ON PaThaKa.DecisionCodeId = decisionCode.Id
	WHERE BusinessTypeId=(CASE WHEN @BusinessTypeId=0 THEN BusinessTypeId ELSE @BusinessTypeId END)
	AND LineofBusinessId=(CASE WHEN @LineofBusinessId=0 THEN LineofBusinessId ELSE @LineofBusinessId END)
	AND Status=(CASE WHEN @Status='' THEN Status ELSE @Status END)
	AND State=(CASE WHEN @State='' THEN State ELSE @State END)
	ORDER BY IssuedDate
END

GO

/* dbo.sp_PathakaBindReport */
-- =============================================
-- Author:      <Author, , Name>
-- Create Date: <Create Date, , >
-- Description: <Description, , >
-- =============================================
CREATE PROCEDURE [dbo].[sp_PathakaBindReport]
(
    -- Add the parameters for the stored procedure here
    @FromDate datetime,
	@ToDate datetime
)
AS
BEGIN
    Select 
			a.ApplicationDate,
			b.ApproveDate,
			a.ApplicationNo,
			b.ApplicationNo as 'Bind Application No',
			b.Status,
			c.PaThaKaNo,
			d.MemberCode,
			d.Email,
			b.CompanyName
	from PaThaKaBind as a
	Left join PaThaKaRegistration as b on b.Id=a.PaThaKaId
	Left Join PaThaKa as c on c.Id=a.PaThaKaId
	Left Join Member as d on d.Id=a.MemberId
	Where b.ApproveDate between @FromDate AND @ToDate
	AND c.MemberId is not null 
END

GO

/* dbo.sp_PaThaKaByBusinessTypeReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_PaThaKaByBusinessTypeReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@BusinessTypeId int
AS
BEGIN
	SELECT businessType.Name BusinessType,Count(PaThaKa.Id) as CompanyCount FROM BusinessType businessType
	LEFT JOIN PaThaKa ON businessType.Id = PaThaKa.BusinessTypeId
	WHERE (PaThaKa.IssuedDate>=@FromDate AND PaThaKa.IssuedDate<=@ToDate)
	AND BusinessTypeId=(CASE WHEN @BusinessTypeId=0 THEN BusinessTypeId ELSE @BusinessTypeId END)
	AND Status='Registered'
	GROUP BY businessType.Name,businessType.SortOrder
	Order by businessType.SortOrder
END

GO

/* dbo.sp_PaThaKaRegistrationReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_PaThaKaRegistrationReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@PaymentType nvarchar(50),
	@ApplyType nvarchar(50)
AS
BEGIN
	SELECT PaThaKaRegistration.CreatedDate Date,CompanyRegistrationNo,CompanyName,
	businessType.Name BusinessType,lineofBusiness.Name LineofBusiness,
	UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,PaymentType,VoucherNo,VoucherDate,AccountTransaction.TotalAmount as TotalAmount
	FROM PaThaKaRegistration
	INNER JOIN AccountTransaction ON PaThaKaRegistration.Id = AccountTransaction.TransactionId
	INNER JOIN BusinessType businessType ON PaThaKaRegistration.BusinessTypeId = businessType.Id
	INNER JOIN LineofBusiness lineofBusiness ON PaThaKaRegistration.LineofBusinessId = lineofBusiness.Id
	WHERE PaThaKaRegistration.ApplyType=@ApplyType AND Status='Approved' AND IsPayment=1
	AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
	AND (PaThaKaRegistration.CreatedDate>=@FromDate AND PaThaKaRegistration.CreatedDate<=@ToDate)
END

GO

/* dbo.sp_PaThaKaReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_PaThaKaReport '2020-07-01','2020-07-28',0,0,'',''
CREATE PROCEDURE [dbo].[sp_PaThaKaReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@BusinessTypeId int,
	@LineofBusinessId int,
	@State nvarchar(200),
	@Status nvarchar(50)
AS
BEGIN
	SELECT CompanyRegistrationNo,CompanyName,CompanyRegistrationDate,EndDate, 
	businessType.Name BusinessType,lineofBusiness.Name LineofBusiness,
	UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,Capital,MICPermitNo
	FROM PaThaKa
	INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
	INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
	WHERE (IssuedDate>=@FromDate AND IssuedDate<=@ToDate)
	AND BusinessTypeId=(CASE WHEN @BusinessTypeId=0 THEN BusinessTypeId ELSE @BusinessTypeId END)
	AND LineofBusinessId=(CASE WHEN @LineofBusinessId=0 THEN LineofBusinessId ELSE @LineofBusinessId END)
	AND Status=(CASE WHEN @Status='' THEN Status ELSE @Status END)
	AND State=(CASE WHEN @State='' THEN State ELSE @State END)
	ORDER BY IssuedDate
END

GO

/* dbo.sp_PaThaKaValidInvalidReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_PaThaKaValidInvalidReport '2020-07-01','2020-07-28',0,0,''
CREATE PROCEDURE [dbo].[sp_PaThaKaValidInvalidReport] 
	-- Add the parameters for the stored procedure here
	@Date datetime,
	@BusinessTypeId int,
	@LineofBusinessId int,
	@State nvarchar(200),
	@Status nvarchar(50),
	@Type nvarchar(20) --valid,invalid
AS
BEGIN

	IF(@Type='valid')
	BEGIN
		SELECT CompanyRegistrationNo,IssuedDate,CompanyName,CompanyRegistrationDate,EndDate, 
		businessType.Name BusinessType,lineofBusiness.Name LineofBusiness,
		UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode
		FROM PaThaKa
		INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
		INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
		WHERE (EndDate>@Date)
		AND BusinessTypeId=(CASE WHEN @BusinessTypeId=0 THEN BusinessTypeId ELSE @BusinessTypeId END)
		AND LineofBusinessId=(CASE WHEN @LineofBusinessId=0 THEN LineofBusinessId ELSE @LineofBusinessId END)
		AND Status=(CASE WHEN @Status='' THEN Status ELSE @Status END)
		AND State=(CASE WHEN @State='' THEN State ELSE @State END)
		ORDER BY IssuedDate
	END
	ELSE 
	BEGIN
		SELECT CompanyRegistrationNo,IssuedDate,CompanyName,CompanyRegistrationDate,EndDate, 
		businessType.Name BusinessType,lineofBusiness.Name LineofBusiness,
		UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode
		FROM PaThaKa
		INNER JOIN BusinessType businessType ON PaThaKa.BusinessTypeId = businessType.Id
		INNER JOIN LineofBusiness lineofBusiness ON PaThaKa.LineofBusinessId = lineofBusiness.Id
		WHERE (EndDate<@Date)
		AND BusinessTypeId=(CASE WHEN @BusinessTypeId=0 THEN BusinessTypeId ELSE @BusinessTypeId END)
		AND LineofBusinessId=(CASE WHEN @LineofBusinessId=0 THEN LineofBusinessId ELSE @LineofBusinessId END)
		AND Status=(CASE WHEN @Status='' THEN Status ELSE @Status END)
		AND State=(CASE WHEN @State='' THEN State ELSE @State END)
		ORDER BY IssuedDate
	END

	
END

GO

/* dbo.sp_PendingReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
--exec sp_ExportLicenceAmendReport ''
CREATE PROCEDURE  [dbo].[sp_PendingReport] 
   @FromDate datetime,
   @ToDate datetime,
   @FormType nvarchar(50),
   	@ExportImportSectionId int
AS
BEGIN
	IF(@FormType='Import Licence')
	BEGIN
  	SELECT ImportLicence.Status,ImportLicence.ApplyType,ImportLicence.ApplicationDate , ImportLicence.ApplicationNo, section.Code SectionCode,section.Name SectionName,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
		(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Currency,
		(SELECT top 1 ImportLicenceItem.Description FROM ImportLicenceItem
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) AdditionalDescription,
		(SELECT ISNULL(SUM(ImportLicenceItem.Amount),0) FROM ImportLicenceItem
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Amount, ImportLicence.CommodityType,
		(SELECT top 1 ImportLicenceItem.HSCode FROM ImportLicenceItem
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) HSCode
		FROM ImportLicence
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id
		WHERE (ImportLicence.Status='Pending' or ImportLicence.Status='Reject')
		AND (ImportLicence.ApplicationDate>=@FromDate AND ImportLicence.ApplicationDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
	
     END
	 ELSE IF(@FormType='Export Licence')
	BEGIN
  	SELECT ExportLicence.Status,ExportLicence.ApplyType,ExportLicence.ApplicationDate , ExportLicence.ApplicationNo, section.Code SectionCode,section.Name SectionName,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
		(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Currency,
		(SELECT top 1 ExportLicenceItem.Description FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) AdditionalDescription,
		(SELECT ISNULL(SUM(ExportLicenceItem.Amount),0) FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Amount, ExportLicence.CommodityType,
		(SELECT top 1 ExportLicenceItem.HSCode FROM ExportLicenceItem
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) HSCode
		FROM ExportLicence
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		WHERE (ExportLicence.Status='Pending' or ExportLicence.Status='Reject')
		AND (ExportLicence.ApplicationDate>=@FromDate AND ExportLicence.ApplicationDate<=@ToDate)
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
     END
	 ELSE IF(@FormType='Border Import Licence')
	BEGIN
		SELECT BorderImportLicence.Status,BorderImportLicence.ApplyType,BorderImportLicence.ApplicationDate , BorderImportLicence.ApplicationNo, section.Code SectionCode,section.Name SectionName,
		PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(SELECT top 1 BorderImportLicenceItem.Description FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) AdditionalDescription,
		(SELECT ISNULL(SUM(BorderImportLicenceItem.Amount),0) FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Amount, BorderImportLicence.CommodityType,
		(SELECT top 1 BorderImportLicenceItem.HSCode FROM BorderImportLicenceItem
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) HSCode
		FROM BorderImportLicence
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId = PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		WHERE (BorderImportLicence.Status='Pending' or BorderImportLicence.Status='Reject')
		AND (BorderImportLicence.ApplicationDate>=@FromDate AND BorderImportLicence.ApplicationDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
	
	END
END


GO

/* dbo.sp_PermitBusinessByPaThaKaReport */
CREATE PROCEDURE [dbo].[sp_PermitBusinessByPaThaKaReport] 
  @CompanyRegistrationNo nvarchar(20) 
AS   

SELECT Description FROM PaThaKaPermitBusiness
FULL JOIN PermitBusiness permitbusiness on permitbusiness.Id=PaThaKaPermitBusiness.PermitBUsinessId
FULL JOIN PaThaKa pathaka on pathaka.Id=PaThaKaPermitBusiness.PaThaKaId
WHERE pathaka.CompanyRegistrationNo=@CompanyRegistrationNo





	



	

GO

/* dbo.sp_ReExportByPaThaKaReport */
CREATE PROCEDURE [dbo].[sp_ReExportByPaThaKaReport]   
    @CompanyRegistrationNo nvarchar(20)
AS   

    SELECT CompanyRegistrationNo,ReExport.ReExportNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			UnitLevel ReExportUnitLevel,StreetNumberStreetName ReExportStreetNumberStreetName,QuarterCityTownship ReExportQuarterCityTownship,State ReExportState,Country ReExportCountry,PostalCode ReExportPostalCode,
			ReExport.IssuedDate,ReExport.EndDate
			FROM ReExport
			INNER JOIN PaThaKa ON ReExport.PaThaKaId = PaThaKa.Id
			WHERE CompanyRegistrationNo=@CompanyRegistrationNo

GO

/* dbo.sp_ReExportReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_ReExportReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@Date datetime,
	@ApplyType nvarchar(20),
	@Type nvarchar(20) --Summary,Detail
AS
BEGIN
	IF(@Type='Summary')
	BEGIN
		SELECT Count(Id) ApplicationCount,'New' ApplyType FROM ReExportRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='New' AND Status='Approved'
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Cancel' ApplyType FROM ReExportRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Cancel' AND Status='Approved'
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Extension' ApplyType FROM ReExportRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Extension' AND Status='Approved'
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Valid' ApplyType FROM ReExport
		WHERE (EndDate>@Date)
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Invalid' ApplyType FROM ReExport
		WHERE (EndDate<@Date)
	END
	ELSE
	BEGIN

		IF(@ApplyType='Valid')
		BEGIN
			SELECT CompanyRegistrationNo,ReExport.ReExportNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			UnitLevel ReExportUnitLevel,StreetNumberStreetName ReExportStreetNumberStreetName,QuarterCityTownship ReExportQuarterCityTownship,State ReExportState,Country ReExportCountry,PostalCode ReExportPostalCode,
			ReExport.IssuedDate,ReExport.EndDate
			FROM ReExport
			INNER JOIN PaThaKa ON ReExport.PaThaKaId = PaThaKa.Id
			WHERE ReExport.EndDate>@Date
		END
		ELSE IF(@ApplyType='Invalid')
		BEGIN
			SELECT CompanyRegistrationNo,ReExport.ReExportNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			UnitLevel ReExportUnitLevel,StreetNumberStreetName ReExportStreetNumberStreetName,QuarterCityTownship ReExportQuarterCityTownship,State ReExportState,Country ReExportCountry,PostalCode ReExportPostalCode,
			ReExport.IssuedDate,ReExport.EndDate
			FROM ReExport
			INNER JOIN PaThaKa ON ReExport.PaThaKaId = PaThaKa.Id
			WHERE ReExport.EndDate<@Date
		END
		ELSE
		BEGIN
			SELECT CompanyRegistrationNo,ReExport.ReExportNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			UnitLevel ReExportUnitLevel,StreetNumberStreetName ReExportStreetNumberStreetName,QuarterCityTownship ReExportQuarterCityTownship,State ReExportState,Country ReExportCountry,PostalCode ReExportPostalCode,
			ReExport.IssuedDate,ReExport.EndDate
			FROM ReExport
			INNER JOIN PaThaKa ON ReExport.PaThaKaId = PaThaKa.Id
			INNER JOIN ReExportRegistration ON ReExport.ReExportNo = ReExportRegistration.ReExportNo
			WHERE ApplyType=@ApplyType AND ReExportRegistration.Status='Approved'
			AND (ReExportRegistration.CreatedDate>=@FromDate AND ReExportRegistration.CreatedDate<=@ToDate)	
		END

		
	END
END

GO

/* dbo.sp_SaleCenterByPaThaKaReport */
CREATE PROCEDURE [dbo].[sp_SaleCenterByPaThaKaReport]   
    @CompanyRegistrationNo nvarchar(20)   
AS   
SELECT PaThaKa.CompanyRegistrationNo,SaleCenter.SaleCenterNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			SaleCenter.Name SaleCenterName,dbo.fn_GetNRCNo(SaleCenter.NRCType,SaleCenter.NRCPrefixId,SaleCenter.NRCPrefixCodeId,SaleCenter.NRCNo) NRCNo,
			CASE WHEN SaleCenter.BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=SaleCenter.BusinessServiceAgencyId) END SaleCenterBusinessServiceAgencyNo,
			UnitLevel SaleCenterUnitLevel,StreetNumberStreetName SaleCenterStreetNumberStreetName,QuarterCityTownship SaleCenterQuarterCityTownship,State SaleCenterState,Country SaleCenterCountry,PostalCode SaleCenterPostalCode,
			SaleCenter.IssuedDate SaleCenterIssuedDate,SaleCenter.EndDate SaleCenterEndDate
			FROM SaleCenter
			INNER JOIN PaThaKa ON SaleCenter.PaThaKaId = PaThaKa.Id
			WHERE CompanyRegistrationNo=@CompanyRegistrationNo

GO

/* dbo.sp_SaleCenterRegistrationReport */
CREATE PROCEDURE [dbo].[sp_SaleCenterRegistrationReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@PaymentType nvarchar(50),
	@ApplyType nvarchar(50),
	@RegistrationType nvarchar(200)
AS
BEGIN
	SELECT SaleCenterRegistration.CreatedDate Date,PaThaKa.CompanyRegistrationNo,SaleCenterRegistration.SaleCenterNo,PaThaKa.CompanyName,
	UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
	SaleCenterRegistration.SaleCenterNo,SaleCenterRegistration.Name,dbo.fn_GetNRCNo(SaleCenterRegistration.NRCType,SaleCenterRegistration.NRCPrefixId,SaleCenterRegistration.NRCPrefixCodeId,SaleCenterRegistration.NRCNo) NRCNo,
	CASE WHEN SaleCenterRegistration.BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=SaleCenterRegistration.BusinessServiceAgencyId) END BusinessServiceAgencyNo,
	SaleCenterUnitLevel,SaleCenterStreetNumberStreetName,SaleCenterQuarterCityTownship,SaleCenterState,SaleCenterCountry,SaleCenterPostalCode,
	PaymentType,VoucherNo,VoucherDate,AccountTransaction.TotalAmount as TotalAmount
	FROM SaleCenterRegistration
	INNER JOIN PaThaKa ON SaleCenterRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN AccountTransaction ON SaleCenterRegistration.Id = AccountTransaction.TransactionId
	WHERE SaleCenterRegistration.ApplyType=@ApplyType AND SaleCenterRegistration.Status='Approved' AND IsPayment=1
	AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
	AND (SaleCenterRegistration.CreatedDate>=@FromDate AND SaleCenterRegistration.CreatedDate<=@ToDate)
	AND (SaleCenterRegistration.RegistrationType=@RegistrationType)
END

GO

/* dbo.sp_SaleCenterReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_SaleCenterReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@Date datetime,
	@ApplyType nvarchar(20),
	@FormType nvarchar(50),
	@Type nvarchar(20) --Summary,Detail
AS
BEGIN
	IF(@Type='Summary')
	BEGIN
		SELECT Count(Id) ApplicationCount,'New' ApplyType FROM SaleCenterRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='New' AND Status='Approved'
		AND RegistrationType=@FormType
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Cancel' ApplyType FROM SaleCenterRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Cancel' AND Status='Approved'
		AND RegistrationType=@FormType
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Extension' ApplyType FROM SaleCenterRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Extension' AND Status='Approved'
		AND RegistrationType=@FormType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Valid' ApplyType FROM SaleCenter
		WHERE (EndDate>@Date)
		AND RegistrationType=@FormType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Invalid' ApplyType FROM SaleCenter
		WHERE (EndDate<@Date)
		AND RegistrationType=@FormType
	END
	ELSE
	BEGIN

		IF(@ApplyType='Valid')
		BEGIN
			SELECT PaThaKa.CompanyRegistrationNo,SaleCenter.SaleCenterNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			SaleCenter.Name,dbo.fn_GetNRCNo(SaleCenter.NRCType,SaleCenter.NRCPrefixId,SaleCenter.NRCPrefixCodeId,SaleCenter.NRCNo) NRCNo,
			CASE WHEN BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel SaleCenterUnitLevel,StreetNumberStreetName SaleCenterStreetNumberStreetName,QuarterCityTownship SaleCenterQuarterCityTownship,State SaleCenterState,Country SaleCenterCountry,PostalCode SaleCenterPostalCode,
			SaleCenter.IssuedDate,SaleCenter.EndDate
			FROM SaleCenter
			INNER JOIN PaThaKa ON SaleCenter.PaThaKaId = PaThaKa.Id
			WHERE SaleCenter.EndDate>@Date
			AND RegistrationType=@FormType
		END
		ELSE IF(@ApplyType='Invalid')
		BEGIN

			SELECT PaThaKa.CompanyRegistrationNo,SaleCenter.SaleCenterNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			SaleCenter.Name,dbo.fn_GetNRCNo(SaleCenter.NRCType,SaleCenter.NRCPrefixId,SaleCenter.NRCPrefixCodeId,SaleCenter.NRCNo) NRCNo,
			CASE WHEN BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel SaleCenterUnitLevel,StreetNumberStreetName SaleCenterStreetNumberStreetName,QuarterCityTownship SaleCenterQuarterCityTownship,State SaleCenterState,Country SaleCenterCountry,PostalCode SaleCenterPostalCode,
			SaleCenter.IssuedDate,SaleCenter.EndDate
			FROM SaleCenter
			INNER JOIN PaThaKa ON SaleCenter.PaThaKaId = PaThaKa.Id
			WHERE SaleCenter.EndDate<@Date
			AND RegistrationType=@FormType
		END
		ELSE
		BEGIN
			SELECT PaThaKa.CompanyRegistrationNo,SaleCenter.SaleCenterNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			SaleCenter.Name,dbo.fn_GetNRCNo(SaleCenter.NRCType,SaleCenter.NRCPrefixId,SaleCenter.NRCPrefixCodeId,SaleCenter.NRCNo) NRCNo,
			CASE WHEN SaleCenter.BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=SaleCenter.BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel SaleCenterUnitLevel,StreetNumberStreetName SaleCenterStreetNumberStreetName,QuarterCityTownship SaleCenterQuarterCityTownship,State SaleCenterState,Country SaleCenterCountry,PostalCode SaleCenterPostalCode,
			SaleCenter.IssuedDate,SaleCenter.EndDate
			FROM SaleCenter
			INNER JOIN PaThaKa ON SaleCenter.PaThaKaId = PaThaKa.Id
			INNER JOIN SaleCenterRegistration ON SaleCenter.SaleCenterNo = SaleCenterRegistration.SaleCenterNo
			WHERE ApplyType=@ApplyType AND SaleCenterRegistration.Status='Approved' AND SaleCenter.RegistrationType=@FormType
			AND (SaleCenterRegistration.CreatedDate>=@FromDate AND SaleCenterRegistration.CreatedDate<=@ToDate)	
		END

		
	END
END

GO

/* dbo.sp_ShowRoomByPaThaKaReport */
CREATE PROCEDURE [dbo].[sp_ShowRoomByPaThaKaReport]   
    @CompanyRegistrationNo nvarchar(20)   
AS   

 SELECT PaThaKa.CompanyRegistrationNo,ShowRoom.ShowRoomNo,ShowRoom.AuthorizeCompany CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			ShowRoom.Name,dbo.fn_GetNRCNo(ShowRoom.NRCType,ShowRoom.NRCPrefixId,ShowRoom.NRCPrefixCodeId,ShowRoom.NRCNo) NRCNo,
			CASE WHEN ShowRoom.BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=ShowRoom.BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel ,StreetNumberStreetName ,QuarterCityTownship ,State ,Country ,PostalCode ,
			ShowRoom.AuthorizeCompanyUnitLevel ShowRoomUnitLevel,ShowRoom.AuthorizeCompanyStreetNumberStreetName ShowRoomStreetNumberStreetName,ShowRoom.AuthorizeCompanyQuarterCityTownship ShowRoomQuarterCityTownship,ShowRoom.AuthorizeCompanyState ShowRoomState,ShowRoom.AuthorizeCompanyCountry ShowRoomCountry,ShowRoom.AuthorizeCompanyPostalCode ShowRoomPostalCode,
			ShowRoom.IssuedDate,ShowRoom.EndDate
			FROM ShowRoom
			INNER JOIN PaThaKa ON ShowRoom.PaThaKaId = PaThaKa.Id
			WHERE CompanyRegistrationNo=@CompanyRegistrationNo

			


			

GO

/* dbo.sp_ShowRoomRegistrationReport */
CREATE PROCEDURE [dbo].[sp_ShowRoomRegistrationReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@PaymentType nvarchar(50),
	@ApplyType nvarchar(50),
	@RegistrationType nvarchar(200)
AS
BEGIN
	SELECT ShowRoomRegistration.CreatedDate Date,PaThaKa.CompanyRegistrationNo,ShowRoomRegistration.ShowRoomNo,PaThaKa.CompanyName,
	UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
	ShowRoomRegistration.ShowRoomNo,ShowRoomRegistration.Name,dbo.fn_GetNRCNo(ShowRoomRegistration.NRCType,ShowRoomRegistration.NRCPrefixId,ShowRoomRegistration.NRCPrefixCodeId,ShowRoomRegistration.NRCNo) NRCNo,
	CASE WHEN ShowRoomRegistration.BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=ShowRoomRegistration.BusinessServiceAgencyId) END BusinessServiceAgencyNo,
	ShowRoomUnitLevel,ShowRoomStreetNumberStreetName,ShowRoomQuarterCityTownship,ShowRoomState,ShowRoomCountry,ShowRoomPostalCode,
	ShowRoomUnitLevel2,ShowRoomStreetNumberStreetName2,ShowRoomQuarterCityTownship2,ShowRoomState2,ShowRoomCountry2,ShowRoomPostalCode2,
	ShowRoomUnitLevel3,ShowRoomStreetNumberStreetName3,ShowRoomQuarterCityTownship3,ShowRoomState3,ShowRoomCountry3,ShowRoomPostalCode3,
	ShowRoomUnitLevel4,ShowRoomStreetNumberStreetName4,ShowRoomQuarterCityTownship4,ShowRoomState4,ShowRoomCountry4,ShowRoomPostalCode4,
	ShowRoomUnitLevel5,ShowRoomStreetNumberStreetName5,ShowRoomQuarterCityTownship5,ShowRoomState5,ShowRoomCountry5,ShowRoomPostalCode5,
	PaymentType,VoucherNo,VoucherDate,AccountTransaction.TotalAmount as TotalAmount
	FROM ShowRoomRegistration
	INNER JOIN PaThaKa ON ShowRoomRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN AccountTransaction ON ShowRoomRegistration.Id = AccountTransaction.TransactionId
	WHERE ShowRoomRegistration.ApplyType=@ApplyType AND ShowRoomRegistration.Status='Approved' AND IsPayment=1
	AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
	AND (ShowRoomRegistration.CreatedDate>=@FromDate AND ShowRoomRegistration.CreatedDate<=@ToDate)
	AND (ShowRoomRegistration.RegistrationType=@RegistrationType)
END

GO

/* dbo.sp_ShowRoomReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_ShowRoomReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@Date datetime,
	@ApplyType nvarchar(20),
	@FormType nvarchar(50),
	@Type nvarchar(20) --Summary,Detail
AS
BEGIN
	IF(@Type='Summary')
	BEGIN
		SELECT Count(Id) ApplicationCount,'New' ApplyType FROM ShowRoomRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='New' AND Status='Approved'
		AND RegistrationType=@FormType
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Cancel' ApplyType FROM ShowRoomRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Cancel' AND Status='Approved'
		AND RegistrationType=@FormType
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Extension' ApplyType FROM ShowRoomRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Extension' AND Status='Approved'
		AND RegistrationType=@FormType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Valid' ApplyType FROM ShowRoom
		WHERE (EndDate>@Date)
		AND RegistrationType=@FormType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Invalid' ApplyType FROM ShowRoom
		WHERE (EndDate<@Date)
		AND RegistrationType=@FormType
	END
	ELSE
	BEGIN

		IF(@ApplyType='Valid')
		BEGIN
			SELECT PaThaKa.CompanyRegistrationNo,ShowRoom.ShowRoomNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			ShowRoom.Name,dbo.fn_GetNRCNo(ShowRoom.NRCType,ShowRoom.NRCPrefixId,ShowRoom.NRCPrefixCodeId,ShowRoom.NRCNo) NRCNo,
			CASE WHEN BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel ShowRoomUnitLevel,StreetNumberStreetName ShowRoomStreetNumberStreetName,QuarterCityTownship ShowRoomQuarterCityTownship,State ShowRoomState,Country ShowRoomCountry,PostalCode ShowRoomPostalCode,
			ShowRoom.IssuedDate,ShowRoom.EndDate
			FROM ShowRoom
			INNER JOIN PaThaKa ON ShowRoom.PaThaKaId = PaThaKa.Id
			WHERE ShowRoom.EndDate>@Date
			AND RegistrationType=@FormType
		END
		ELSE IF(@ApplyType='Invalid')
		BEGIN

			SELECT PaThaKa.CompanyRegistrationNo,ShowRoom.ShowRoomNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			ShowRoom.Name,dbo.fn_GetNRCNo(ShowRoom.NRCType,ShowRoom.NRCPrefixId,ShowRoom.NRCPrefixCodeId,ShowRoom.NRCNo) NRCNo,
			CASE WHEN BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel ShowRoomUnitLevel,StreetNumberStreetName ShowRoomStreetNumberStreetName,QuarterCityTownship ShowRoomQuarterCityTownship,State ShowRoomState,Country ShowRoomCountry,PostalCode ShowRoomPostalCode,
			ShowRoom.IssuedDate,ShowRoom.EndDate
			FROM ShowRoom
			INNER JOIN PaThaKa ON ShowRoom.PaThaKaId = PaThaKa.Id
			WHERE ShowRoom.EndDate<@Date
			AND RegistrationType=@FormType
		END
		ELSE
		BEGIN
			SELECT PaThaKa.CompanyRegistrationNo,ShowRoom.ShowRoomNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			ShowRoom.Name,dbo.fn_GetNRCNo(ShowRoom.NRCType,ShowRoom.NRCPrefixId,ShowRoom.NRCPrefixCodeId,ShowRoom.NRCNo) NRCNo,
			CASE WHEN ShowRoom.BusinessServiceAgencyId='' THEN '' ELSE (SELECT TOP 1 BusinessServiceAgencyNo FROM BusinessServiceAgency WHERE Id=ShowRoom.BusinessServiceAgencyId) END BusinessServiceAgencyNo,
			UnitLevel ShowRoomUnitLevel,StreetNumberStreetName ShowRoomStreetNumberStreetName,QuarterCityTownship ShowRoomQuarterCityTownship,State ShowRoomState,Country ShowRoomCountry,PostalCode ShowRoomPostalCode,
			ShowRoom.IssuedDate,ShowRoom.EndDate
			FROM ShowRoom
			INNER JOIN PaThaKa ON ShowRoom.PaThaKaId = PaThaKa.Id
			INNER JOIN ShowRoomRegistration ON ShowRoom.ShowRoomNo = ShowRoomRegistration.ShowRoomNo
			WHERE ApplyType=@ApplyType AND ShowRoomRegistration.Status='Approved' AND ShowRoom.RegistrationType=@FormType
			AND (ShowRoomRegistration.CreatedDate>=@FromDate AND ShowRoomRegistration.CreatedDate<=@ToDate)	
		END

		
	END
END

GO

/* dbo.sp_TestReport */
-- =============================================
-- Author:      Name
-- Create Date: 
-- Description: 
-- =============================================
--EXEC sp_MemberRegistrationReport '2020-11-01 00:00:00','2020-11-03 23:59:59','All'
CREATE PROCEDURE [dbo].[sp_TestReport]

AS
BEGIN
	SELECT 
		 P.CompanyRegistrationNo,P.MOCStatus,P.CompanyName,
(p.UnitLevel+' '+p.StreetNumberStreetName+' '+p.QuarterCityTownship+''+p.State+' '+p.Country) as Address
,M.Email as Member_Email,M.Password,M.MemberCode,M.EndDate as MemberExpiredDate from Member as M
JOIN PaThaKa as P on p.MemberId= M.Id
where P.CompanyRegistrationNo in (
'174300275'
,'162596497'
,'171498007'
,'114639109'
,'101106535'
,'106304807'
,'110641370'
,'106294682'
,'107615180'
,'112517774'
,'107616195'
,'118586441'
,'113701838'
,'104990673'
,'121177722'
,'111882231'
,'112155023'
,'104937942'
,'101781917'
,'100955172'
,'105002629'
,'104092829'
,'107212337'
,'122339734'
,'112823603'
,'106842159'
,'111808570'
,'100055252'
,'106818983'
,'122268284'
,'105178719'
,'100215934'
,'107670173'
,'110628064'
,'120660497'
,'114795763'
,'103602041'
,'106071586'
,'121749823'
,'115266594'
,'101272389'
,'114585815'
,'103514703'
,'100222094'
,'102115511'
,'108186666'
,'104952836'
,'100442612'
,'105857381'
,'109193216'
,'108033150'
,'113906472'
,'122290670'
,'104791549'
,'115633228'
,'107083766'
,'148668302'
,'108308273'
,'109072567'
,'112642528'
,'111283060'
,'119840449'
,'105852916'
,'110958307'
,'113472928'
,'112882286'
,'101002764'
,'103409519'
,'105350996'
,'113994649'
,'109794449'
,'106448493'
,'114374830'
,'114540463'
,'110196172'
,'106206333'
,'112206094'
,'100929538'
,'117973832'
,'111981353'
,'116194252'
,'182204102'
,'104393721'
,'107609628'
,'101535940'
,'103718651'
,'111176663'
,'107043500'
,'109481580'
,'107370366'
,'109819093'
,'110543581'
,'106631522'
,'117570630'
,'103388538'
,'115410377'
,'111147329'
,'109292494'
,'101935914'
,'103730066'
,'103059879'
,'160143371'
,'102701364'
,'110294093'
,'102587022'
,'106705380'
,'104941613'
,'108956712'
,'107163034'
,'102581180'
,'103254086'
,'121694298'
,'107472126'
,'100758512'
,'101639878'
,'109441899'
,'117325229'
,'152043775'
,'106109052'
,'110528000'
,'103168988'
,'112133798'
,'108076453'
,'110409621'
,'111749981'
,'103713668'
,'103877709'
,'108184191'
,'143382427'
,'104059317'
,'109938718'
,'103652200'
,'105110898'
,'107868577'
,'105930089'
,'108655267'
,'106727325'
,'105648715'
,'101114422'
,'113514671'
,'104059368'
,'106017557'
,'104210961'
,'123782933'
,'103434459'
,'113669470'
,'102473086'
,'110163487'
,'124418658'
,'107119248'
,'101163458'
,'106279225'
,'107837655'
,'112866302'
,'111267928'
,'108230835'
,'112117598'
,'109635006'
,'101973905'
,'117958817'
,'101594041'
,'113897643'
,'120740105'
,'112105700'
,'101460657'
,'119149231'
,'109324825'
,'114515396'
,'121595133'
,'101478009'
,'106933294'
,'107535195'
,'186358228'
,'118452240'
,'108518537'
,'105394608'
,'151585418'
,'112727795'
,'118388887'
,'106140901'
,'105728301'
,'121874075'
,'109065994'
,'105152957'
,'105606990'
,'112842071'
,'111225893'
,'107542035'
,'105611897'
,'107011560'
,'109513121'
,'117911926'
,'100221454'
,'115515799'
,'102569873'
,'100670402'
,'109381543'
,'111271445'
,'109056030'
,'114485292'
,'101461890'
,'102666984'
,'109323314'
,'102603346'
,'109734608'
,'120941143'
,'115680021'
,'108790849'
,'101645703'
,'111311889'
,'121266997'
,'104212964'
,'100586576'
,'110161867'
,'102596005'
,'106472106'
,'100196085'
,'107971564'
,'106071349'
,'116878402'
,'100873370'
,'103853729'
,'106686998'
,'106510067'
,'101520358'
,'109865621'
,'103342015'
,'110200137'
,'101002403'
,'109844632'
,'111798133'
,'115345958'
,'119133378'
,'105365268'
,'106803994'
,'105263031'
,'105894260'
,'144782119'
,'119444632'
,'167274587'
,'102131924'
,'101522261'
,'112792899'
,'106035687'
,'167781802'
,'106065799'
,'105450117'
,'104733034'
,'124146372'
,'104362524'
,'100928965'
,'102267435'
,'107477179'
,'105106890'
,'104151159'
,'101104885'
,'118238532'
,'107778713'
,'121545152'
,'103057663'
,'105712634'
,'106382484'
,'111732957'
,'110177682'
,'150146364'
,'102483391'
,'101629139'
,'106607702'
,'108826339'
,'103716101'
,'111303452'
,'107821422'
,'102852885'
,'157910132'
,'100514524'
,'109963585'
,'107676732'
,'105812655'
,'101963861'
,'100639165'
,'111569258'
,'107191488'
,'105605889'
,'105147996'
,'115083082'
,'109713147'
,'101593746'
,'105856520'
,'105717563'
,'113385324'
,'103000157'
,'113015845'
,'116347571'
,'104427200'
,'120478133'
,'107388931'
,'123431456'
,'104562833'
,'120940775'
,'100515261'
,'120329545'
,'103106788'
,'105999372'
,'124375770'
,'117524434'
,'110839650'
,'102374355'
,'112177469'
,'120774891'
,'113406275'
,'169172153'
,'114551430'
,'112033254'
,'109788023'
,'113176997'
,'113003995'
,'112050736'
,'108365722'
,'104376177'
,'118115988'
,'122911640'
,'111107394'
,'117455319'
,'116454572'
,'111575150'
,'108329211'
,'106537062'
,'123946898'
,'102643402'
,'110756178'
,'109229687'
,'105304986'
,'109745928'
,'105179081'
,'104441874'
,'104366295'
,'123446747'
,'109910570'
,'104908721'
,'106668841'
,'108985364'
,'100970996'
,'100002159'
,'120776177'
,'102644964'
,'105607989'
,'101051919'
,'135265411'
,'109552518'
,'106353352'
,'117912655'
,'112901914'
,'106623376'
,'113204737'
,'100892030'
,'108138866'
,'122730166'
,'119977800'
,'182662224'
,'193935133'
,'113700637'
,'118310950'
,'105986777'
,'101108937'
,'139930770'
,'110018223'
,'121208148'
,'111273146'
,'100214997'
,'120206990'
,'107852662'
,'109230634'
,'108106506'
,'189409230'
,'119876303'
,'107139613'
,'145705347'
,'121598167'
,'116229811'
,'102598911'
,'103954207'
,'110276850'
,'105113528'
,'102314948'
,'113743239'
,'115676253'
,'110105908'
,'115700227'
,'121717441'
,'104404448'
,'118721683'
,'103124441'
,'104163874'
,'121528797'
,'115306715'
,'121156539'
,'120027026'
,'117333205'
,'100067684'
,'104411592'
,'100318636'
,'111953287'
,'121804050'
,'100360365'
,'104999581'
,'137450313'
,'113020288'
,'112287914'
,'108200502'
,'103056195'
,'109748803'
,'120040944'
,'104229972'
,'100918234'
,'106894590'
,'121355647'
,'121449188'
,'115679244'
,'110921365'
,'101465691'
,'113726504'
,'124067839'
,'120045709'
,'103502136'
,'112335633'
,'107215182'
,'108084936'
,'122357031'
,'123955765'
,'120940341'
,'116936569'
,'119259673'
,'121497387'
,'101701719'
,'122657000'
,'122072738'
,'122788695'
,'124011027'
,'113981253'
,'104076548'
,'119113237'
,'123044835'
,'126722141'
,'130606636'
,'124132444'
,'124205352'
,'105720262'
,'122343766'
,'124968054'
,'100228122'
,'125499414'
,'124405580'
,'114055484'
,'116230135'
,'124726263'
,'105689896'
,'124375762'
,'125518176'
,'102914589'
,'131331266'
,'126521979'
)
END

GO

/* dbo.sp_VoucherReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_VoucherReport] 
	-- Add the parameters for the stored procedure here
	@FormType nvarchar(50),
	@FromDate datetime,
	@ToDate datetime,
	@ExportImportSectionId int,
	@PaymentType nvarchar(50),
	@ApplyType nvarchar(20),
	@CompanyRegistrationNo nvarchar(10),
	@SakhanId int
AS
BEGIN
	IF(@FormType='Export Licence')
	BEGIN
		SELECT ExportLicence.ApplicationNo,ExportLicence.ApplicationDate,Users.FullName as ApprovedUser,AccountTransaction.PaymentDate Date,CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,section.Code SectionCode,
		ApplyType, OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,
		ExportLicence.CreatedDate LicenceDate,CONVERT(varchar,ExportLicence.CreatedDate,103) sLicenceDate,PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
		VoucherNo,VoucherDate,CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,TotalAmount Amount,PaymentType,
		(SELECT top 1 currency.Code FROM ExportLicenceItem
		INNER JOIN Currency currency ON ExportLicenceItem.CurrencyId = currency.Id
		WHERE ExportLicenceItem.ExportLicenceId=ExportLicence.Id) Currency,
		(Select SUM(Amount) as TotalAmount  from ExportLicenceItem
       where ExportLicenceItem.ExportLicenceId=ExportLicence.Id) as TotalAmount,ExportLicence.CommodityType
		FROM ExportLicence
		INNER JOIN AccountTransaction ON ExportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ExportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ExportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ExportLicence.Status='Approved'
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		ORDER BY AccountTransaction.PaymentDate
	END
	ELSE IF(@FormType='Import Licence')
	BEGIN
		SELECT ImportLicence.ApplicationNo,ImportLicence.ApplicationDate,Users.FullName as ApprovedUser,AccountTransaction.PaymentDate Date,CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,section.Code SectionCode,
		ApplyType, OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,
		ImportLicence.CreatedDate LicenceDate,CONVERT(varchar,ImportLicence.CreatedDate,103) sLicenceDate,PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
		VoucherNo,VoucherDate,CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,TotalAmount Amount,PaymentType,
		(SELECT top 1 currency.Code FROM ImportLicenceItem
		INNER JOIN Currency currency ON ImportLicenceItem.CurrencyId = currency.Id
		WHERE ImportLicenceItem.ImportLicenceId=ImportLicence.Id) Currency,
		(Select SUM(Amount) as TotalAmount  from ImportLicenceItem
       where ImportLicenceItem.ImportLicenceId=ImportLicence.Id) as TotalAmount,ImportLicence.CommodityType,ImportLicence.ExchangeRate,ImportLicence.TotalCIF
		FROM ImportLicence
		INNER JOIN AccountTransaction ON ImportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ImportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportLicence.ExportImportSectionId = section.Id 
		INNER JOIN Users ON Users.Id = ImportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ImportLicence.Status='Approved'
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		ORDER BY AccountTransaction.PaymentDate
	END
	ELSE IF(@FormType='Export Permit')
	BEGIN
		SELECT ExportPermit.ApplicationNo,ExportPermit.ApplicationDate,Users.FullName as ApprovedUser,AccountTransaction.PaymentDate Date,CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,section.Code SectionCode,
		ApplyType, OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,
		ExportPermit.CreatedDate LicenceDate,CONVERT(varchar,ExportPermit.CreatedDate,103) sLicenceDate,PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
		VoucherNo,VoucherDate,CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,TotalAmount Amount,PaymentType,
		(SELECT top 1 currency.Code FROM ExportPermitItem
		INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
		WHERE ExportPermitItem.ExportPermitId=ExportPermit.Id) Currency,
		(Select SUM(Amount) as TotalAmount  from ExportPermitItem
        where ExportPermitItem.ExportPermitId=ExportPermit.Id) as TotalAmount,ExportPermit.CommodityType
		FROM ExportPermit
		INNER JOIN AccountTransaction ON ExportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ExportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ExportPermit.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ExportPermit.Status='Approved'
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		ORDER BY AccountTransaction.PaymentDate
	END
	ELSE IF(@FormType='Import Permit')
	BEGIN
		SELECT ImportPermit.ApplicationNo,ImportPermit.ApplicationDate,Users.FullName as ApprovedUser,AccountTransaction.PaymentDate Date,CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,section.Code SectionCode,
		ApplyType, OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,
		ImportPermit.CreatedDate LicenceDate,CONVERT(varchar,ImportPermit.CreatedDate,103) sLicenceDate,PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
		VoucherNo,VoucherDate,CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,TotalAmount Amount,PaymentType,
		(SELECT top 1 currency.Code FROM ImportPermitItem
		INNER JOIN Currency currency ON ImportPermitItem.CurrencyId = currency.Id
		WHERE ImportPermitItem.ImportPermitId=ImportPermit.Id) Currency,
		(Select SUM(Amount) as TotalAmount  from ImportPermitItem
       where ImportPermitItem.ImportPermitId=ImportPermit.Id) as TotalAmount,ImportPermit.CommodityType
		FROM ImportPermit
		INNER JOIN AccountTransaction ON ImportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON ImportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON ImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Users ON Users.Id = ImportPermit.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND ImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then ImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND ImportPermit.Status='Approved'
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		ORDER BY AccountTransaction.PaymentDate
	END
	ELSE IF(@FormType='Border Export Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderExportLicence.ApplicationNo,BorderExportLicence.ApplicationDate,Users.FullName as ApprovedUser,AccountTransaction.PaymentDate Date,CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,section.Code SectionCode,
		ApplyType, OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,
		BorderExportLicence.CreatedDate LicenceDate,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sLicenceDate,PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
		VoucherNo,VoucherDate,CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,TotalAmount Amount,PaymentType,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(Select SUM(Amount) as TotalAmount  from BorderExportLicenceItem
       where BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) as TotalAmount,BorderExportLicence.CommodityType
		FROM BorderExportLicence
		INNER JOIN AccountTransaction ON BorderExportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON BorderExportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderExportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Pa Tha Ka'
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderExportLicence.ApplicationNo,BorderExportLicence.ApplicationDate,Users.FullName as ApprovedUser,AccountTransaction.PaymentDate Date,CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,section.Code SectionCode,
		ApplyType, OldExportLicenceNo OldLicenceNo,ExportLicenceNo LicenceNo,
		BorderExportLicence.CreatedDate LicenceDate,CONVERT(varchar,BorderExportLicence.CreatedDate,103) sLicenceDate,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,
		VoucherNo,VoucherDate,CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,TotalAmount Amount,PaymentType,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,
		(SELECT top 1 currency.Code FROM BorderExportLicenceItem
		INNER JOIN Currency currency ON BorderExportLicenceItem.CurrencyId = currency.Id
		WHERE BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) Currency,
		(Select SUM(Amount) as TotalAmount  from BorderExportLicenceItem
       where BorderExportLicenceItem.BorderExportLicenceId=BorderExportLicence.Id) as TotalAmount,BorderExportLicence.CommodityType
		FROM BorderExportLicence
		INNER JOIN AccountTransaction ON BorderExportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN IndividualTrading ON BorderExportLicence.IndividualTradingId=IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderExportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportLicence.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderExportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderExportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderExportLicence.Status='Approved' AND BorderExportLicence.CardType='Individual Trading'
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderExportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportLicence.SakhanId ELSE @SakhanId END)
		)tmp
		
		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Import Licence')
	BEGIN
		SELECT * FROM
		(SELECT BorderImportLicence.ApplicationNo,BorderImportLicence.ApplicationDate,Users.FullName as ApprovedUser,AccountTransaction.PaymentDate Date,CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,section.Code SectionCode,
		ApplyType, OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,
		BorderImportLicence.CreatedDate LicenceDate,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sLicenceDate,PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
		VoucherNo,VoucherDate,CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,TotalAmount Amount,PaymentType,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(Select SUM(Amount) as TotalAmount  from BorderImportLicenceItem
       where BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) as TotalAmount,BorderImportLicence.CommodityType,BorderImportLicence.ExchangeRate,BorderImportLicence.TotalCIF
		FROM BorderImportLicence
		INNER JOIN AccountTransaction ON BorderImportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON BorderImportLicence.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderImportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Pa Tha Ka'
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END)
		UNION ALL
		SELECT BorderImportLicence.ApplicationNo,BorderImportLicence.ApplicationDate,Users.FullName as ApprovedUser,AccountTransaction.PaymentDate Date,CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,section.Code SectionCode,
		ApplyType, OldImportLicenceNo OldLicenceNo,ImportLicenceNo LicenceNo,
		BorderImportLicence.CreatedDate LicenceDate,CONVERT(varchar,BorderImportLicence.CreatedDate,103) sLicenceDate,IndividualTrading.TINNo CompanyRegistrationNo,IndividualTrading.Name CompanyName,
		VoucherNo,VoucherDate,CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,TotalAmount Amount,PaymentType,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,
		(SELECT top 1 currency.Code FROM BorderImportLicenceItem
		INNER JOIN Currency currency ON BorderImportLicenceItem.CurrencyId = currency.Id
		WHERE BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) Currency,
		(Select SUM(Amount) as TotalAmount  from BorderImportLicenceItem
       where BorderImportLicenceItem.BorderImportLicenceId=BorderImportLicence.Id) as TotalAmount,BorderImportLicence.CommodityType,BorderImportLicence.ExchangeRate,BorderImportLicence.TotalCIF
		FROM BorderImportLicence
		INNER JOIN AccountTransaction ON BorderImportLicence.Id=AccountTransaction.TransactionId
		INNER JOIN IndividualTrading ON BorderImportLicence.IndividualTradingId=IndividualTrading.Id
		INNER JOIN ExportImportSection section ON BorderImportLicence.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportLicence.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderImportLicence.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderImportLicence.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportLicence.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderImportLicence.Status='Approved' AND BorderImportLicence.CardType='Individual Trading'
		AND IndividualTrading.TINNo=(CASE WHEN @CompanyRegistrationNo='' then IndividualTrading.TINNo ELSE @CompanyRegistrationNo END)
		AND BorderImportLicence.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportLicence.SakhanId ELSE @SakhanId END))tmp

		ORDER BY tmp.Date
	END
	ELSE IF(@FormType='Border Export Permit')
	BEGIN
		SELECT BorderExportPermit.ApplicationNo,BorderExportPermit.ApplicationDate,Users.FullName as ApprovedUser,AccountTransaction.PaymentDate Date,CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,section.Code SectionCode,
		ApplyType, OldExportPermitNo OldLicenceNo,ExportPermitNo LicenceNo,
		BorderExportPermit.CreatedDate LicenceDate,CONVERT(varchar,BorderExportPermit.CreatedDate,103) sLicenceDate,PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
		VoucherNo,VoucherDate,CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,TotalAmount Amount,PaymentType,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,
		(SELECT top 1 currency.Code FROM BorderExportPermitItem
		INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
		WHERE BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) Currency,
		(Select SUM(Amount) as TotalAmount  from BorderExportPermitItem
       where BorderExportPermitItem.BorderExportPermitId=BorderExportPermit.Id) as TotalAmount,BorderExportPermit.CommodityType
		FROM BorderExportPermit
		INNER JOIN AccountTransaction ON BorderExportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON BorderExportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderExportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderExportPermit.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderExportPermit.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderExportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderExportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderExportPermit.Status='Approved'
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderExportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderExportPermit.SakhanId ELSE @SakhanId END)
		ORDER BY AccountTransaction.PaymentDate
	END
	ELSE IF(@FormType='Border Import Permit')
	BEGIN
		SELECT BorderImportPermit.ApplicationNo,BorderImportPermit.ApplicationDate,Users.FullName as ApprovedUser,AccountTransaction.PaymentDate Date,CONVERT(varchar,AccountTransaction.PaymentDate,103) sDate,section.Code SectionCode,
		ApplyType, OldImportPermitNo OldLicenceNo,ImportPermitNo LicenceNo,
		BorderImportPermit.CreatedDate LicenceDate,CONVERT(varchar,BorderImportPermit.CreatedDate,103) sLicenceDate,PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
		VoucherNo,VoucherDate,CONVERT(varchar,AccountTransaction.VoucherDate,103) sVoucherDate,TotalAmount Amount,PaymentType,
		sakhan.Id SakhanId,sakhan.Code SakhanCode,sakhan.Name SakhanName,
		(SELECT top 1 currency.Code FROM BorderImportPermitItem
		INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id
		WHERE BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) Currency,
		(Select SUM(Amount) as TotalAmount  from BorderImportPermitItem
       where BorderImportPermitItem.BorderImportPermitId=BorderImportPermit.Id) as TotalAmount,BorderImportPermit.CommodityType
		FROM BorderImportPermit
		INNER JOIN AccountTransaction ON BorderImportPermit.Id=AccountTransaction.TransactionId
		INNER JOIN PaThaKa ON BorderImportPermit.PaThaKaId=PaThaKa.Id
		INNER JOIN ExportImportSection section ON BorderImportPermit.ExportImportSectionId = section.Id
		INNER JOIN Sakhan sakhan ON BorderImportPermit.SakhanId = sakhan.Id
		INNER JOIN Users ON Users.Id = BorderImportPermit.ApproveUserId
		WHERE IsPayment=1
		AND (AccountTransaction.PaymentDate>=@FromDate AND AccountTransaction.PaymentDate<=@ToDate)
		AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)
		AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
		AND ApplyType=@ApplyType AND BorderImportPermit.Status='Approved'
		AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)
		AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)
		ORDER BY AccountTransaction.PaymentDate
	END
END

GO

/* dbo.sp_WholeSaleAndRetailByPaThaKaReport */
CREATE PROCEDURE [dbo].[sp_WholeSaleAndRetailByPaThaKaReport] 
    @CompanyRegistrationNo nvarchar(20) 
AS   

      SELECT Pathaka.CompanyRegistrationNo,WholeSaleRetail.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,WholeSaleRetailNo,WholeSaleRetailUnitLevel,WholeSaleRetailStreetNumberStreetName,WholeSaleRetailQuarterCityTownship,WholeSaleRetailState,WholeSaleRetailCountry,WholeSaleRetailPostalCode,
		WholeSaleRetail.IssuedDate WholeSaleRetailIssuedDate,WholeSaleRetail.EndDate WholeSaleRetailEndDate
		FROM WholeSaleRetail
		INNER JOIN PaThaKa ON WholeSaleRetail.PaThaKaId = PaThaKa.Id
		WHERE Pathaka.CompanyRegistrationNo=@CompanyRegistrationNo



GO

/* dbo.sp_WholeSaleRetailRegistrationReport */
CREATE PROCEDURE [dbo].[sp_WholeSaleRetailRegistrationReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@PaymentType nvarchar(50),
	@ApplyType nvarchar(50),
	@RegistrationType nvarchar(200)
AS
BEGIN
	SELECT * FROM 
	(SELECT WholeSaleRetailRegistration.CreatedDate Date,WholeSaleRetailRegistration.CompanyRegistrationNo,WholeSaleRetailRegistration.CompanyName,
	WholeSaleRetailRegistration.WholeSaleRetailNo,WholeSaleRetailRegistration.Name WholeSalRetailName,
	UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
	WholeSaleRetailUnitLevel,WholeSaleRetailStreetNumberStreetName,WholeSaleRetailQuarterCityTownship,WholeSaleRetailState,WholeSaleRetailCountry,WholeSaleRetailPostalCode,
	PaymentType,VoucherNo,VoucherDate,AccountTransaction.TotalAmount as TotalAmount
	FROM WholeSaleRetailRegistration
	INNER JOIN PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN AccountTransaction ON WholeSaleRetailRegistration.Id = AccountTransaction.TransactionId
	WHERE WholeSaleRetailRegistration.ApplyType=@ApplyType AND WholeSaleRetailRegistration.Status='Approved' AND IsPayment=1
	AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
	AND (WholeSaleRetailRegistration.CreatedDate>=@FromDate AND WholeSaleRetailRegistration.CreatedDate<=@ToDate)
	AND WholeSaleRetailRegistration.RegistrationType=@RegistrationType
	UNION ALL
	SELECT WholeSaleRetailRegistration.CreatedDate Date,WholeSaleRetailRegistration.CompanyRegistrationNo,WholeSaleRetailRegistration.CompanyName,
	WholeSaleRetailRegistration.WholeSaleRetailNo,WholeSaleRetailRegistration.Name WholeSalRetailName,
	PaThaKa.UnitLevel,PaThaKa.StreetNumberStreetName,PaThaKa.QuarterCityTownship,PaThaKa.State,PaThaKa.Country,PaThaKa.PostalCode,
	WholeSaleRetailUnitLevel,WholeSaleRetailStreetNumberStreetName,WholeSaleRetailQuarterCityTownship,WholeSaleRetailState,WholeSaleRetailCountry,WholeSaleRetailPostalCode,
	PaymentType,VoucherNo,VoucherDate,AccountTransaction.TotalAmount as TotalAmount
	FROM WholeSaleRetailRegistration
	INNER JOIN PaThaKa ON WholeSaleRetailRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN PaThaKaRegistration ON WholeSaleRetailRegistration.Id = PaThaKaRegistration.WholeSaleRetailRegistrationId AND PaThaKaRegistration.IsWholeSale=1
	INNER JOIN AccountTransaction ON PaThaKaRegistration.Id = AccountTransaction.TransactionId
	WHERE WholeSaleRetailRegistration.ApplyType=@ApplyType AND WholeSaleRetailRegistration.Status='Approved' AND IsPayment=1
	AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
	AND (WholeSaleRetailRegistration.CreatedDate>=@FromDate AND WholeSaleRetailRegistration.CreatedDate<=@ToDate)
	AND WholeSaleRetailRegistration.RegistrationType=@RegistrationType)tmp
	ORDER BY tmp.Date
END

GO

/* dbo.sp_WholeSaleRetailReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_WholeSaleRetailReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@Date datetime,
	@ApplyType nvarchar(20),
	@FormType nvarchar(50),
	@Type nvarchar(20) --Summary,Detail
AS
BEGIN
	IF(@Type='Summary')
	BEGIN
		SELECT Count(Id) ApplicationCount,'New' ApplyType FROM WholeSaleRetailRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='New' AND Status='Approved'
		AND RegistrationType=@FormType
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Cancel' ApplyType FROM WholeSaleRetailRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Cancel' AND Status='Approved'
		AND RegistrationType=@FormType
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Extension' ApplyType FROM WholeSaleRetailRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Extension' AND Status='Approved'
		AND RegistrationType=@FormType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Valid' ApplyType FROM WholeSaleRetail
		WHERE (EndDate>@Date)
		AND RegistrationType=@FormType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Invalid' ApplyType FROM WholeSaleRetail
		WHERE (EndDate<@Date)
		AND RegistrationType=@FormType
	END
	ELSE
	BEGIN

		IF(@ApplyType='Valid')
		BEGIN
			SELECT PaThaKa.CompanyRegistrationNo,WholeSaleRetail.WholeSaleRetailNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			UnitLevel WholeSaleRetailUnitLevel,StreetNumberStreetName WholeSaleRetailStreetNumberStreetName,QuarterCityTownship WholeSaleRetailQuarterCityTownship,State WholeSaleRetailState,Country WholeSaleRetailCountry,PostalCode WholeSaleRetailostalCode,
			WholeSaleRetail.IssuedDate,WholeSaleRetail.EndDate
			FROM WholeSaleRetail
			INNER JOIN PaThaKa ON WholeSaleRetail.PaThaKaId = PaThaKa.Id
			WHERE WholeSaleRetail.EndDate>@Date
			AND RegistrationType=@FormType
		END
		ELSE IF(@ApplyType='Invalid')
		BEGIN

			SELECT PaThaKa.CompanyRegistrationNo,WholeSaleRetail.WholeSaleRetailNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			UnitLevel WholeSaleRetailUnitLevel,StreetNumberStreetName WholeSaleRetailStreetNumberStreetName,QuarterCityTownship WholeSaleRetailQuarterCityTownship,State WholeSaleRetailState,Country WholeSaleRetailCountry,PostalCode WholeSaleRetailostalCode,
			WholeSaleRetail.IssuedDate,WholeSaleRetail.EndDate
			FROM WholeSaleRetail
			INNER JOIN PaThaKa ON WholeSaleRetail.PaThaKaId = PaThaKa.Id
			WHERE WholeSaleRetail.EndDate<@Date
			AND RegistrationType=@FormType
		END
		ELSE
		BEGIN
			SELECT PaThaKa.CompanyRegistrationNo,WholeSaleRetail.WholeSaleRetailNo,PaThaKa.CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			UnitLevel WholeSaleRetailUnitLevel,StreetNumberStreetName WholeSaleRetailStreetNumberStreetName,QuarterCityTownship WholeSaleRetailQuarterCityTownship,State WholeSaleRetailState,Country WholeSaleRetailCountry,PostalCode WholeSaleRetailostalCode,
			WholeSaleRetail.IssuedDate,WholeSaleRetail.EndDate
			FROM WholeSaleRetail
			INNER JOIN PaThaKa ON WholeSaleRetail.PaThaKaId = PaThaKa.Id
			INNER JOIN WholeSaleRetailRegistration ON WholeSaleRetail.WholeSaleRetailNo = WholeSaleRetailRegistration.WholeSaleRetailNo
			WHERE ApplyType=@ApplyType AND WholeSaleRetailRegistration.Status='Approved' AND WholeSaleRetail.RegistrationType=@FormType
			AND (WholeSaleRetailRegistration.CreatedDate>=@FromDate AND WholeSaleRetailRegistration.CreatedDate<=@ToDate)	
		END

		
	END
END

GO

/* dbo.sp_WineImportationByPaThaKaReport */
CREATE PROCEDURE [dbo].[sp_WineImportationByPaThaKaReport] 
   @CompanyRegistrationNo nvarchar(20)   
AS   

SELECT CompanyRegistrationNo,WineImportation.WineImportationNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			WineImportation.Name,dbo.fn_GetNRCNo(WineImportation.NRCType,WineImportation.NRCPrefixId,WineImportation.NRCPrefixCodeId,WineImportation.NRCNo) NRCNo,
			WineImportation.FL11Name,dbo.fn_GetNRCNo(WineImportation.FL11NRCType,WineImportation.FL11NRCPrefixId,WineImportation.FL11NRCPrefixCodeId,WineImportation.FL11NRCNo) FL11NRCNo,
			WineImportation.FL4Name,dbo.fn_GetNRCNo(WineImportation.FL4NRCType,WineImportation.FL4NRCPrefixId,WineImportation.FL4NRCPrefixCodeId,WineImportation.FL4NRCNo) FL4NRCNo,
			WineImportation.FL5Name,dbo.fn_GetNRCNo(WineImportation.FL5NRCType,WineImportation.FL5NRCPrefixId,WineImportation.FL5NRCPrefixCodeId,WineImportation.FL5NRCNo) FL5NRCNo,
			 (  
			   SELECT ','+wineType.Name  
			   FROM WineType wineType  
			   WHERE ','+WineImportation.WineTypeId+',' LIKE '%,'+CAST(wineType.Id as nvarchar(20)) +',%'  
			   for xml path(''), type  
			  ).value('substring(text()[1], 2)', 'varchar(max)') as WineType,
			WineImportation.IssuedDate,WineImportation.EndDate
			FROM WineImportation
			INNER JOIN PaThaKa ON WineImportation.PaThaKaId = PaThaKa.Id
			WHERE CompanyRegistrationNo=@CompanyRegistrationNo


		

GO

/* dbo.sp_WineImportationRegistrationReport */
CREATE PROCEDURE [dbo].[sp_WineImportationRegistrationReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@PaymentType nvarchar(50),
	@ApplyType nvarchar(50)
AS
BEGIN
	SELECT WineImportationRegistration.CreatedDate Date,PaThaKa.CompanyRegistrationNo,PaThaKa.CompanyName,
	UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
	WineImportationRegistration.WineImportationNo,WineImportationRegistration.Name,dbo.fn_GetNRCNo(WineImportationRegistration.NRCType,WineImportationRegistration.NRCPrefixId,WineImportationRegistration.NRCPrefixCodeId,WineImportationRegistration.NRCNo) NRCNo,
	WineImportationRegistration.FL11Name,dbo.fn_GetNRCNo(WineImportationRegistration.FL11NRCType,WineImportationRegistration.FL11NRCPrefixId,WineImportationRegistration.FL11NRCPrefixCodeId,WineImportationRegistration.FL11NRCNo) FL11NRCNo,
	WineImportationRegistration.FL4Name,dbo.fn_GetNRCNo(WineImportationRegistration.FL4NRCType,WineImportationRegistration.FL4NRCPrefixId,WineImportationRegistration.FL4NRCPrefixCodeId,WineImportationRegistration.FL4NRCNo) FL4NRCNo,
	WineImportationRegistration.FL5Name,dbo.fn_GetNRCNo(WineImportationRegistration.FL5NRCType,WineImportationRegistration.FL5NRCPrefixId,WineImportationRegistration.FL5NRCPrefixCodeId,WineImportationRegistration.FL5NRCNo) FL5NRCNo,
		(  
		SELECT ','+wineType.Name  
		FROM WineType wineType  
		WHERE ','+WineImportationRegistration.WineTypeId+',' LIKE '%,'+CAST(wineType.Id as nvarchar(20)) +',%'  
		for xml path(''), type  
		).value('substring(text()[1], 2)', 'varchar(max)') as WineType,
	PaymentType,VoucherNo,VoucherDate,AccountTransaction.TotalAmount as TotalAmount
	FROM WineImportationRegistration
	INNER JOIN PaThaKa ON WineImportationRegistration.PaThaKaId = PaThaKa.Id
	INNER JOIN AccountTransaction ON WineImportationRegistration.Id = AccountTransaction.TransactionId
	WHERE WineImportationRegistration.ApplyType=@ApplyType AND WineImportationRegistration.Status='Approved' AND IsPayment=1
	AND AccountTransaction.PaymentType=(CASE WHEN @PaymentType='' then AccountTransaction.PaymentType ELSE @PaymentType END)
	AND (WineImportationRegistration.CreatedDate>=@FromDate AND WineImportationRegistration.CreatedDate<=@ToDate)
END

GO

/* dbo.sp_WineImportationReport */
-- =============================================
-- Author:		Name
-- Create date: 
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[sp_WineImportationReport] 
	-- Add the parameters for the stored procedure here
	@FromDate datetime,
	@ToDate datetime,
	@Date datetime,
	@ApplyType nvarchar(20),
	@Type nvarchar(20) --Summary,Detail
AS
BEGIN
	IF(@Type='Summary')
	BEGIN
		SELECT Count(Id) ApplicationCount,'New' ApplyType FROM WineImportationRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='New' AND Status='Approved'
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Cancel' ApplyType FROM WineImportationRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Cancel' AND Status='Approved'
		GROUP BY ApplyType
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Extension' ApplyType FROM WineImportationRegistration
		WHERE (CreatedDate>=@FromDate AND CreatedDate<=@ToDate)
		AND ApplyType='Extension' AND Status='Approved'
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Valid' ApplyType FROM WineImportation
		WHERE (EndDate>@Date)
		UNION ALL
		SELECT Count(Id) ApplicationCount,'Invalid' ApplyType FROM WineImportation
		WHERE (EndDate<@Date)
	END
	ELSE
	BEGIN

		IF(@ApplyType='Valid')
		BEGIN
			SELECT CompanyRegistrationNo,WineImportation.WineImportationNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			WineImportation.Name,dbo.fn_GetNRCNo(WineImportation.NRCType,WineImportation.NRCPrefixId,WineImportation.NRCPrefixCodeId,WineImportation.NRCNo) NRCNo,
			WineImportation.FL11Name,dbo.fn_GetNRCNo(WineImportation.FL11NRCType,WineImportation.FL11NRCPrefixId,WineImportation.FL11NRCPrefixCodeId,WineImportation.FL11NRCNo) FL11NRCNo,
			WineImportation.FL4Name,dbo.fn_GetNRCNo(WineImportation.FL4NRCType,WineImportation.FL4NRCPrefixId,WineImportation.FL4NRCPrefixCodeId,WineImportation.FL4NRCNo) FL4NRCNo,
			WineImportation.FL5Name,dbo.fn_GetNRCNo(WineImportation.FL5NRCType,WineImportation.FL5NRCPrefixId,WineImportation.FL5NRCPrefixCodeId,WineImportation.FL5NRCNo) FL5NRCNo,
			 (  
			   SELECT ','+wineType.Name  
			   FROM WineType wineType  
			   WHERE ','+WineImportation.WineTypeId+',' LIKE '%,'+CAST(wineType.Id as nvarchar(20)) +',%'  
			   for xml path(''), type  
			  ).value('substring(text()[1], 2)', 'varchar(max)') as WineType,
			WineImportation.IssuedDate,WineImportation.EndDate
			FROM WineImportation
			INNER JOIN PaThaKa ON WineImportation.PaThaKaId = PaThaKa.Id
			WHERE WineImportation.EndDate>@Date
		END
		ELSE IF(@ApplyType='Invalid')
		BEGIN
			SELECT CompanyRegistrationNo,WineImportation.WineImportationNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			WineImportation.Name,dbo.fn_GetNRCNo(WineImportation.NRCType,WineImportation.NRCPrefixId,WineImportation.NRCPrefixCodeId,WineImportation.NRCNo) NRCNo,
			WineImportation.FL11Name,dbo.fn_GetNRCNo(WineImportation.FL11NRCType,WineImportation.FL11NRCPrefixId,WineImportation.FL11NRCPrefixCodeId,WineImportation.FL11NRCNo) FL11NRCNo,
			WineImportation.FL4Name,dbo.fn_GetNRCNo(WineImportation.FL4NRCType,WineImportation.FL4NRCPrefixId,WineImportation.FL4NRCPrefixCodeId,WineImportation.FL4NRCNo) FL4NRCNo,
			WineImportation.FL5Name,dbo.fn_GetNRCNo(WineImportation.FL5NRCType,WineImportation.FL5NRCPrefixId,WineImportation.FL5NRCPrefixCodeId,WineImportation.FL5NRCNo) FL5NRCNo,
			 (  
			   SELECT ','+wineType.Name  
			   FROM WineType wineType  
			   WHERE ','+WineImportation.WineTypeId+',' LIKE '%,'+CAST(wineType.Id as nvarchar(20)) +',%'  
			   for xml path(''), type  
			  ).value('substring(text()[1], 2)', 'varchar(max)') as WineType,
			WineImportation.IssuedDate,WineImportation.EndDate
			FROM WineImportation
			INNER JOIN PaThaKa ON WineImportation.PaThaKaId = PaThaKa.Id
			WHERE WineImportation.EndDate<@Date
		END
		ELSE
		BEGIN
			SELECT CompanyRegistrationNo,WineImportation.WineImportationNo,CompanyName, 
			UnitLevel,StreetNumberStreetName,QuarterCityTownship,State,Country,PostalCode,
			WineImportation.Name,dbo.fn_GetNRCNo(WineImportation.NRCType,WineImportation.NRCPrefixId,WineImportation.NRCPrefixCodeId,WineImportation.NRCNo) NRCNo,
			WineImportation.FL11Name,dbo.fn_GetNRCNo(WineImportation.FL11NRCType,WineImportation.FL11NRCPrefixId,WineImportation.FL11NRCPrefixCodeId,WineImportation.FL11NRCNo) FL11NRCNo,
			WineImportation.FL4Name,dbo.fn_GetNRCNo(WineImportation.FL4NRCType,WineImportation.FL4NRCPrefixId,WineImportation.FL4NRCPrefixCodeId,WineImportation.FL4NRCNo) FL4NRCNo,
			WineImportation.FL5Name,dbo.fn_GetNRCNo(WineImportation.FL5NRCType,WineImportation.FL5NRCPrefixId,WineImportation.FL5NRCPrefixCodeId,WineImportation.FL5NRCNo) FL5NRCNo,
			 (  
			   SELECT ','+wineType.Name  
			   FROM WineType wineType  
			   WHERE ','+WineImportation.WineTypeId+',' LIKE '%,'+CAST(wineType.Id as nvarchar(20)) +',%'  
			   for xml path(''), type  
			  ).value('substring(text()[1], 2)', 'varchar(max)') as WineType,
			WineImportation.IssuedDate,WineImportation.EndDate
			FROM WineImportation
			INNER JOIN PaThaKa ON WineImportation.PaThaKaId = PaThaKa.Id
			INNER JOIN WineImportationRegistration ON WineImportation.WineImportationNo = WineImportationRegistration.WineImportationNo
			WHERE ApplyType=@ApplyType AND WineImportationRegistration.Status='Approved'
			AND (WineImportationRegistration.CreatedDate>=@FromDate AND WineImportationRegistration.CreatedDate<=@ToDate)	
		END

		
	END
END

GO

/* dbo.TempGetAppID */

    CREATE PROCEDURE dbo.TempGetAppID
    @appName    tAppName,
    @appId      int OUTPUT
    AS
    SET @appName = LOWER(@appName)
    SET @appId = NULL

    SELECT @appId = AppId
    FROM [TradeNetDBTest].dbo.ASPStateTempApplications
    WHERE AppName = @appName

    IF @appId IS NULL BEGIN
        BEGIN TRAN        

        SELECT @appId = AppId
        FROM [TradeNetDBTest].dbo.ASPStateTempApplications WITH (TABLOCKX)
        WHERE AppName = @appName
        
        IF @appId IS NULL
        BEGIN
            EXEC GetHashCode @appName, @appId OUTPUT
            
            INSERT [TradeNetDBTest].dbo.ASPStateTempApplications
            VALUES
            (@appId, @appName)
            
            IF @@ERROR = 2627 
            BEGIN
                DECLARE @dupApp tAppName
            
                SELECT @dupApp = RTRIM(AppName)
                FROM [TradeNetDBTest].dbo.ASPStateTempApplications 
                WHERE AppId = @appId
                
                RAISERROR('SQL session state fatal error: hash-code collision between applications ''%s'' and ''%s''. Please rename the 1st application to resolve the problem.', 
                            18, 1, @appName, @dupApp)
            END
        END

        COMMIT
    END

    RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    
GO

/* dbo.TempGetStateItem */

        CREATE PROCEDURE dbo.TempGetStateItem
            @id         tSessionId,
            @itemShort  tSessionItemShort OUTPUT,
            @locked     bit OUTPUT,
            @lockDate   datetime OUTPUT,
            @lockCookie int OUTPUT
        AS
            DECLARE @textptr AS tTextPtr
            DECLARE @length AS int
            DECLARE @now AS datetime
            SET @now = GETUTCDATE()

            UPDATE [TradeNetDBTest].dbo.ASPStateTempSessions
            SET Expires = DATEADD(n, Timeout, @now), 
                @locked = Locked,
                @lockDate = LockDateLocal,
                @lockCookie = LockCookie,
                @itemShort = CASE @locked
                    WHEN 0 THEN SessionItemShort
                    ELSE NULL
                    END,
                @textptr = CASE @locked
                    WHEN 0 THEN TEXTPTR(SessionItemLong)
                    ELSE NULL
                    END,
                @length = CASE @locked
                    WHEN 0 THEN DATALENGTH(SessionItemLong)
                    ELSE NULL
                    END
            WHERE SessionId = @id
            IF @length IS NOT NULL BEGIN
                READTEXT [TradeNetDBTest].dbo.ASPStateTempSessions.SessionItemLong @textptr 0 @length
            END

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       
GO

/* dbo.TempGetStateItem2 */

        CREATE PROCEDURE dbo.TempGetStateItem2
            @id         tSessionId,
            @itemShort  tSessionItemShort OUTPUT,
            @locked     bit OUTPUT,
            @lockAge    int OUTPUT,
            @lockCookie int OUTPUT
        AS
            DECLARE @textptr AS tTextPtr
            DECLARE @length AS int
            DECLARE @now AS datetime
            SET @now = GETUTCDATE()

            UPDATE [TradeNetDBTest].dbo.ASPStateTempSessions
            SET Expires = DATEADD(n, Timeout, @now), 
                @locked = Locked,
                @lockAge = DATEDIFF(second, LockDate, @now),
                @lockCookie = LockCookie,
                @itemShort = CASE @locked
                    WHEN 0 THEN SessionItemShort
                    ELSE NULL
                    END,
                @textptr = CASE @locked
                    WHEN 0 THEN TEXTPTR(SessionItemLong)
                    ELSE NULL
                    END,
                @length = CASE @locked
                    WHEN 0 THEN DATALENGTH(SessionItemLong)
                    ELSE NULL
                    END
            WHERE SessionId = @id
            IF @length IS NOT NULL BEGIN
                READTEXT [TradeNetDBTest].dbo.ASPStateTempSessions.SessionItemLong @textptr 0 @length
            END

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         
GO

/* dbo.TempGetStateItem3 */

        CREATE PROCEDURE dbo.TempGetStateItem3
            @id         tSessionId,
            @itemShort  tSessionItemShort OUTPUT,
            @locked     bit OUTPUT,
            @lockAge    int OUTPUT,
            @lockCookie int OUTPUT,
            @actionFlags int OUTPUT
        AS
            DECLARE @textptr AS tTextPtr
            DECLARE @length AS int
            DECLARE @now AS datetime
            SET @now = GETUTCDATE()

            UPDATE [TradeNetDBTest].dbo.ASPStateTempSessions
            SET Expires = DATEADD(n, Timeout, @now), 
                @locked = Locked,
                @lockAge = DATEDIFF(second, LockDate, @now),
                @lockCookie = LockCookie,
                @itemShort = CASE @locked
                    WHEN 0 THEN SessionItemShort
                    ELSE NULL
                    END,
                @textptr = CASE @locked
                    WHEN 0 THEN TEXTPTR(SessionItemLong)
                    ELSE NULL
                    END,
                @length = CASE @locked
                    WHEN 0 THEN DATALENGTH(SessionItemLong)
                    ELSE NULL
                    END,

                /* If the Uninitialized flag (0x1) if it is set,
                   remove it and return InitializeItem (0x1) in actionFlags */
                Flags = CASE
                    WHEN (Flags & 1) <> 0 THEN (Flags & ~1)
                    ELSE Flags
                    END,
                @actionFlags = CASE
                    WHEN (Flags & 1) <> 0 THEN 1
                    ELSE 0
                    END
            WHERE SessionId = @id
            IF @length IS NOT NULL BEGIN
                READTEXT [TradeNetDBTest].dbo.ASPStateTempSessions.SessionItemLong @textptr 0 @length
            END

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         
GO

/* dbo.TempGetStateItemExclusive */

        CREATE PROCEDURE dbo.TempGetStateItemExclusive
            @id         tSessionId,
            @itemShort  tSessionItemShort OUTPUT,
            @locked     bit OUTPUT,
            @lockDate   datetime OUTPUT,
            @lockCookie int OUTPUT
        AS
            DECLARE @textptr AS tTextPtr
            DECLARE @length AS int
            DECLARE @now AS datetime
            DECLARE @nowLocal AS datetime

            SET @now = GETUTCDATE()
            SET @nowLocal = GETDATE()
            
            UPDATE [TradeNetDBTest].dbo.ASPStateTempSessions
            SET Expires = DATEADD(n, Timeout, @now), 
                LockDate = CASE Locked
                    WHEN 0 THEN @now
                    ELSE LockDate
                    END,
                @lockDate = LockDateLocal = CASE Locked
                    WHEN 0 THEN @nowLocal
                    ELSE LockDateLocal
                    END,
                @lockCookie = LockCookie = CASE Locked
                    WHEN 0 THEN LockCookie + 1
                    ELSE LockCookie
                    END,
                @itemShort = CASE Locked
                    WHEN 0 THEN SessionItemShort
                    ELSE NULL
                    END,
                @textptr = CASE Locked
                    WHEN 0 THEN TEXTPTR(SessionItemLong)
                    ELSE NULL
                    END,
                @length = CASE Locked
                    WHEN 0 THEN DATALENGTH(SessionItemLong)
                    ELSE NULL
                    END,
                @locked = Locked,
                Locked = 1
            WHERE SessionId = @id
            IF @length IS NOT NULL BEGIN
                READTEXT [TradeNetDBTest].dbo.ASPStateTempSessions.SessionItemLong @textptr 0 @length
            END

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 
GO

/* dbo.TempGetStateItemExclusive2 */

        CREATE PROCEDURE dbo.TempGetStateItemExclusive2
            @id         tSessionId,
            @itemShort  tSessionItemShort OUTPUT,
            @locked     bit OUTPUT,
            @lockAge    int OUTPUT,
            @lockCookie int OUTPUT
        AS
            DECLARE @textptr AS tTextPtr
            DECLARE @length AS int
            DECLARE @now AS datetime
            DECLARE @nowLocal AS datetime

            SET @now = GETUTCDATE()
            SET @nowLocal = GETDATE()
            
            UPDATE [TradeNetDBTest].dbo.ASPStateTempSessions
            SET Expires = DATEADD(n, Timeout, @now), 
                LockDate = CASE Locked
                    WHEN 0 THEN @now
                    ELSE LockDate
                    END,
                LockDateLocal = CASE Locked
                    WHEN 0 THEN @nowLocal
                    ELSE LockDateLocal
                    END,
                @lockAge = CASE Locked
                    WHEN 0 THEN 0
                    ELSE DATEDIFF(second, LockDate, @now)
                    END,
                @lockCookie = LockCookie = CASE Locked
                    WHEN 0 THEN LockCookie + 1
                    ELSE LockCookie
                    END,
                @itemShort = CASE Locked
                    WHEN 0 THEN SessionItemShort
                    ELSE NULL
                    END,
                @textptr = CASE Locked
                    WHEN 0 THEN TEXTPTR(SessionItemLong)
                    ELSE NULL
                    END,
                @length = CASE Locked
                    WHEN 0 THEN DATALENGTH(SessionItemLong)
                    ELSE NULL
                    END,
                @locked = Locked,
                Locked = 1
            WHERE SessionId = @id
            IF @length IS NOT NULL BEGIN
                READTEXT [TradeNetDBTest].dbo.ASPStateTempSessions.SessionItemLong @textptr 0 @length
            END

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     
GO

/* dbo.TempGetStateItemExclusive3 */

        CREATE PROCEDURE dbo.TempGetStateItemExclusive3
            @id         tSessionId,
            @itemShort  tSessionItemShort OUTPUT,
            @locked     bit OUTPUT,
            @lockAge    int OUTPUT,
            @lockCookie int OUTPUT,
            @actionFlags int OUTPUT
        AS
            DECLARE @textptr AS tTextPtr
            DECLARE @length AS int
            DECLARE @now AS datetime
            DECLARE @nowLocal AS datetime

            SET @now = GETUTCDATE()
            SET @nowLocal = GETDATE()
            
            UPDATE [TradeNetDBTest].dbo.ASPStateTempSessions
            SET Expires = DATEADD(n, Timeout, @now), 
                LockDate = CASE Locked
                    WHEN 0 THEN @now
                    ELSE LockDate
                    END,
                LockDateLocal = CASE Locked
                    WHEN 0 THEN @nowLocal
                    ELSE LockDateLocal
                    END,
                @lockAge = CASE Locked
                    WHEN 0 THEN 0
                    ELSE DATEDIFF(second, LockDate, @now)
                    END,
                @lockCookie = LockCookie = CASE Locked
                    WHEN 0 THEN LockCookie + 1
                    ELSE LockCookie
                    END,
                @itemShort = CASE Locked
                    WHEN 0 THEN SessionItemShort
                    ELSE NULL
                    END,
                @textptr = CASE Locked
                    WHEN 0 THEN TEXTPTR(SessionItemLong)
                    ELSE NULL
                    END,
                @length = CASE Locked
                    WHEN 0 THEN DATALENGTH(SessionItemLong)
                    ELSE NULL
                    END,
                @locked = Locked,
                Locked = 1,

                /* If the Uninitialized flag (0x1) if it is set,
                   remove it and return InitializeItem (0x1) in actionFlags */
                Flags = CASE
                    WHEN (Flags & 1) <> 0 THEN (Flags & ~1)
                    ELSE Flags
                    END,
                @actionFlags = CASE
                    WHEN (Flags & 1) <> 0 THEN 1
                    ELSE 0
                    END
            WHERE SessionId = @id
            IF @length IS NOT NULL BEGIN
                READTEXT [TradeNetDBTest].dbo.ASPStateTempSessions.SessionItemLong @textptr 0 @length
            END

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     
GO

/* dbo.TempGetVersion */

/*****************************************************************************/

CREATE PROCEDURE dbo.TempGetVersion
    @ver      char(10) OUTPUT
AS
    SELECT @ver = "2"
    RETURN 0
GO

/* dbo.TempInsertStateItemLong */

        CREATE PROCEDURE dbo.TempInsertStateItemLong
            @id         tSessionId,
            @itemLong   tSessionItemLong,
            @timeout    int
        AS    
            DECLARE @now AS datetime
            DECLARE @nowLocal AS datetime
            
            SET @now = GETUTCDATE()
            SET @nowLocal = GETDATE()

            INSERT [TradeNetDBTest].dbo.ASPStateTempSessions 
                (SessionId, 
                 SessionItemLong, 
                 Timeout, 
                 Expires, 
                 Locked, 
                 LockDate,
                 LockDateLocal,
                 LockCookie) 
            VALUES 
                (@id, 
                 @itemLong, 
                 @timeout, 
                 DATEADD(n, @timeout, @now), 
                 0, 
                 @now,
                 @nowLocal,
                 1)

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                
GO

/* dbo.TempInsertStateItemShort */

        CREATE PROCEDURE dbo.TempInsertStateItemShort
            @id         tSessionId,
            @itemShort  tSessionItemShort,
            @timeout    int
        AS    

            DECLARE @now AS datetime
            DECLARE @nowLocal AS datetime
            
            SET @now = GETUTCDATE()
            SET @nowLocal = GETDATE()

            INSERT [TradeNetDBTest].dbo.ASPStateTempSessions 
                (SessionId, 
                 SessionItemShort, 
                 Timeout, 
                 Expires, 
                 Locked, 
                 LockDate,
                 LockDateLocal,
                 LockCookie) 
            VALUES 
                (@id, 
                 @itemShort, 
                 @timeout, 
                 DATEADD(n, @timeout, @now), 
                 0, 
                 @now,
                 @nowLocal,
                 1)

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           
GO

/* dbo.TempInsertUninitializedItem */

        CREATE PROCEDURE dbo.TempInsertUninitializedItem
            @id         tSessionId,
            @itemShort  tSessionItemShort,
            @timeout    int
        AS    

            DECLARE @now AS datetime
            DECLARE @nowLocal AS datetime
            
            SET @now = GETUTCDATE()
            SET @nowLocal = GETDATE()

            INSERT [TradeNetDBTest].dbo.ASPStateTempSessions 
                (SessionId, 
                 SessionItemShort, 
                 Timeout, 
                 Expires, 
                 Locked, 
                 LockDate,
                 LockDateLocal,
                 LockCookie,
                 Flags) 
            VALUES 
                (@id, 
                 @itemShort, 
                 @timeout, 
                 DATEADD(n, @timeout, @now), 
                 0, 
                 @now,
                 @nowLocal,
                 1,
                 1)

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            
GO

/* dbo.TempReleaseStateItemExclusive */

        CREATE PROCEDURE dbo.TempReleaseStateItemExclusive
            @id         tSessionId,
            @lockCookie int
        AS
            UPDATE [TradeNetDBTest].dbo.ASPStateTempSessions
            SET Expires = DATEADD(n, Timeout, GETUTCDATE()), 
                Locked = 0
            WHERE SessionId = @id AND LockCookie = @lockCookie

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               
GO

/* dbo.TempRemoveStateItem */

    CREATE PROCEDURE dbo.TempRemoveStateItem
        @id     tSessionId,
        @lockCookie int
    AS
        DELETE [TradeNetDBTest].dbo.ASPStateTempSessions
        WHERE SessionId = @id AND LockCookie = @lockCookie
        RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   
GO

/* dbo.TempResetTimeout */

        CREATE PROCEDURE dbo.TempResetTimeout
            @id     tSessionId
        AS
            UPDATE [TradeNetDBTest].dbo.ASPStateTempSessions
            SET Expires = DATEADD(n, Timeout, GETUTCDATE())
            WHERE SessionId = @id
            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        
GO

/* dbo.TempUpdateStateItemLong */

        CREATE PROCEDURE dbo.TempUpdateStateItemLong
            @id         tSessionId,
            @itemLong   tSessionItemLong,
            @timeout    int,
            @lockCookie int
        AS    
            UPDATE [TradeNetDBTest].dbo.ASPStateTempSessions
            SET Expires = DATEADD(n, @timeout, GETUTCDATE()), 
                SessionItemLong = @itemLong,
                Timeout = @timeout,
                Locked = 0
            WHERE SessionId = @id AND LockCookie = @lockCookie

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        
GO

/* dbo.TempUpdateStateItemLongNullShort */

        CREATE PROCEDURE dbo.TempUpdateStateItemLongNullShort
            @id         tSessionId,
            @itemLong   tSessionItemLong,
            @timeout    int,
            @lockCookie int
        AS    
            UPDATE [TradeNetDBTest].dbo.ASPStateTempSessions
            SET Expires = DATEADD(n, @timeout, GETUTCDATE()), 
                SessionItemLong = @itemLong, 
                SessionItemShort = NULL,
                Timeout = @timeout,
                Locked = 0
            WHERE SessionId = @id AND LockCookie = @lockCookie

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     
GO

/* dbo.TempUpdateStateItemShort */

        CREATE PROCEDURE dbo.TempUpdateStateItemShort
            @id         tSessionId,
            @itemShort  tSessionItemShort,
            @timeout    int,
            @lockCookie int
        AS    
            UPDATE [TradeNetDBTest].dbo.ASPStateTempSessions
            SET Expires = DATEADD(n, @timeout, GETUTCDATE()), 
                SessionItemShort = @itemShort, 
                Timeout = @timeout,
                Locked = 0
            WHERE SessionId = @id AND LockCookie = @lockCookie

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   
GO

/* dbo.TempUpdateStateItemShortNullLong */

        CREATE PROCEDURE dbo.TempUpdateStateItemShortNullLong
            @id         tSessionId,
            @itemShort  tSessionItemShort,
            @timeout    int,
            @lockCookie int
        AS    
            UPDATE [TradeNetDBTest].dbo.ASPStateTempSessions
            SET Expires = DATEADD(n, @timeout, GETUTCDATE()), 
                SessionItemShort = @itemShort, 
                SessionItemLong = NULL, 
                Timeout = @timeout,
                Locked = 0
            WHERE SessionId = @id AND LockCookie = @lockCookie

            RETURN 0                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  
GO

/* dbo.UpdateAdditionalDescription */
Create PROCEDURE [dbo].[UpdateAdditionalDescription]
    @Id uniqueidentifier,
    @Status nvarchar(450),
    @Code nvarchar(450),
    @HSCode nvarchar(450),
    @AdditionalDescription nvarchar(450),
    @Remark nvarchar(450),
    @RecommendPrice nvarchar(450),
    @RequestDate datetime,
    @Message nvarchar(450),
    @MemberId int,
    @MemberName nvarchar(450)
AS
BEGIN
    -- Update the status in the RequestAutoApproveDescription table
    UPDATE RequestAutoApproveDescription
    SET Status = @Status
    WHERE id = @Id
    
    -- Insert a new row into the AdditionalDescription table if the status is Approve
    IF @Status = 'Approve'
    BEGIN
        DECLARE @ADCode varchar(450) = @Code
        
        INSERT INTO AdditionalDescription (id, HSCode, ADCode, AdditionalDescription)
        VALUES (NEWID(), @HSCode, @ADCode, @AdditionalDescription)
    END
END

GO

/* dbo.UpdateAdditionalDescriptionImport */
CREATE PROCEDURE [dbo].[UpdateAdditionalDescriptionImport]
    @Id uniqueidentifier,
    @Status nvarchar(450),
    @Code nvarchar(450),
    @HSCode nvarchar(450),
    @AdditionalDescription nvarchar(450),
    @Remark nvarchar(450),
    @RecommendPrice nvarchar(450),
    @RequestDate datetime,
    @Message nvarchar(450),
    @MemberId int,
    @MemberName nvarchar(450)
AS
BEGIN
    -- Update the status in the RequestAutoApproveDescription table
    UPDATE RequestAutoApproveDescriptionImport
    SET Status = @Status
    WHERE id = @Id
    
    -- Insert a new row into the AdditionalDescription table if the status is Approve
    IF @Status = 'Approve'
    BEGIN
        DECLARE @ADCode varchar(450) = @Code
        
        INSERT INTO AdditionalDescriptionImport (id, HSCode, ADCode, AdditionalDescription)
        VALUES (NEWID(), @HSCode, @ADCode, @AdditionalDescription)
    END
END

GO

