using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

public partial class CardRegistrationFee
{
    [Key]
    public int Id { get; set; }

    [StringLength(200)]
    public string CardType { get; set; } = null!;

    [StringLength(50)]
    public string ApplyType { get; set; } = null!;

    [StringLength(50)]
    public string Description { get; set; } = null!;

    [StringLength(50)]
    public string? Type { get; set; }

    public int? Terms { get; set; }

    public int? FromDays { get; set; }

    public int? ToDays { get; set; }

    public double Amount { get; set; }

    public int SortOrder { get; set; }

    public bool IsFree { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }
}
