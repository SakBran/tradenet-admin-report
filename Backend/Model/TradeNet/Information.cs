using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

public partial class Information
{
    [Key]
    public int Id { get; set; }

    [StringLength(200)]
    public string Type { get; set; } = null!;

    [StringLength(50)]
    public string FormType { get; set; } = null!;

    [StringLength(200)]
    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    [StringLength(50)]
    public string ApplyType { get; set; } = null!;

    public int SortOrder { get; set; }

    [StringLength(50)]
    public string Controller { get; set; } = null!;

    [StringLength(50)]
    public string Action { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedUserId { get; set; }
}
