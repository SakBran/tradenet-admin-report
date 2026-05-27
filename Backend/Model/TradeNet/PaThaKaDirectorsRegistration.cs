using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKaDirectorsRegistration")]
public partial class PaThaKaDirectorsRegistration
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaRegistrationId { get; set; } = null!;

    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(200)]
    public string? FormerName { get; set; }

    [StringLength(200)]
    public string? Nationality { get; set; }

    [Column("NRC")]
    [StringLength(200)]
    public string Nrc { get; set; } = null!;

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

    public int SortOrder { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? ParentId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? CheckId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? DirectorId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? OfficerId { get; set; }

    public bool IsDeleted { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }
}
