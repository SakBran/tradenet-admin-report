using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("EICCCertificate")]
public partial class Eicccertificate
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

    [Column("EICCNoId")]
    public int EiccnoId { get; set; }

    [Column("EICCDate", TypeName = "datetime")]
    public DateTime Eiccdate { get; set; }

    public int ProductGroupId { get; set; }

    public int ProductItemId { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = null!;

    public string? Remark { get; set; }

    public bool IsFinish { get; set; }

    public int SubmitUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int CreatedUserId { get; set; }
}
