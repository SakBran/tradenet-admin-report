using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("Status")]
public partial class Status
{
    [Key]
    public int Id { get; set; }

    [Column("Status")]
    [StringLength(50)]
    public string Status1 { get; set; } = null!;

    [StringLength(200)]
    public string Message { get; set; } = null!;

    public int SortOrder { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedUserId { get; set; }
}
