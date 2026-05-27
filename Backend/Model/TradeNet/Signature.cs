using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("Signature")]
public partial class Signature
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string CompanyRegistrationNo { get; set; } = null!;

    [StringLength(255)]
    public string DirectorName { get; set; } = null!;

    [StringLength(255)]
    public string Rank { get; set; } = null!;

    [Column("Image_Url")]
    [StringLength(255)]
    public string ImageUrl { get; set; } = null!;

    [Column("Signature_No")]
    public int? SignatureNo { get; set; }

    public bool IsActive { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }
}
