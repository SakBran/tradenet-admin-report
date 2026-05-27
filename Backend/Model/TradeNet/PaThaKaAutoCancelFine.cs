using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKaAutoCancelFine")]
public partial class PaThaKaAutoCancelFine
{
    [Key]
    public int Id { get; set; }

    public double FineAmount { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string PathakaRegistrationId { get; set; } = null!;

    [StringLength(50)]
    public string ApplicationNo { get; set; } = null!;

    public bool? IsAutoCancel { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? AutoCancelDate { get; set; }

    public bool? IsReopen { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ReopenDate { get; set; }
}
