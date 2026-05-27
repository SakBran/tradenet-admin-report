using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("DICA_log")]
public partial class DicaLog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("data")]
    public string? Data { get; set; }

    [Column("createdDate", TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }
}
