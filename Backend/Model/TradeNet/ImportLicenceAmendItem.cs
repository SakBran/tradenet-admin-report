using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("ImportLicenceAmendItem")]
public partial class ImportLicenceAmendItem
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string ImportLicenceId { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string ImportLicenceItemId { get; set; } = null!;

    [Column("IsHSCode")]
    public bool IsHscode { get; set; }

    public bool IsDescription { get; set; }

    public bool IsUnitId { get; set; }

    public bool IsPrice { get; set; }

    public bool IsQuantity { get; set; }

    public bool IsAmount { get; set; }

    public bool IsCurrencyId { get; set; }

    public bool IsNew { get; set; }
}
