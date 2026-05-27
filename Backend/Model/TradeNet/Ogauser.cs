using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("OGAUsers")]
public partial class Ogauser
{
    [Key]
    public int Id { get; set; }

    [Column("OGADepartmentId")]
    public int OgadepartmentId { get; set; }

    [Column("OGASectionId")]
    public int OgasectionId { get; set; }

    [StringLength(50)]
    public string UserType { get; set; } = null!;

    [StringLength(200)]
    public string FullName { get; set; } = null!;

    [StringLength(200)]
    public string UserName { get; set; } = null!;

    [StringLength(200)]
    public string Password { get; set; } = null!;

    [StringLength(200)]
    public string? Position { get; set; }

    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(100)]
    public string? Mobile { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public int CreatedUserId { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
