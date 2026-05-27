using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("AccountTransactionAutoGenerate")]
public partial class AccountTransactionAutoGenerate
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string ItemNo { get; set; } = null!;

    [StringLength(50)]
    public string PaymentType { get; set; } = null!;

    public int ValueCount { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
