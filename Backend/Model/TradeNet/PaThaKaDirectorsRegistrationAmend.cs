using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKaDirectorsRegistrationAmend")]
public partial class PaThaKaDirectorsRegistrationAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaRegistrationId { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaDirectorsRegistrationId { get; set; } = null!;

    public bool IsName { get; set; }

    public bool IsFormerName { get; set; }

    public bool IsNationality { get; set; }

    [Column("IsNRC")]
    public bool IsNrc { get; set; }

    [Column("IsOldNRC")]
    public bool IsOldNrc { get; set; }

    public bool IsOtherNationality { get; set; }

    public bool IsGender { get; set; }

    [Column("IsDOB")]
    public bool IsDob { get; set; }

    public bool IsPosition { get; set; }

    public bool IsMobile { get; set; }

    public bool IsEmail { get; set; }

    public bool IsUnitLevel { get; set; }

    public bool IsStreetNumberStreetName { get; set; }

    public bool IsQuarterCityTownship { get; set; }

    public bool IsState { get; set; }

    public bool IsCountry { get; set; }

    public bool IsPostalCode { get; set; }

    public bool IsNew { get; set; }

    public bool IsDeleted { get; set; }
}
