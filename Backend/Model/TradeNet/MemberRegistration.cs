using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("MemberRegistration")]
public partial class MemberRegistration
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(50)]
    public string ApplyType { get; set; } = null!;

    [StringLength(50)]
    public string ApplicationNo { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime ApplicationDate { get; set; }

    [StringLength(50)]
    public string Email { get; set; } = null!;

    [StringLength(200)]
    public string Password { get; set; } = null!;

    [StringLength(50)]
    public string MemberCode { get; set; } = null!;

    [StringLength(50)]
    public string FullName { get; set; } = null!;

    [StringLength(50)]
    public string Mobile1 { get; set; } = null!;

    [StringLength(50)]
    public string? Mobile2 { get; set; }

    [StringLength(50)]
    public string? Mobile3 { get; set; }

    [Column("NRCType")]
    [StringLength(50)]
    public string Nrctype { get; set; } = null!;

    [Column("NRCPrefixId")]
    public int NrcprefixId { get; set; }

    [Column("NRCPrefixCodeId")]
    public int NrcprefixCodeId { get; set; }

    [Column("NRCNo")]
    [StringLength(50)]
    public string Nrcno { get; set; } = null!;

    [StringLength(128)]
    public string? UnitLevel { get; set; }

    [StringLength(128)]
    public string StreetNumberStreetName { get; set; } = null!;

    [StringLength(64)]
    public string QuarterCityTownship { get; set; } = null!;

    public int StateId { get; set; }

    public int CountryId { get; set; }

    [StringLength(8)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string Status { get; set; } = null!;

    public bool IsVerified { get; set; }

    public bool IsActive { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? IssuedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ExtensionDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? StartDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? EndDate { get; set; }

    [StringLength(50)]
    public string? CompanyRegistrationNo { get; set; }

    public bool? IsOld { get; set; }
}
