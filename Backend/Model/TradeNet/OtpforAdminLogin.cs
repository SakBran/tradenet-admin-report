using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("OTPForAdminLogin")]
public partial class OtpforAdminLogin
{
    [Key]
    public int Id { get; set; }

    [Column("tableName")]
    [StringLength(255)]
    [Unicode(false)]
    public string? TableName { get; set; }

    [Column("otp")]
    [StringLength(255)]
    [Unicode(false)]
    public string? Otp { get; set; }

    [Column("userId")]
    [StringLength(255)]
    [Unicode(false)]
    public string? UserId { get; set; }

    [Column("isUsed")]
    public bool? IsUsed { get; set; }

    [Column("lifetime", TypeName = "datetime")]
    public DateTime? Lifetime { get; set; }
}
