using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("LicencePermitLimit")]
public partial class LicencePermitLimit
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string FormType { get; set; } = null!;

    public double FromAmount { get; set; }

    public double ToAmount { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }
}
