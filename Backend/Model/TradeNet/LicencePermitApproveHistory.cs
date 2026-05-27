using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("LicencePermitApproveHistory")]
public partial class LicencePermitApproveHistory
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(50)]
    public string LicencePermitNo { get; set; } = null!;

    [StringLength(50)]
    public string FormType { get; set; } = null!;

    public string? Remark { get; set; }

    [Column("OGADepartmentId")]
    public int OgadepartmentId { get; set; }

    [Column("OGASectionId")]
    public int OgasectionId { get; set; }

    public int LicenceApproveUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
