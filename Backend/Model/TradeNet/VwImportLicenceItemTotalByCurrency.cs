using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Keyless]
public partial class VwImportLicenceItemTotalByCurrency
{
    [StringLength(36)]
    [Unicode(false)]
    public string ImportLicenceId { get; set; } = null!;

    public int CurrencyId { get; set; }

    [Column(TypeName = "decimal(38, 4)")]
    public decimal? TotalAmount { get; set; }

    public long ItemCount { get; set; }
}
