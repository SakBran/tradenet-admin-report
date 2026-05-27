using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("LicencePermitPeriod")]
public partial class LicencePermitPeriod
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string FormType { get; set; } = null!;

    [StringLength(20)]
    public string ApplyType { get; set; } = null!;

    public int ExtensionType { get; set; }

    public int Period { get; set; }

    public int CreatedUserId { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
