using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[PrimaryKey("Id", "UniqueId")]
[Table("ImportLicenceItem")]
public partial class ImportLicenceItem
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [Key]
    public int UniqueId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string ImportLicenceId { get; set; } = null!;

    public int ItemNo { get; set; }

    [Column("HSCodeId")]
    public int HscodeId { get; set; }

    [Column("HSCode")]
    [StringLength(100)]
    public string Hscode { get; set; } = null!;

    [Column("HSYear")]
    public int Hsyear { get; set; }

    public string? Description { get; set; }

    public int UnitId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Price { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Amount { get; set; }

    public int CurrencyId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? ParentId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? CheckId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
