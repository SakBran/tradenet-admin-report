using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKaAmendAPIDirectors")]
public partial class PaThaKaAmendApidirector
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [Column("PaThaKaAmendAPILogId")]
    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaAmendApilogId { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string? OfficerId { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(200)]
    public string? FormerName { get; set; }

    [StringLength(200)]
    public string? Nationality { get; set; }

    [Column("NRC")]
    [StringLength(200)]
    public string? Nrc { get; set; }

    [StringLength(200)]
    public string? OtherNationality { get; set; }

    [StringLength(50)]
    public string? Gender { get; set; }

    [Column("DOB")]
    [StringLength(50)]
    public string? Dob { get; set; }

    [StringLength(200)]
    public string? Position { get; set; }

    [StringLength(100)]
    public string? Mobile { get; set; }

    [StringLength(100)]
    public string? Email { get; set; }

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

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }
}
