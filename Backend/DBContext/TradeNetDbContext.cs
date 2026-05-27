using System;
using System.Collections.Generic;
using API.Model.TradeNet;
using Microsoft.EntityFrameworkCore;

namespace API.DBContext;

public partial class TradeNetDbContext : DbContext
{
    public TradeNetDbContext(DbContextOptions<TradeNetDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AccountTitle> AccountTitles { get; set; }

    public virtual DbSet<AccountTransaction> AccountTransactions { get; set; }

    public virtual DbSet<AccountTransactionAutoGenerate> AccountTransactionAutoGenerates { get; set; }

    public virtual DbSet<AccountTransactionDetail> AccountTransactionDetails { get; set; }

    public virtual DbSet<AccountType> AccountTypes { get; set; }

    public virtual DbSet<AdditionalDescription> AdditionalDescriptions { get; set; }

    public virtual DbSet<AdditionalDescriptionImport> AdditionalDescriptionImports { get; set; }

    public virtual DbSet<ApierrorLog> ApierrorLogs { get; set; }

    public virtual DbSet<AspstateTempApplication> AspstateTempApplications { get; set; }

    public virtual DbSet<AspstateTempSession> AspstateTempSessions { get; set; }

    public virtual DbSet<BorderExportLicence> BorderExportLicences { get; set; }

    public virtual DbSet<BorderExportLicenceAmend> BorderExportLicenceAmends { get; set; }

    public virtual DbSet<BorderExportLicenceAmendItem> BorderExportLicenceAmendItems { get; set; }

    public virtual DbSet<BorderExportLicenceFile> BorderExportLicenceFiles { get; set; }

    public virtual DbSet<BorderExportLicenceItem> BorderExportLicenceItems { get; set; }

    public virtual DbSet<BorderExportLicenceRecommendation> BorderExportLicenceRecommendations { get; set; }

    public virtual DbSet<BorderExportPermit> BorderExportPermits { get; set; }

    public virtual DbSet<BorderExportPermitAmend> BorderExportPermitAmends { get; set; }

    public virtual DbSet<BorderExportPermitAmendItem> BorderExportPermitAmendItems { get; set; }

    public virtual DbSet<BorderExportPermitFile> BorderExportPermitFiles { get; set; }

    public virtual DbSet<BorderExportPermitItem> BorderExportPermitItems { get; set; }

    public virtual DbSet<BorderExportPermitRecommendation> BorderExportPermitRecommendations { get; set; }

    public virtual DbSet<BorderImportLicence> BorderImportLicences { get; set; }

    public virtual DbSet<BorderImportLicenceAmend> BorderImportLicenceAmends { get; set; }

    public virtual DbSet<BorderImportLicenceAmendItem> BorderImportLicenceAmendItems { get; set; }

    public virtual DbSet<BorderImportLicenceFile> BorderImportLicenceFiles { get; set; }

    public virtual DbSet<BorderImportLicenceItem> BorderImportLicenceItems { get; set; }

    public virtual DbSet<BorderImportLicenceRecommendation> BorderImportLicenceRecommendations { get; set; }

    public virtual DbSet<BorderImportPermit> BorderImportPermits { get; set; }

    public virtual DbSet<BorderImportPermitAmend> BorderImportPermitAmends { get; set; }

    public virtual DbSet<BorderImportPermitAmendItem> BorderImportPermitAmendItems { get; set; }

    public virtual DbSet<BorderImportPermitFile> BorderImportPermitFiles { get; set; }

    public virtual DbSet<BorderImportPermitItem> BorderImportPermitItems { get; set; }

    public virtual DbSet<BorderImportPermitRecommendation> BorderImportPermitRecommendations { get; set; }

    public virtual DbSet<BorderLicenceAutoGenerate> BorderLicenceAutoGenerates { get; set; }

    public virtual DbSet<BusinessServiceAgency> BusinessServiceAgencies { get; set; }

    public virtual DbSet<BusinessServiceAgencyRegistration> BusinessServiceAgencyRegistrations { get; set; }

    public virtual DbSet<BusinessServiceAgencyRegistrationAmend> BusinessServiceAgencyRegistrationAmends { get; set; }

    public virtual DbSet<BusinessServiceAgencyRegistrationFile> BusinessServiceAgencyRegistrationFiles { get; set; }

    public virtual DbSet<BusinessType> BusinessTypes { get; set; }

    public virtual DbSet<CarBrand> CarBrands { get; set; }

    public virtual DbSet<CarDetail> CarDetails { get; set; }

    public virtual DbSet<CarEnginePower> CarEnginePowers { get; set; }

    public virtual DbSet<CarGroup> CarGroups { get; set; }

    public virtual DbSet<CarModelYear> CarModelYears { get; set; }

    public virtual DbSet<CarModelYearGroup> CarModelYearGroups { get; set; }

    public virtual DbSet<CarPermitType> CarPermitTypes { get; set; }

    public virtual DbSet<CarSubBrand> CarSubBrands { get; set; }

    public virtual DbSet<CardAutoGenerate> CardAutoGenerates { get; set; }

    public virtual DbSet<CardBudgetYearAutoGenerate> CardBudgetYearAutoGenerates { get; set; }

    public virtual DbSet<CardRegistrationFee> CardRegistrationFees { get; set; }

    public virtual DbSet<CardType> CardTypes { get; set; }

    public virtual DbSet<ChequeNo> ChequeNos { get; set; }

    public virtual DbSet<CitizenPayLog> CitizenPayLogs { get; set; }

    public virtual DbSet<CodePrefix> CodePrefixes { get; set; }

    public virtual DbSet<Company> Companies { get; set; }

    public virtual DbSet<Contactu> Contactus { get; set; }

    public virtual DbSet<Content> Contents { get; set; }

    public virtual DbSet<Country> Countries { get; set; }

    public virtual DbSet<Currency> Currencies { get; set; }

    public virtual DbSet<CycleBrand> CycleBrands { get; set; }

    public virtual DbSet<DataUpdateHistory> DataUpdateHistories { get; set; }

    public virtual DbSet<DecisionCode> DecisionCodes { get; set; }

    public virtual DbSet<DeleteDatum> DeleteData { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<DicaLog> DicaLogs { get; set; }

    public virtual DbSet<Dicadirector> Dicadirectors { get; set; }

    public virtual DbSet<DocumentType> DocumentTypes { get; set; }

    public virtual DbSet<DutyFreeShop> DutyFreeShops { get; set; }

    public virtual DbSet<DutyFreeShopRegistration> DutyFreeShopRegistrations { get; set; }

    public virtual DbSet<DutyFreeShopRegistrationAmend> DutyFreeShopRegistrationAmends { get; set; }

    public virtual DbSet<DutyFreeShopRegistrationFile> DutyFreeShopRegistrationFiles { get; set; }

    public virtual DbSet<Ediapilog> Ediapilogs { get; set; }

    public virtual DbSet<Edilog> Edilogs { get; set; }

    public virtual DbSet<EiccattachFile> EiccattachFiles { get; set; }

    public virtual DbSet<Eicccertificate> Eicccertificates { get; set; }

    public virtual DbSet<EicccertificateHistory> EicccertificateHistories { get; set; }

    public virtual DbSet<Eiccno> Eiccnos { get; set; }

    public virtual DbSet<EmailLog> EmailLogs { get; set; }

    public virtual DbSet<EvcycleShowRoom> EvcycleShowRooms { get; set; }

    public virtual DbSet<EvcycleShowRoomRegistration> EvcycleShowRoomRegistrations { get; set; }

    public virtual DbSet<EvcycleShowRoomRegistrationAmend> EvcycleShowRoomRegistrationAmends { get; set; }

    public virtual DbSet<EvcycleShowRoomRegistrationFile> EvcycleShowRoomRegistrationFiles { get; set; }

    public virtual DbSet<EvshowRoom> EvshowRooms { get; set; }

    public virtual DbSet<EvshowRoomRegistration> EvshowRoomRegistrations { get; set; }

    public virtual DbSet<EvshowRoomRegistrationAmend> EvshowRoomRegistrationAmends { get; set; }

    public virtual DbSet<EvshowRoomRegistrationFile> EvshowRoomRegistrationFiles { get; set; }

    public virtual DbSet<ExchangeRate> ExchangeRates { get; set; }

    public virtual DbSet<ExportImportCommodityType> ExportImportCommodityTypes { get; set; }

    public virtual DbSet<ExportImportIncoterm> ExportImportIncoterms { get; set; }

    public virtual DbSet<ExportImportMethod> ExportImportMethods { get; set; }

    public virtual DbSet<ExportImportSection> ExportImportSections { get; set; }

    public virtual DbSet<ExportLicence> ExportLicences { get; set; }

    public virtual DbSet<ExportLicenceAmend> ExportLicenceAmends { get; set; }

    public virtual DbSet<ExportLicenceAmendItem> ExportLicenceAmendItems { get; set; }

    public virtual DbSet<ExportLicenceFile> ExportLicenceFiles { get; set; }

    public virtual DbSet<ExportLicenceItem> ExportLicenceItems { get; set; }

    public virtual DbSet<ExportLicenceRecommendation> ExportLicenceRecommendations { get; set; }

    public virtual DbSet<ExportPermit> ExportPermits { get; set; }

    public virtual DbSet<ExportPermitAmend> ExportPermitAmends { get; set; }

    public virtual DbSet<ExportPermitAmendItem> ExportPermitAmendItems { get; set; }

    public virtual DbSet<ExportPermitFile> ExportPermitFiles { get; set; }

    public virtual DbSet<ExportPermitItem> ExportPermitItems { get; set; }

    public virtual DbSet<ExportPermitRecommendation> ExportPermitRecommendations { get; set; }

    public virtual DbSet<Faq> Faqs { get; set; }

    public virtual DbSet<Faqcategory> Faqcategories { get; set; }

    public virtual DbSet<ForgotPassword> ForgotPasswords { get; set; }

    public virtual DbSet<GroupCode> GroupCodes { get; set; }

    public virtual DbSet<Hscode> Hscodes { get; set; }

    public virtual DbSet<HscodeOldBk> HscodeOldBks { get; set; }

    public virtual DbSet<ImportLicence> ImportLicences { get; set; }

    public virtual DbSet<ImportLicenceAmend> ImportLicenceAmends { get; set; }

    public virtual DbSet<ImportLicenceAmendItem> ImportLicenceAmendItems { get; set; }

    public virtual DbSet<ImportLicenceFile> ImportLicenceFiles { get; set; }

    public virtual DbSet<ImportLicenceItem> ImportLicenceItems { get; set; }

    public virtual DbSet<ImportLicenceRecommendation> ImportLicenceRecommendations { get; set; }

    public virtual DbSet<ImportPermit> ImportPermits { get; set; }

    public virtual DbSet<ImportPermitAmend> ImportPermitAmends { get; set; }

    public virtual DbSet<ImportPermitAmendItem> ImportPermitAmendItems { get; set; }

    public virtual DbSet<ImportPermitFile> ImportPermitFiles { get; set; }

    public virtual DbSet<ImportPermitItem> ImportPermitItems { get; set; }

    public virtual DbSet<ImportPermitRecommendation> ImportPermitRecommendations { get; set; }

    public virtual DbSet<IndividualTrading> IndividualTradings { get; set; }

    public virtual DbSet<IndividualTradingBind> IndividualTradingBinds { get; set; }

    public virtual DbSet<IndividualTradingBindFile> IndividualTradingBindFiles { get; set; }

    public virtual DbSet<IndividualTradingRegistration> IndividualTradingRegistrations { get; set; }

    public virtual DbSet<IndividualTradingRegistrationAmend> IndividualTradingRegistrationAmends { get; set; }

    public virtual DbSet<IndividualTradingRegistrationFile> IndividualTradingRegistrationFiles { get; set; }

    public virtual DbSet<Information> Information { get; set; }

    public virtual DbSet<LicenceAutoGenerate> LicenceAutoGenerates { get; set; }

    public virtual DbSet<LicenceFee> LicenceFees { get; set; }

    public virtual DbSet<LicencePermitAmendRemark> LicencePermitAmendRemarks { get; set; }

    public virtual DbSet<LicencePermitApproveHistory> LicencePermitApproveHistories { get; set; }

    public virtual DbSet<LicencePermitApproveUser> LicencePermitApproveUsers { get; set; }

    public virtual DbSet<LicencePermitLimit> LicencePermitLimits { get; set; }

    public virtual DbSet<LicencePermitPeriod> LicencePermitPeriods { get; set; }

    public virtual DbSet<LineofBusiness> LineofBusinesses { get; set; }

    public virtual DbSet<Log4NetLog> Log4NetLogs { get; set; }

    public virtual DbSet<ManifestLicence> ManifestLicences { get; set; }

    public virtual DbSet<Mcblog> Mcblogs { get; set; }

    public virtual DbSet<McbpaymentLog> McbpaymentLogs { get; set; }

    public virtual DbSet<Member> Members { get; set; }

    public virtual DbSet<MemberAutoGenerate> MemberAutoGenerates { get; set; }

    public virtual DbSet<MemberRegistration> MemberRegistrations { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<MessageHistory> MessageHistories { get; set; }

    public virtual DbSet<MpupaymentTransaction> MpupaymentTransactions { get; set; }

    public virtual DbSet<MpupaymentTransactionLog> MpupaymentTransactionLogs { get; set; }

    public virtual DbSet<Nrcprefix> Nrcprefixes { get; set; }

    public virtual DbSet<NrcprefixCode> NrcprefixCodes { get; set; }

    public virtual DbSet<OgaautoGenerate> OgaautoGenerates { get; set; }

    public virtual DbSet<Ogadepartment> Ogadepartments { get; set; }

    public virtual DbSet<Ogarecommendation> Ogarecommendations { get; set; }

    public virtual DbSet<OgarecommendationFile> OgarecommendationFiles { get; set; }

    public virtual DbSet<OgarecommendationHistory> OgarecommendationHistories { get; set; }

    public virtual DbSet<Ogasection> Ogasections { get; set; }

    public virtual DbSet<Ogauser> Ogausers { get; set; }

    public virtual DbSet<OtpforAdminLogin> OtpforAdminLogins { get; set; }

    public virtual DbSet<Otplog> Otplogs { get; set; }

    public virtual DbSet<PaThaKa> PaThaKas { get; set; }

    public virtual DbSet<PaThaKaAmendApidirector> PaThaKaAmendApidirectors { get; set; }

    public virtual DbSet<PaThaKaAmendApilog> PaThaKaAmendApilogs { get; set; }

    public virtual DbSet<PaThaKaApplicationRemoval> PaThaKaApplicationRemovals { get; set; }

    public virtual DbSet<PaThaKaAutoCancelFine> PaThaKaAutoCancelFines { get; set; }

    public virtual DbSet<PaThaKaBind> PaThaKaBinds { get; set; }

    public virtual DbSet<PaThaKaBindFile> PaThaKaBindFiles { get; set; }

    public virtual DbSet<PaThaKaDirector> PaThaKaDirectors { get; set; }

    public virtual DbSet<PaThaKaDirectorsBlackListLog> PaThaKaDirectorsBlackListLogs { get; set; }

    public virtual DbSet<PaThaKaDirectorsRegistration> PaThaKaDirectorsRegistrations { get; set; }

    public virtual DbSet<PaThaKaDirectorsRegistrationAmend> PaThaKaDirectorsRegistrationAmends { get; set; }

    public virtual DbSet<PaThaKaPermitBusiness> PaThaKaPermitBusinesses { get; set; }

    public virtual DbSet<PaThaKaPermitBusinessRegistration> PaThaKaPermitBusinessRegistrations { get; set; }

    public virtual DbSet<PaThaKaPermitBusinessRegistrationAmend> PaThaKaPermitBusinessRegistrationAmends { get; set; }

    public virtual DbSet<PaThaKaRegistration> PaThaKaRegistrations { get; set; }

    public virtual DbSet<PaThaKaRegistrationAmend> PaThaKaRegistrationAmends { get; set; }

    public virtual DbSet<PaThaKaRegistrationFile> PaThaKaRegistrationFiles { get; set; }

    public virtual DbSet<PaThaKaStatus> PaThaKaStatuses { get; set; }

    public virtual DbSet<PaThaKaStatusLog> PaThaKaStatusLogs { get; set; }

    public virtual DbSet<PaThaKaType> PaThaKaTypes { get; set; }

    public virtual DbSet<PaymentType> PaymentTypes { get; set; }

    public virtual DbSet<PermitBusiness> PermitBusinesses { get; set; }

    public virtual DbSet<PortOfDischarge> PortOfDischarges { get; set; }

    public virtual DbSet<PortOfLoading> PortOfLoadings { get; set; }

    public virtual DbSet<Position> Positions { get; set; }

    public virtual DbSet<Price> Prices { get; set; }

    public virtual DbSet<PriceImport> PriceImports { get; set; }

    public virtual DbSet<ProductGroup> ProductGroups { get; set; }

    public virtual DbSet<ProductItem> ProductItems { get; set; }

    public virtual DbSet<QuotaAllowance> QuotaAllowances { get; set; }

    public virtual DbSet<QuotaUsage> QuotaUsages { get; set; }

    public virtual DbSet<ReExport> ReExports { get; set; }

    public virtual DbSet<ReExportGood> ReExportGoods { get; set; }

    public virtual DbSet<ReExportRegistration> ReExportRegistrations { get; set; }

    public virtual DbSet<ReExportRegistrationAmend> ReExportRegistrationAmends { get; set; }

    public virtual DbSet<ReExportRegistrationFile> ReExportRegistrationFiles { get; set; }

    public virtual DbSet<RequestAutoApproveDescription> RequestAutoApproveDescriptions { get; set; }

    public virtual DbSet<RequestAutoApproveDescriptionImport> RequestAutoApproveDescriptionImports { get; set; }

    public virtual DbSet<Sakhan> Sakhans { get; set; }

    public virtual DbSet<SaleCenter> SaleCenters { get; set; }

    public virtual DbSet<SaleCenterRegistration> SaleCenterRegistrations { get; set; }

    public virtual DbSet<SaleCenterRegistrationAmend> SaleCenterRegistrationAmends { get; set; }

    public virtual DbSet<SaleCenterRegistrationFile> SaleCenterRegistrationFiles { get; set; }

    public virtual DbSet<ServiceAgentCommodity> ServiceAgentCommodities { get; set; }

    public virtual DbSet<ShowRoom> ShowRooms { get; set; }

    public virtual DbSet<ShowRoomRegistration> ShowRoomRegistrations { get; set; }

    public virtual DbSet<ShowRoomRegistrationAmend> ShowRoomRegistrationAmends { get; set; }

    public virtual DbSet<ShowRoomRegistrationFile> ShowRoomRegistrationFiles { get; set; }

    public virtual DbSet<Signature> Signatures { get; set; }

    public virtual DbSet<State> States { get; set; }

    public virtual DbSet<Status> Statuses { get; set; }

    public virtual DbSet<Testing> Testings { get; set; }

    public virtual DbSet<Tinno> Tinnos { get; set; }

    public virtual DbSet<Township> Townships { get; set; }

    public virtual DbSet<TransferApplicationHistory> TransferApplicationHistories { get; set; }

    public virtual DbSet<TransferHistory> TransferHistories { get; set; }

    public virtual DbSet<Unit> Units { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserDetail> UserDetails { get; set; }

    public virtual DbSet<WholeSaleRetail> WholeSaleRetails { get; set; }

    public virtual DbSet<WholeSaleRetailRegistration> WholeSaleRetailRegistrations { get; set; }

    public virtual DbSet<WholeSaleRetailRegistrationAmend> WholeSaleRetailRegistrationAmends { get; set; }

    public virtual DbSet<WholeSaleRetailRegistrationFile> WholeSaleRetailRegistrationFiles { get; set; }

    public virtual DbSet<WineImportation> WineImportations { get; set; }

    public virtual DbSet<WineImportationRegistration> WineImportationRegistrations { get; set; }

    public virtual DbSet<WineImportationRegistrationAmend> WineImportationRegistrationAmends { get; set; }

    public virtual DbSet<WineImportationRegistrationFile> WineImportationRegistrationFiles { get; set; }

    public virtual DbSet<WineType> WineTypes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountTransaction>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<AccountTransactionDetail>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.AccountTransactionId).IsFixedLength();
        });

        modelBuilder.Entity<AdditionalDescription>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<AdditionalDescriptionImport>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<ApierrorLog>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<AspstateTempApplication>(entity =>
        {
            entity.HasKey(e => e.AppId).HasName("PK__ASPState__8E2CF7F9362FA828");

            entity.Property(e => e.AppId).ValueGeneratedNever();
            entity.Property(e => e.AppName).IsFixedLength();
        });

        modelBuilder.Entity<AspstateTempSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PK__ASPState__C9F492908C216A78");

            entity.Property(e => e.Created).HasDefaultValueSql("(getutcdate())");
        });

        modelBuilder.Entity<BorderExportLicence>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CustomResponseId).IsFixedLength();
            entity.Property(e => e.IndividualTradingId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BorderExportLicenceAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderExportLicenceId).IsFixedLength();
        });

        modelBuilder.Entity<BorderExportLicenceAmendItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_BoderExportLicenceAmendItem");

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderExportLicenceId).IsFixedLength();
            entity.Property(e => e.BorderExportLicenceItemId).IsFixedLength();
        });

        modelBuilder.Entity<BorderExportLicenceFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_BorderExportLicenceFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderExportLicenceId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BorderExportLicenceItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.UniqueId).ValueGeneratedOnAdd();
            entity.Property(e => e.BorderExportLicenceId).IsFixedLength();
            entity.Property(e => e.CheckId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BorderExportLicenceRecommendation>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderExportLicenceId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.RecommendationId).IsFixedLength();
        });

        modelBuilder.Entity<BorderExportPermit>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CustomResponseId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BorderExportPermitAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderExportPermitId).IsFixedLength();
        });

        modelBuilder.Entity<BorderExportPermitAmendItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderExportPermitId).IsFixedLength();
            entity.Property(e => e.BorderExportPermitItemId).IsFixedLength();
        });

        modelBuilder.Entity<BorderExportPermitFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_BorderExportPermitFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderExportPermitId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BorderExportPermitItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.UniqueId).ValueGeneratedOnAdd();
            entity.Property(e => e.BorderExportPermitId).IsFixedLength();
            entity.Property(e => e.CheckId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BorderExportPermitRecommendation>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderExportPermitId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.RecommendationId).IsFixedLength();
        });

        modelBuilder.Entity<BorderImportLicence>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CustomResponseId).IsFixedLength();
            entity.Property(e => e.IndividualTradingId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BorderImportLicenceAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderImportLicenceId).IsFixedLength();
        });

        modelBuilder.Entity<BorderImportLicenceAmendItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderImportLicenceId).IsFixedLength();
            entity.Property(e => e.BorderImportLicenceItemId).IsFixedLength();
        });

        modelBuilder.Entity<BorderImportLicenceFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_BorderImportLicenceFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderImportLicenceId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BorderImportLicenceItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.UniqueId).ValueGeneratedOnAdd();
            entity.Property(e => e.BorderImportLicenceId).IsFixedLength();
            entity.Property(e => e.CheckId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BorderImportLicenceRecommendation>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderImportLicenceId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.RecommendationId).IsFixedLength();
        });

        modelBuilder.Entity<BorderImportPermit>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CustomResponseId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BorderImportPermitAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderImportPermitId).IsFixedLength();
        });

        modelBuilder.Entity<BorderImportPermitAmendItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderImportPermitId).IsFixedLength();
            entity.Property(e => e.BorderImportPermitItemId).IsFixedLength();
        });

        modelBuilder.Entity<BorderImportPermitFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_BorderImportPermitFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderImportPermitId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BorderImportPermitItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.UniqueId).ValueGeneratedOnAdd();
            entity.Property(e => e.BorderImportPermitId).IsFixedLength();
            entity.Property(e => e.CheckId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BorderImportPermitRecommendation>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BorderImportPermitId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.RecommendationId).IsFixedLength();
        });

        modelBuilder.Entity<BusinessServiceAgency>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<BusinessServiceAgencyRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<BusinessServiceAgencyRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BusinessServiceAgencyRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<BusinessServiceAgencyRegistrationFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_BusinessServiceAgencyRegistrationFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BusinessServiceAgencyRegistrationId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<DataUpdateHistory>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<DeleteDatum>(entity =>
        {
            entity.Property(e => e.ApplicationId).IsFixedLength();
            entity.Property(e => e.IndividualTradingId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<Dicadirector>(entity =>
        {
            entity.Property(e => e.OfficerId).IsFixedLength();
        });

        modelBuilder.Entity<DutyFreeShop>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<DutyFreeShopRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<DutyFreeShopRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.DutyFreeShopRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<DutyFreeShopRegistrationFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_DutyFreeShopRegistrationFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.DutyFreeShopRegistrationId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<Ediapilog>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ResponseId).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<Edilog>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CustomResponseId).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<EiccattachFile>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<Eicccertificate>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<EicccertificateHistory>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.EicccertificateId).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_SendEmailLog");

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<EvcycleShowRoom>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BusinessServiceAgencyId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<EvcycleShowRoomRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BusinessServiceAgencyId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<EvcycleShowRoomRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ShowRoomRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<EvcycleShowRoomRegistrationFile>(entity =>
        {
            entity.ToTable("EVCycleShowRoomRegistrationFiles", tb => tb.HasTrigger("TR_FixUrl_EVCycleShowRoomRegistrationFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.ShowRoomRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<EvshowRoom>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BusinessServiceAgencyId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<EvshowRoomRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BusinessServiceAgencyId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<EvshowRoomRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ShowRoomRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<EvshowRoomRegistrationFile>(entity =>
        {
            entity.ToTable("EVShowRoomRegistrationFiles", tb => tb.HasTrigger("TR_FixUrl_EVShowRoomRegistrationFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.ShowRoomRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<ExportLicence>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CustomResponseId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ExportLicenceAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ExportLicenceId).IsFixedLength();
        });

        modelBuilder.Entity<ExportLicenceAmendItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ExportLicenceId).IsFixedLength();
            entity.Property(e => e.ExportLicenceItemId).IsFixedLength();
        });

        modelBuilder.Entity<ExportLicenceFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_ExportLicenceFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ExportLicenceId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ExportLicenceItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.UniqueId).ValueGeneratedOnAdd();
            entity.Property(e => e.CheckId).IsFixedLength();
            entity.Property(e => e.ExportLicenceId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ExportLicenceRecommendation>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ExportLicenceId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.RecommendationId).IsFixedLength();
        });

        modelBuilder.Entity<ExportPermit>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CustomResponseId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ExportPermitAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ExportPermitId).IsFixedLength();
        });

        modelBuilder.Entity<ExportPermitAmendItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ExportPermitId).IsFixedLength();
            entity.Property(e => e.ExportPermitItemId).IsFixedLength();
        });

        modelBuilder.Entity<ExportPermitFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_ExportPermitFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ExportPermitId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ExportPermitItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.UniqueId).ValueGeneratedOnAdd();
            entity.Property(e => e.CheckId).IsFixedLength();
            entity.Property(e => e.ExportPermitId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ExportPermitRecommendation>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ExportPermitId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.RecommendationId).IsFixedLength();
        });

        modelBuilder.Entity<ForgotPassword>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<Hscode>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_HSCode_1");
        });

        modelBuilder.Entity<HscodeOldBk>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_HSCode");
        });

        modelBuilder.Entity<ImportLicence>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CustomResponseId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ImportLicenceAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ImportLicenceId).IsFixedLength();
        });

        modelBuilder.Entity<ImportLicenceAmendItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ImportLicenceId).IsFixedLength();
            entity.Property(e => e.ImportLicenceItemId).IsFixedLength();
        });

        modelBuilder.Entity<ImportLicenceFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_ImportLicenceFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ImportLicenceId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ImportLicenceItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.UniqueId).ValueGeneratedOnAdd();
            entity.Property(e => e.CheckId).IsFixedLength();
            entity.Property(e => e.ImportLicenceId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ImportLicenceRecommendation>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ImportLicenceId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.RecommendationId).IsFixedLength();
        });

        modelBuilder.Entity<ImportPermit>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CustomResponseId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ImportPermitAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ImportPermitId).IsFixedLength();
        });

        modelBuilder.Entity<ImportPermitAmendItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ImportPermitId).IsFixedLength();
            entity.Property(e => e.ImportPermitItemId).IsFixedLength();
        });

        modelBuilder.Entity<ImportPermitFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_ImportPermitFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ImportPermitId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ImportPermitItem>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.UniqueId).ValueGeneratedOnAdd();
            entity.Property(e => e.CheckId).IsFixedLength();
            entity.Property(e => e.ImportPermitId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ImportPermitRecommendation>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ImportPermitId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.RecommendationId).IsFixedLength();
        });

        modelBuilder.Entity<IndividualTrading>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
        });

        modelBuilder.Entity<IndividualTradingBind>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.IndividualTradingId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
        });

        modelBuilder.Entity<IndividualTradingBindFile>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.IndividualTradingBindId).IsFixedLength();
        });

        modelBuilder.Entity<IndividualTradingRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<IndividualTradingRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.IndividualTradingRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<IndividualTradingRegistrationFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_IndividualTradingRegistrationFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.IndividualTradingRegistrationId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<LicencePermitApproveHistory>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<Log4NetLog>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ManifestLicence>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<MemberRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<MessageHistory>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<MpupaymentTransaction>(entity =>
        {
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<Ogarecommendation>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<OgarecommendationFile>(entity =>
        {
            entity.ToTable("OGARecommendationFile", tb => tb.HasTrigger("TR_FixUrl_OGARecommendationFile"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.OgarecommendationId).IsFixedLength();
        });

        modelBuilder.Entity<OgarecommendationHistory>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.LicencePermitId).IsFixedLength();
            entity.Property(e => e.OgarecommendationId).IsFixedLength();
        });

        modelBuilder.Entity<OtpforAdminLogin>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__OTPForAd__3214EC0776D7A155");
        });

        modelBuilder.Entity<Otplog>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKa>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CompanyId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.WholeSaleRetailId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaAmendApidirector>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_PaThaKaAPIDirectors");

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.OfficerId).IsFixedLength();
            entity.Property(e => e.PaThaKaAmendApilogId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaAmendApilog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_AmendCompanyAPILog");

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CompanyId).IsFixedLength();
            entity.Property(e => e.PaThaKaRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaApplicationRemoval>(entity =>
        {
            entity.Property(e => e.MemberId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaAutoCancelFine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PaThaKaAutoCancelFine_LicenceFees");

            entity.Property(e => e.PathakaRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaBind>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaBindFile>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.PaThaKaBindId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaDirector>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.OfficerId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaDirectorsBlackListLog>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.DirectorId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaDirectorsRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CheckId).IsFixedLength();
            entity.Property(e => e.DirectorId).IsFixedLength();
            entity.Property(e => e.OfficerId).IsFixedLength();
            entity.Property(e => e.PaThaKaRegistrationId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaDirectorsRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.PaThaKaDirectorsRegistrationId).IsFixedLength();
            entity.Property(e => e.PaThaKaRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaPermitBusiness>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaPermitBusinessRegistration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_PaThaKaRegistrationPermitBusiness");

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CheckId).IsFixedLength();
            entity.Property(e => e.PaThaKaRegistrationId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaPermitBusinessRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.PaThaKaPermitBusinessRegistrationId).IsFixedLength();
            entity.Property(e => e.PaThaKaRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CompanyId).IsFixedLength();
            entity.Property(e => e.CustomResponseId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaAmendApilog).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.WholeSaleRetailRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.PaThaKaRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaRegistrationFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_PaThaKaRegistrationFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.PaThaKaRegistrationId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<PaThaKaStatusLog>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.CustomResponseId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<PermitBusiness>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.PermitBusiness");
        });

        modelBuilder.Entity<Price>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<PriceImport>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<QuotaAllowance>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<QuotaUsage>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<ReExport>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<ReExportGood>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<ReExportRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ReExportRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ReExportRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<ReExportRegistrationFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_ReExportRegistrationFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.ReExportRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<RequestAutoApproveDescription>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<RequestAutoApproveDescriptionImport>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<SaleCenter>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BusinessServiceAgencyId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<SaleCenterRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BusinessServiceAgencyId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<SaleCenterRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.SaleCenterRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<SaleCenterRegistrationFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_SaleCenterRegistrationFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.SaleCenterRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<ShowRoom>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BusinessServiceAgencyId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<ShowRoomRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.BusinessServiceAgencyId).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<ShowRoomRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ShowRoomRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<ShowRoomRegistrationFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_ShowRoomRegistrationFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.ShowRoomRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<Testing>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.Test).IsFixedLength();
        });

        modelBuilder.Entity<Tinno>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
        });

        modelBuilder.Entity<TransferApplicationHistory>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.TransactionId).IsFixedLength();
        });

        modelBuilder.Entity<WholeSaleRetail>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<WholeSaleRetailRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<WholeSaleRetailRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.WholeSaleRetailRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<WholeSaleRetailRegistrationFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_WholeSaleRetailRegistrationFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.WholeSaleRetailRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<WineImportation>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
        });

        modelBuilder.Entity<WineImportationRegistration>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.MemberId).IsFixedLength();
            entity.Property(e => e.PaThaKaId).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
        });

        modelBuilder.Entity<WineImportationRegistrationAmend>(entity =>
        {
            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.WineImportationRegistrationId).IsFixedLength();
        });

        modelBuilder.Entity<WineImportationRegistrationFile>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_FixUrl_WineImportationRegistrationFiles"));

            entity.Property(e => e.Id).IsFixedLength();
            entity.Property(e => e.ParentId).IsFixedLength();
            entity.Property(e => e.WineImportationRegistrationId).IsFixedLength();
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
