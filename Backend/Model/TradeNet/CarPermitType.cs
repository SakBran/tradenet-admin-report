using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("CarPermitType")]
public partial class CarPermitType
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string? Code { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    [StringLength(50)]
    public string? ApplicationType { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public int CreatedUserId { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
