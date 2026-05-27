using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("ImportPermitRecommendation")]
public partial class ImportPermitRecommendation
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string ImportPermitId { get; set; } = null!;

    [Column("OGADepartmentId")]
    public int OgadepartmentId { get; set; }

    [Column("OGASectionId")]
    public int OgasectionId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string RecommendationId { get; set; } = null!;

    [StringLength(200)]
    public string RecommendationNo { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string? ParentId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
