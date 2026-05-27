using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("OGARecommendationFile")]
public partial class OgarecommendationFile
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [Column("OGARecommendationId")]
    [StringLength(36)]
    [Unicode(false)]
    public string OgarecommendationId { get; set; } = null!;

    [StringLength(200)]
    public string Url { get; set; } = null!;

    [StringLength(200)]
    public string Filename { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
