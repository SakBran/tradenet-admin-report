using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("BorderExportLicence")]
public partial class BorderExportLicence
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(20)]
    public string ApplyType { get; set; } = null!;

    [StringLength(50)]
    public string ApplicationNo { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime ApplicationDate { get; set; }

    [StringLength(50)]
    public string CardType { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string? PaThaKaId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? IndividualTradingId { get; set; }

    [StringLength(50)]
    public string ExportLicenceNo { get; set; } = null!;

    [StringLength(50)]
    public string? OldExportLicenceNo { get; set; }

    public int SakhanId { get; set; }

    public int ExportImportSectionId { get; set; }

    [StringLength(200)]
    public string BuyerName { get; set; } = null!;

    public string BuyerAddress { get; set; } = null!;

    public int BuyerCountryId { get; set; }

    public string PortofExportId { get; set; } = null!;

    [StringLength(255)]
    public string PortofDischargeCode { get; set; } = null!;

    public string PortofDischarge { get; set; } = null!;

    [StringLength(20)]
    public string ModeofTransport { get; set; } = null!;

    public int ExportImportMethodId { get; set; }

    public int CountryofOriginId { get; set; }

    public int ConsignedCountryId { get; set; }

    public string DestinationCountryId { get; set; } = null!;

    public int ExportImportIncotermId { get; set; }

    [StringLength(200)]
    public string? CommodityType { get; set; }

    public string? Remark { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string MemberId { get; set; } = null!;

    public int Step { get; set; }

    public bool IsDraft { get; set; }

    public bool IsFinish { get; set; }

    public bool IsOld { get; set; }

    [StringLength(100)]
    public string Status { get; set; } = null!;

    public int AssignUser { get; set; }

    public bool IsCancel { get; set; }

    public bool IsAmend { get; set; }

    public bool IsExtension { get; set; }

    public bool? IsSpecial { get; set; }

    public int? SpecialExtensionPeriod { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? ParentId { get; set; }

    public int? CheckUserId { get; set; }

    public bool? IsCheck { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CheckDate { get; set; }

    public int? ApproveUserId { get; set; }

    public bool? IsApprove { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ApproveDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? LicenceDate { get; set; }

    [StringLength(20)]
    public string? LicenceYear { get; set; }

    public bool? IsLastDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? LastDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? IssuedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? AmendDate { get; set; }

    public int? AmendRemarkId { get; set; }

    public string? AmendRemark { get; set; }

    public string? AmendAuthority { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ExtensionDate { get; set; }

    public string? ExtensionRemark { get; set; }

    public string? ExtensionAuthority { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CancellationDate { get; set; }

    public string? CancellationRemark { get; set; }

    public string? CancellationAuthority { get; set; }

    [Column("IsEICCSubmit")]
    public bool? IsEiccsubmit { get; set; }

    [Column("EICCNoId")]
    public int? EiccnoId { get; set; }

    [Column("EICCDate", TypeName = "datetime")]
    public DateTime? Eiccdate { get; set; }

    public int? ProductGroupId { get; set; }

    public int? ProductItemId { get; set; }

    [Column("EICCStatus")]
    [StringLength(50)]
    public string? Eiccstatus { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }

    public bool? IsAutoApprove { get; set; }

    public bool? IsAutoCancel { get; set; }

    public bool? IsReaddRecommendation { get; set; }

    public bool? IsPrint { get; set; }

    [Column("IsGenerateEDI")]
    public bool? IsGenerateEdi { get; set; }

    public bool? IsCustomSent { get; set; }

    public bool? IsCustomReceived { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? CustomResponseId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CustomSentDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? AutoCancelDate { get; set; }

    [Column("isCheckPrint")]
    public bool? IsCheckPrint { get; set; }

    [Column("isApprovePrint")]
    public bool? IsApprovePrint { get; set; }

    [Column("isSuspension")]
    public bool? IsSuspension { get; set; }

    [Column("auto")]
    [StringLength(50)]
    public string? Auto { get; set; }
}
