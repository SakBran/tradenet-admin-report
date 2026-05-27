using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("ASPStateTempSessions")]
[Index("Expires", Name = "Index_Expires")]
public partial class AspstateTempSession
{
    [Key]
    [StringLength(88)]
    public string SessionId { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime Created { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime Expires { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime LockDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime LockDateLocal { get; set; }

    public int LockCookie { get; set; }

    public int Timeout { get; set; }

    public bool Locked { get; set; }

    [MaxLength(7000)]
    public byte[]? SessionItemShort { get; set; }

    [Column(TypeName = "image")]
    public byte[]? SessionItemLong { get; set; }

    public int Flags { get; set; }
}
