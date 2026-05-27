using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKaPermitBusinessRegistrationAmend")]
public partial class PaThaKaPermitBusinessRegistrationAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaRegistrationId { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaPermitBusinessRegistrationId { get; set; } = null!;

    public bool IsPermitBusinessId { get; set; }

    public bool IsNew { get; set; }

    public bool IsDeleted { get; set; }
}
