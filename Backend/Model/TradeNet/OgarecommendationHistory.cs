using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("OGARecommendationHistory")]
public partial class OgarecommendationHistory
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [Column("OGARecommendationId")]
    [StringLength(36)]
    [Unicode(false)]
    public string OgarecommendationId { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string LicencePermitId { get; set; } = null!;

    [StringLength(50)]
    public string Type { get; set; } = null!;

    public string Remark { get; set; } = null!;

    public string Balance { get; set; } = null!;

    [Column("MOCUserId")]
    public int MocuserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }
}
