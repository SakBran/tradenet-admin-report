using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("ReExportRegistration")]
public partial class ReExportRegistration
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
    public string ReExportNo { get; set; } = null!;

    public int CardRegistrationFeesId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaId { get; set; } = null!;

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [Column("NRCType")]
    [StringLength(50)]
    public string Nrctype { get; set; } = null!;

    [Column("NRCPrefixId")]
    public int NrcprefixId { get; set; }

    [Column("NRCPrefixCodeId")]
    public int NrcprefixCodeId { get; set; }

    [Column("NRCNo")]
    [StringLength(50)]
    public string Nrcno { get; set; } = null!;

    [StringLength(128)]
    public string? WarehouseUnitLevel { get; set; }

    [StringLength(128)]
    public string WarehouseStreetNumberStreetName { get; set; } = null!;

    [StringLength(64)]
    public string WarehouseQuarterCityTownship { get; set; } = null!;

    [StringLength(64)]
    public string WarehouseState { get; set; } = null!;

    [StringLength(200)]
    public string WarehouseCountry { get; set; } = null!;

    [StringLength(8)]
    public string? WarehousePostalCode { get; set; }

    public int BusinessTypeId { get; set; }

    [StringLength(200)]
    public string GoodsGroup { get; set; } = null!;

    public string? GoodsDescription { get; set; }

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

    [StringLength(128)]
    public string? SaleCenterUnitLevel2 { get; set; }

    [StringLength(128)]
    public string? SaleCenterStreetNumberStreetName2 { get; set; }

    [StringLength(128)]
    public string? SaleCenterQuarterCityTownship2 { get; set; }

    [StringLength(64)]
    public string? SaleCenterState2 { get; set; }

    [StringLength(200)]
    public string? SaleCenterCountry2 { get; set; }

    [StringLength(8)]
    public string? SaleCenterPostalCode2 { get; set; }

    [StringLength(128)]
    public string? SaleCenterUnitLevel3 { get; set; }

    [StringLength(128)]
    public string? SaleCenterStreetNumberStreetName3 { get; set; }

    [StringLength(128)]
    public string? SaleCenterQuarterCityTownship3 { get; set; }

    [StringLength(64)]
    public string? SaleCenterState3 { get; set; }

    [StringLength(200)]
    public string? SaleCenterCountry3 { get; set; }

    [StringLength(8)]
    public string? SaleCenterPostalCode3 { get; set; }

    [StringLength(128)]
    public string? SaleCenterUnitLevel4 { get; set; }

    [StringLength(128)]
    public string? SaleCenterStreetNumberStreetName4 { get; set; }

    [StringLength(128)]
    public string? SaleCenterQuarterCityTownship4 { get; set; }

    [StringLength(64)]
    public string? SaleCenterState4 { get; set; }

    [StringLength(200)]
    public string? SaleCenterCountry4 { get; set; }

    [StringLength(8)]
    public string? SaleCenterPostalCode4 { get; set; }

    [StringLength(128)]
    public string? SaleCenterUnitLevel5 { get; set; }

    [StringLength(128)]
    public string? SaleCenterStreetNumberStreetName5 { get; set; }

    [StringLength(128)]
    public string? SaleCenterQuarterCityTownship5 { get; set; }

    [StringLength(64)]
    public string? SaleCenterState5 { get; set; }

    [StringLength(200)]
    public string? SaleCenterCountry5 { get; set; }

    [StringLength(8)]
    public string? SaleCenterPostalCode5 { get; set; }
}
