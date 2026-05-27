using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("HSCode")]
public partial class Hscode
{
    [Key]
    public int Id { get; set; }

    public int Year { get; set; }

    [StringLength(20)]
    public string Code { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string? ImportLicenceType { get; set; }

    public string? ExportLicenceType { get; set; }

    public string? ImportAutoLicence { get; set; }

    public string? ExportAutoLicence { get; set; }

    public string? ImportGroupCode { get; set; }

    public string? ExportGroupCode { get; set; }

    [Column("ImportOGAId")]
    public int? ImportOgaid { get; set; }

    [Column("ExportOGAId")]
    public int? ExportOgaid { get; set; }

    public string? ImportSection { get; set; }

    public string? ExportSection { get; set; }

    public int? ImportCardTypeId { get; set; }

    public int? ExportCardTypeId { get; set; }

    public string? ImportAutoLicenceApprove { get; set; }

    public string? ExportAutoLicenceApprove { get; set; }

    public string? ImportProhibited { get; set; }

    public string? ExportProhibited { get; set; }

    public string? ImportRestricted { get; set; }

    public string? ExportRestricted { get; set; }

    public bool IsActive { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    public int? UnitId { get; set; }

    [Column("CMP")]
    [StringLength(50)]
    public string? Cmp { get; set; }

    [StringLength(50)]
    public string LicenceFree { get; set; } = null!;
}
