using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Keyless]
[Table("Log4NetLog")]
public partial class Log4NetLog
{
    public int Id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime Date { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string Thread { get; set; } = null!;

    [StringLength(50)]
    [Unicode(false)]
    public string Level { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string Logger { get; set; } = null!;

    [StringLength(255)]
    public string? Method { get; set; }

    [StringLength(4000)]
    [Unicode(false)]
    public string Message { get; set; } = null!;

    [StringLength(2000)]
    [Unicode(false)]
    public string? Exception { get; set; }

    public string? Json { get; set; }
}
