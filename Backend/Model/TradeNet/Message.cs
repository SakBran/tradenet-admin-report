using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("Message")]
public partial class Message
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string TransactionId { get; set; } = null!;

    [StringLength(50)]
    public string FormType { get; set; } = null!;

    [StringLength(50)]
    public string ApplicationNo { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaId { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string? MemberId { get; set; }

    [Column("Message")]
    public string Message1 { get; set; } = null!;

    [StringLength(50)]
    public string Status { get; set; } = null!;

    public bool IsRead { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public bool? IsSuspension { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? SuspensionDate { get; set; }

    public int? SuspendedUser { get; set; }
}
