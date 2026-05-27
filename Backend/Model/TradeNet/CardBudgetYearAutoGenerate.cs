using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("CardBudgetYearAutoGenerate")]
public partial class CardBudgetYearAutoGenerate
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string ItemNo { get; set; } = null!;

    [StringLength(50)]
    public string FormType { get; set; } = null!;

    [StringLength(50)]
    public string Type { get; set; } = null!;

    [StringLength(50)]
    public string AppType { get; set; } = null!;

    public int ValueCount { get; set; }

    [StringLength(50)]
    public string BudgetYear { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
