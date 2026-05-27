using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKaType")]
public partial class PaThaKaType
{
    [Key]
    public int Id { get; set; }

    [Column("APISource")]
    [StringLength(50)]
    public string Apisource { get; set; } = null!;

    [Column("APIUrl")]
    [StringLength(50)]
    public string Apiurl { get; set; } = null!;

    [StringLength(50)]
    public string Code { get; set; } = null!;

    [StringLength(50)]
    public string Prefix { get; set; } = null!;

    [StringLength(200)]
    public string Description { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsNeedDirector { get; set; }

    public bool IsFree { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public int CreatedUserId { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
