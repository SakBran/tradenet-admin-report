using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("BusinessServiceAgency")]
public partial class BusinessServiceAgency
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(50)]
    public string BusinessServiceAgencyNo { get; set; } = null!;

    public int CardRegistrationFeesId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaId { get; set; } = null!;

    [StringLength(200)]
    public string Name { get; set; } = null!;

    public int BusinessTypeId { get; set; }

    [StringLength(200)]
    public string AuthorizeCompany { get; set; } = null!;

    [StringLength(200)]
    public string ServiceAgent { get; set; } = null!;

    [StringLength(50)]
    public string Commodity { get; set; } = null!;

    [StringLength(100)]
    public string ComissionRate { get; set; } = null!;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Salary { get; set; }

    public string? Remark { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime DecisionDate { get; set; }

    public int DecisionCodeId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime StartDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime EndDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime IssuedDate { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
