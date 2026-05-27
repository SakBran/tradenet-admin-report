using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("GroupCode")]
public partial class GroupCode
{
    [Key]
    [Column("id")]
    [StringLength(100)]
    public string Id { get; set; } = null!;

    [Column("groupCode")]
    [StringLength(50)]
    public string? GroupCode1 { get; set; }

    [Column("unitId")]
    public int? UnitId { get; set; }

    [Column("description")]
    public string? Description { get; set; }
}
