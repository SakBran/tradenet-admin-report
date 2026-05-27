using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Keyless]
[Table("Testing")]
public partial class Testing
{
    [StringLength(10)]
    public string? Id { get; set; }

    [StringLength(10)]
    public string? Test { get; set; }
}
