using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("FAQ")]
public partial class Faq
{
    [Key]
    public int Id { get; set; }

    [Column("FAQCategoriesId")]
    public int FaqcategoriesId { get; set; }

    public string EnglishTitle { get; set; } = null!;

    public string MyanmarTitle { get; set; } = null!;

    public string EnglishDescription { get; set; } = null!;

    public string MyanmarDescription { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedUserId { get; set; }
}
