using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKaStatusLog")]
public partial class PaThaKaStatusLog
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime Date { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaId { get; set; } = null!;

    public int PaThaKaStatusId { get; set; }

    public string? Remark { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }

    public bool? IsCustomSent { get; set; }

    public bool? IsCustomReceived { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? CustomResponseId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CustomSentDate { get; set; }
}
