using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("WholeSaleRetailRegistration")]
public partial class WholeSaleRetailRegistration
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
    public string WholeSaleRetailNo { get; set; } = null!;

    [StringLength(100)]
    public string RegistrationType { get; set; } = null!;

    public int CardRegistrationFeesId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaId { get; set; } = null!;

    [StringLength(20)]
    public string CompanyRegistrationNo { get; set; } = null!;

    [StringLength(200)]
    public string CompanyName { get; set; } = null!;

    [StringLength(200)]
    public string Mobile { get; set; } = null!;

    [StringLength(200)]
    public string Email { get; set; } = null!;

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(128)]
    public string? WholeSaleRetailUnitLevel { get; set; }

    [StringLength(128)]
    public string WholeSaleRetailStreetNumberStreetName { get; set; } = null!;

    [StringLength(64)]
    public string WholeSaleRetailQuarterCityTownship { get; set; } = null!;

    [StringLength(64)]
    public string WholeSaleRetailState { get; set; } = null!;

    [StringLength(200)]
    public string WholeSaleRetailCountry { get; set; } = null!;

    [StringLength(8)]
    public string? WholeSaleRetailPostalCode { get; set; }

    [StringLength(100)]
    public string TypeofBusiness { get; set; } = null!;

    public string GoodsCategory { get; set; } = null!;

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
}
