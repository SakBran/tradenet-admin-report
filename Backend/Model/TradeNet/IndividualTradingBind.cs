using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("IndividualTradingBind")]
public partial class IndividualTradingBind
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(50)]
    public string ApplicationNo { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime ApplicationDate { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string IndividualTradingId { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string MemberId { get; set; } = null!;

    public bool IsFinish { get; set; }

    [StringLength(100)]
    public string Status { get; set; } = null!;

    public int AssignUser { get; set; }

    public int? CheckUserId { get; set; }

    public bool? IsCheck { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CheckDate { get; set; }

    public int? ApproveUserId { get; set; }

    public bool? IsApprove { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ApproveDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
