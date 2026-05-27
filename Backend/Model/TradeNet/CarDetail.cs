using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("CarDetail")]
public partial class CarDetail
{
    [Key]
    public int Id { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    [Column("CarModelYearGroupID")]
    public int CarModelYearGroupId { get; set; }

    public int CarModelYearId { get; set; }

    public int CarGroupId { get; set; }

    public int CarBrandId { get; set; }

    public int CarSubBrandId { get; set; }

    public int CountryId { get; set; }

    public int CarEnginePowerId { get; set; }

    [Column("FromCIFPrice", TypeName = "decimal(18, 2)")]
    public decimal FromCifprice { get; set; }

    [Column("ToCIFPrice", TypeName = "decimal(18, 2)")]
    public decimal ToCifprice { get; set; }

    public string? Remark { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public int CreatedUserId { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
