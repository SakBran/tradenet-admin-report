using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("ReExportRegistrationAmend")]
public partial class ReExportRegistrationAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string ReExportRegistrationId { get; set; } = null!;

    public bool IsName { get; set; }

    [Column("IsNRC")]
    public bool IsNrc { get; set; }

    [Column("IsOldNRC")]
    public bool IsOldNrc { get; set; }

    public bool IsUnitLevel { get; set; }

    public bool IsStreetNumberStreetName { get; set; }

    public bool IsQuarterCityTownship { get; set; }

    public bool IsState { get; set; }

    public bool IsCountry { get; set; }

    public bool IsPostalCode { get; set; }

    public bool IsBusinessTypeId { get; set; }

    public bool IsGoodsGroup { get; set; }

    public bool IsGoodsDescription { get; set; }
}
