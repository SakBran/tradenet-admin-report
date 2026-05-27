using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("RequestAutoApproveDescription")]
public partial class RequestAutoApproveDescription
{
    [Key]
    [Column("id")]
    [StringLength(200)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [Column("EIRNo")]
    [StringLength(50)]
    public string? Eirno { get; set; }

    [Column("HSCode")]
    [StringLength(50)]
    public string? Hscode { get; set; }

    [StringLength(450)]
    public string? AdditionalDescription { get; set; }

    public string? Remark { get; set; }

    public string? RecommendPrice { get; set; }

    public string? RequestDate { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }

    public string? Message { get; set; }

    public string? MemberId { get; set; }

    public string? MemberName { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }
}
