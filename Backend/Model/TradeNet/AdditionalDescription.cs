using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("AdditionalDescription")]
public partial class AdditionalDescription
{
    [Key]
    [Column("id")]
    [StringLength(200)]
    public string Id { get; set; } = null!;

    [Column("HSCode")]
    [StringLength(50)]
    public string? Hscode { get; set; }

    [Column("ADCode")]
    [StringLength(50)]
    public string? Adcode { get; set; }

    [Column("AdditionalDescription")]
    [StringLength(450)]
    public string? AdditionalDescription1 { get; set; }
}
