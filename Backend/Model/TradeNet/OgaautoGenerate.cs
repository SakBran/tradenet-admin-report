using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("OGAAutoGenerate")]
public partial class OgaautoGenerate
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string ItemNo { get; set; } = null!;

    [Column("OGASectionId")]
    public int OgasectionId { get; set; }

    public int ValueCount { get; set; }

    public int Year { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
