using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PriceImport")]
public partial class PriceImport
{
    [Key]
    [Column("id")]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [Column("HSCode")]
    public string? Hscode { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? ExpPrice { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? ExpPriceQty { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? ImpLowPrice { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? ImpHighPrice { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? ImpPriceQty { get; set; }

    [Column("UnitID")]
    public int? UnitId { get; set; }

    public int? Currency { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? EffectiveDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }

    public bool? IsActive { get; set; }

    [Column("ADCode")]
    [StringLength(50)]
    public string? Adcode { get; set; }

    [Column("HSCodeID")]
    public int? HscodeId { get; set; }
}
