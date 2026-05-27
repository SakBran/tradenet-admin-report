using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKaRegistration")]
public partial class PaThaKaRegistration
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
    public string PaThaKaNo { get; set; } = null!;

    public int PaThaKaTypeId { get; set; }

    public int CardRegistrationFeesId { get; set; }

    [StringLength(20)]
    public string CompanyRegistrationNo { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CompanyRegistrationDate { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? CompanyId { get; set; }

    [StringLength(200)]
    public string CompanyName { get; set; } = null!;

    [StringLength(128)]
    public string? UnitLevel { get; set; }

    [StringLength(128)]
    public string StreetNumberStreetName { get; set; } = null!;

    [StringLength(128)]
    public string QuarterCityTownship { get; set; } = null!;

    [StringLength(64)]
    public string State { get; set; } = null!;

    [StringLength(200)]
    public string Country { get; set; } = null!;

    [StringLength(8)]
    public string? PostalCode { get; set; }

    public int CurrencyId { get; set; }

    public double Capital { get; set; }

    [StringLength(50)]
    public string Mobile1 { get; set; } = null!;

    [StringLength(50)]
    public string? Mobile2 { get; set; }

    [StringLength(50)]
    public string? Mobile3 { get; set; }

    [StringLength(200)]
    public string? Fax { get; set; }

    [StringLength(200)]
    public string Email { get; set; } = null!;

    public int BusinessTypeId { get; set; }

    public int LineofBusinessId { get; set; }

    [StringLength(200)]
    public string OwnerName { get; set; } = null!;

    [Column("OwnerNRCType")]
    [StringLength(50)]
    public string OwnerNrctype { get; set; } = null!;

    [Column("OwnerNRCPrefixId")]
    public int OwnerNrcprefixId { get; set; }

    [Column("OwnerNRCPrefixCodeId")]
    public int OwnerNrcprefixCodeId { get; set; }

    [Column("OwnerNRCNo")]
    [StringLength(50)]
    public string OwnerNrcno { get; set; } = null!;

    [Column("MICPermitNo")]
    [StringLength(200)]
    public string? MicpermitNo { get; set; }

    [StringLength(200)]
    public string? CompanyType { get; set; }

    public string? Remark { get; set; }

    public string? Notes { get; set; }

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

    public bool IsDeCancel { get; set; }

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
    public DateTime? DecisionDate { get; set; }

    public int? DecisionCodeId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? StartDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? EndDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? IssuedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? AmendDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ExtensionDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CancellationDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? DeCancellationDate { get; set; }

    public bool IsWholeSale { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? WholeSaleRetailRegistrationId { get; set; }

    public bool? IsAutoCancel { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? AutoCancelDate { get; set; }

    [Column("IsEICCSubmit")]
    public bool? IsEiccsubmit { get; set; }

    [Column("EICCNoId")]
    public int? EiccnoId { get; set; }

    [Column("EICCDate", TypeName = "datetime")]
    public DateTime? Eiccdate { get; set; }

    [Column("EICCStatus")]
    [StringLength(50)]
    public string? Eiccstatus { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }

    public bool? IsPrint { get; set; }

    public bool? IsCustomSent { get; set; }

    public bool? IsCustomReceived { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? CustomResponseId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CustomSentDate { get; set; }

    [Column("PaThaKaAmendAPILog")]
    [StringLength(36)]
    [Unicode(false)]
    public string? PaThaKaAmendApilog { get; set; }

    [Column("IsDICAAmend")]
    public bool? IsDicaamend { get; set; }

    [Column("IRDStatus")]
    [StringLength(50)]
    public string? Irdstatus { get; set; }
}
