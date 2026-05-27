using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKaDirectorsBlackListLog")]
public partial class PaThaKaDirectorsBlackListLog
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

    [StringLength(36)]
    [Unicode(false)]
    public string DirectorId { get; set; } = null!;

    [Column("NRC")]
    [StringLength(100)]
    public string? Nrc { get; set; }

    [Column("NRCType")]
    [StringLength(50)]
    public string? Nrctype { get; set; }

    [Column("NRCPrefixId")]
    public int? NrcprefixId { get; set; }

    [Column("NRCPrefixCodeId")]
    public int? NrcprefixCodeId { get; set; }

    [Column("NRCNo")]
    [StringLength(50)]
    public string? Nrcno { get; set; }

    public bool IsBlackList { get; set; }

    public string? Remark { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }
}
