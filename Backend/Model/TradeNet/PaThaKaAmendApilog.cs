using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKaAmendAPILog")]
public partial class PaThaKaAmendApilog
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string? CompanyId { get; set; }

    [StringLength(200)]
    public string? CompanyName { get; set; }

    [StringLength(10)]
    public string? IsForeignCompany { get; set; }

    [StringLength(50)]
    public string? CompanyRegistrationNo { get; set; }

    [StringLength(50)]
    public string? CompanyRegistrationDate { get; set; }

    [StringLength(200)]
    public string? UnitLevel { get; set; }

    [StringLength(200)]
    public string? StreetNumberStreetName { get; set; }

    [StringLength(200)]
    public string? QuarterCityTownship { get; set; }

    [StringLength(200)]
    public string? State { get; set; }

    [StringLength(200)]
    public string? Country { get; set; }

    [StringLength(8)]
    public string? PostalCode { get; set; }

    [StringLength(50)]
    public string? Currency { get; set; }

    [StringLength(100)]
    public string Capital { get; set; } = null!;

    [Column("MICPermitNo")]
    [StringLength(200)]
    public string? MicpermitNo { get; set; }

    [StringLength(200)]
    public string CompanyType { get; set; } = null!;

    [StringLength(100)]
    public string Status { get; set; } = null!;

    public bool IsFinished { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? PaThaKaRegistrationId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }
}
