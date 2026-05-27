using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("QuotaUsage")]
public partial class QuotaUsage
{
    [Key]
    [Column("id")]
    [StringLength(36)]
    public string Id { get; set; } = null!;

    [StringLength(50)]
    public string? GroupCode { get; set; }

    [Column("EIRNo")]
    [StringLength(12)]
    public string? Eirno { get; set; }

    [StringLength(50)]
    public string? BudgetYear { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Amount { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }

    [StringLength(50)]
    public string? CreatedUserId { get; set; }

    public string? Remark { get; set; }

    public string? ApplicationNo { get; set; }

    public string? LicenceNo { get; set; }

    public string? ApplyType { get; set; }
}
