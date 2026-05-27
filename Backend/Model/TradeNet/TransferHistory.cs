using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("TransferHistory")]
public partial class TransferHistory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("fromEirno")]
    public string? FromEirno { get; set; }

    [Column("toEirno")]
    public string? ToEirno { get; set; }

    [Column("groupCode")]
    public string? GroupCode { get; set; }

    [Column("budgetYear")]
    public string? BudgetYear { get; set; }

    [Column("transferAmount", TypeName = "decimal(18, 4)")]
    public decimal? TransferAmount { get; set; }

    [Column("traferDate", TypeName = "datetime")]
    public DateTime? TraferDate { get; set; }
}
