using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("DocumentType")]
public partial class DocumentType
{
    [Key]
    public int Id { get; set; }

    [StringLength(200)]
    public string FormType { get; set; } = null!;

    [StringLength(50)]
    public string Type { get; set; } = null!;

    [StringLength(50)]
    public string ShortCode { get; set; } = null!;

    public string Description { get; set; } = null!;

    public bool IsRequire { get; set; }

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedUserId { get; set; }
}
