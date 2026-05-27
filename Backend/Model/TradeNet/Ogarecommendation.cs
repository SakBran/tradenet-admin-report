using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("OGARecommendation")]
public partial class Ogarecommendation
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [Column("OGADepartmentId")]
    public int OgadepartmentId { get; set; }

    [Column("OGASectionId")]
    public int OgasectionId { get; set; }

    [StringLength(50)]
    public string ReferenceNo { get; set; } = null!;

    [StringLength(50)]
    public string SarNo { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime SarDate { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaId { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime? FromDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ToDate { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? FromAmount { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? ToAmount { get; set; }

    public string? Allowance { get; set; }

    public bool IsUsedOnce { get; set; }

    public bool IsClosed { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedUserId { get; set; }
}
