using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKaRegistrationAmend")]
public partial class PaThaKaRegistrationAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaRegistrationId { get; set; } = null!;

    public bool IsCompanyRegistrationNo { get; set; }

    public bool IsCompanyRegistrationDate { get; set; }

    public bool IsCompanyName { get; set; }

    public bool IsUnitLevel { get; set; }

    public bool IsStreetNumberStreetName { get; set; }

    public bool IsQuarterCityTownship { get; set; }

    public bool IsState { get; set; }

    public bool IsCountry { get; set; }

    public bool IsPostalCode { get; set; }

    public bool IsCurrencyId { get; set; }

    public bool IsCapital { get; set; }

    public bool IsMobile1 { get; set; }

    public bool IsMobile2 { get; set; }

    public bool IsMobile3 { get; set; }

    public bool IsFax { get; set; }

    public bool IsEmail { get; set; }

    public bool IsBusinessTypeId { get; set; }

    public bool IsLineofBusinessId { get; set; }

    public bool IsOwnerName { get; set; }

    [Column("IsOwnerNRC")]
    public bool IsOwnerNrc { get; set; }

    [Column("IsOwnerOldNRC")]
    public bool IsOwnerOldNrc { get; set; }

    [Column("IsMICPermitNo")]
    public bool IsMicpermitNo { get; set; }

    public bool? IsDirectorChange { get; set; }
}
