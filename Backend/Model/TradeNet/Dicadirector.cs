using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("DICADirectors")]
public partial class Dicadirector
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string? CompanyRegistrationNo { get; set; }

    [StringLength(200)]
    public string? CompanyName { get; set; }

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

    [Column("NRCType")]
    [StringLength(50)]
    public string? Nrctype { get; set; }

    [Column("NRCPrefixId")]
    public int NrcprefixId { get; set; }

    [Column("NRCPrefixCodeId")]
    public int NrcprefixCodeId { get; set; }

    [Column("NRCNo")]
    [StringLength(50)]
    public string Nrcno { get; set; } = null!;

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

    [StringLength(128)]
    public string? UnitLevel { get; set; }

    [StringLength(128)]
    public string? StreetNumberStreetName { get; set; }

    [StringLength(64)]
    public string? QuarterCityTownship { get; set; }

    [StringLength(64)]
    public string? State { get; set; }

    [StringLength(200)]
    public string? Country { get; set; }

    [StringLength(8)]
    public string? PostalCode { get; set; }

    [StringLength(50)]
    public string? AppointmentDate { get; set; }
}
