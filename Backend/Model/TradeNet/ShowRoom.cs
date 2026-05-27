using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("ShowRoom")]
public partial class ShowRoom
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(50)]
    public string ShowRoomNo { get; set; } = null!;

    [StringLength(100)]
    public string RegistrationType { get; set; } = null!;

    public int CardRegistrationFeesId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaId { get; set; } = null!;

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

    [StringLength(36)]
    [Unicode(false)]
    public string BusinessServiceAgencyId { get; set; } = null!;

    public int BusinessTypeId { get; set; }

    public int CarBrandId { get; set; }

    [StringLength(200)]
    public string ServiceAgent { get; set; } = null!;

    public int ServiceAgentCommodityId { get; set; }

    [StringLength(200)]
    public string AuthorizeCompany { get; set; } = null!;

    [StringLength(128)]
    public string? AuthorizeCompanyUnitLevel { get; set; }

    [StringLength(128)]
    public string AuthorizeCompanyStreetNumberStreetName { get; set; } = null!;

    [StringLength(128)]
    public string AuthorizeCompanyQuarterCityTownship { get; set; } = null!;

    [StringLength(64)]
    public string AuthorizeCompanyState { get; set; } = null!;

    [StringLength(200)]
    public string AuthorizeCompanyCountry { get; set; } = null!;

    [StringLength(8)]
    public string? AuthorizeCompanyPostalCode { get; set; }

    [StringLength(50)]
    public string? AuthorizeCompanyEmail { get; set; }

    [StringLength(50)]
    public string? AuthorizeCompanyMobile1 { get; set; }

    [StringLength(50)]
    public string? AuthorizeCompanyMobile2 { get; set; }

    [StringLength(50)]
    public string? AuthorizeCompanyMobile3 { get; set; }

    [StringLength(50)]
    public string? AuthorizeCompanyFax { get; set; }

    [StringLength(128)]
    public string? ShowRoomUnitLevel { get; set; }

    [StringLength(128)]
    public string ShowRoomStreetNumberStreetName { get; set; } = null!;

    [StringLength(128)]
    public string ShowRoomQuarterCityTownship { get; set; } = null!;

    [StringLength(64)]
    public string ShowRoomState { get; set; } = null!;

    [StringLength(200)]
    public string ShowRoomCountry { get; set; } = null!;

    [StringLength(8)]
    public string? ShowRoomPostalCode { get; set; }

    [StringLength(128)]
    public string? ShowRoomUnitLevel2 { get; set; }

    [StringLength(128)]
    public string? ShowRoomStreetNumberStreetName2 { get; set; }

    [StringLength(128)]
    public string? ShowRoomQuarterCityTownship2 { get; set; }

    [StringLength(64)]
    public string? ShowRoomState2 { get; set; }

    [StringLength(200)]
    public string? ShowRoomCountry2 { get; set; }

    [StringLength(8)]
    public string? ShowRoomPostalCode2 { get; set; }

    [StringLength(128)]
    public string? ShowRoomUnitLevel3 { get; set; }

    [StringLength(128)]
    public string? ShowRoomStreetNumberStreetName3 { get; set; }

    [StringLength(128)]
    public string? ShowRoomQuarterCityTownship3 { get; set; }

    [StringLength(64)]
    public string? ShowRoomState3 { get; set; }

    [StringLength(200)]
    public string? ShowRoomCountry3 { get; set; }

    [StringLength(8)]
    public string? ShowRoomPostalCode3 { get; set; }

    [StringLength(128)]
    public string? ShowRoomUnitLevel4 { get; set; }

    [StringLength(128)]
    public string? ShowRoomStreetNumberStreetName4 { get; set; }

    [StringLength(128)]
    public string? ShowRoomQuarterCityTownship4 { get; set; }

    [StringLength(64)]
    public string? ShowRoomState4 { get; set; }

    [StringLength(200)]
    public string? ShowRoomCountry4 { get; set; }

    [StringLength(8)]
    public string? ShowRoomPostalCode4 { get; set; }

    [StringLength(128)]
    public string? ShowRoomUnitLevel5 { get; set; }

    [StringLength(128)]
    public string? ShowRoomStreetNumberStreetName5 { get; set; }

    [StringLength(128)]
    public string? ShowRoomQuarterCityTownship5 { get; set; }

    [StringLength(64)]
    public string? ShowRoomState5 { get; set; }

    [StringLength(200)]
    public string? ShowRoomCountry5 { get; set; }

    [StringLength(8)]
    public string? ShowRoomPostalCode5 { get; set; }

    [StringLength(128)]
    public string? WarehouseUnitLevel { get; set; }

    [StringLength(128)]
    public string WarehouseStreetNumberStreetName { get; set; } = null!;

    [StringLength(128)]
    public string WarehouseQuarterCityTownship { get; set; } = null!;

    [StringLength(64)]
    public string WarehouseState { get; set; } = null!;

    [StringLength(200)]
    public string WarehouseCountry { get; set; } = null!;

    [StringLength(8)]
    public string? WarehousePostalCode { get; set; }

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
