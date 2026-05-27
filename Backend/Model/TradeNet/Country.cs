using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

public partial class Country
{
    [Key]
    public int Id { get; set; }

    [StringLength(10)]
    public string? Code { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }

    public bool IsCarPermit { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public int CreatedUserId { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
