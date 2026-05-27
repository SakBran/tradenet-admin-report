using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("UserDetail")]
public partial class UserDetail
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [StringLength(50)]
    public string Type { get; set; } = null!;

    [StringLength(200)]
    public string SubType { get; set; } = null!;

    [StringLength(50)]
    public string Section { get; set; } = null!;
}
