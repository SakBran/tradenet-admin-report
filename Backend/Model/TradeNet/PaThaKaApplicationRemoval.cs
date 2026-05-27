using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKaApplicationRemoval")]
public partial class PaThaKaApplicationRemoval
{
    [Key]
    public int Id { get; set; }

    public string ApplicationNo { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime? ApplicationDate { get; set; }

    [StringLength(20)]
    public string CompanyRegistrationNo { get; set; } = null!;

    [StringLength(200)]
    public string CompanyName { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string MemberId { get; set; } = null!;

    public int ApproveUserId { get; set; }

    [Column("JSON")]
    public string Json { get; set; } = null!;

    [Column("Removing Date", TypeName = "datetime")]
    public DateTime? RemovingDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }
}
