using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("EICCCertificateHistory")]
public partial class EicccertificateHistory
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [Column("EICCCertificateId")]
    [StringLength(36)]
    [Unicode(false)]
    public string EicccertificateId { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string TransactionId { get; set; } = null!;

    [StringLength(50)]
    public string FormType { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime Date { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = null!;

    public string? Remark { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
