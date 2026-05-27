using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("DutyFreeShop")]
public partial class DutyFreeShop
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(50)]
    public string DutyFreeShopNo { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaId { get; set; } = null!;

    public int CardRegistrationFeesId { get; set; }

    public int BusinessTypeId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

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
    public string? LocationUnitLevel { get; set; }

    [StringLength(128)]
    public string LocationStreetNumberStreetName { get; set; } = null!;

    [StringLength(64)]
    public string LocationQuarterCityTownship { get; set; } = null!;

    [StringLength(64)]
    public string LocationState { get; set; } = null!;

    [StringLength(200)]
    public string LocationCountry { get; set; } = null!;

    [StringLength(8)]
    public string? LocationPostalCode { get; set; }

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
