using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("EVCycleShowRoomRegistrationAmend")]
public partial class EvcycleShowRoomRegistrationAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string ShowRoomRegistrationId { get; set; } = null!;

    public bool IsName { get; set; }

    [Column("IsNRC")]
    public bool IsNrc { get; set; }

    [Column("IsOldNRC")]
    public bool IsOldNrc { get; set; }

    public bool IsBusinessTypeId { get; set; }

    public bool IsCarBrandId { get; set; }

    public bool IsServiceAgent { get; set; }

    public bool IsAuthorizeCompany { get; set; }

    public bool IsAuthorizeCompanyUnitLevel { get; set; }

    public bool IsAuthorizeCompanyStreetNumberStreetName { get; set; }

    public bool IsAuthorizeCompanyQuarterCityTownship { get; set; }

    public bool IsAuthorizeCompanyState { get; set; }

    public bool IsAuthorizeCompanyCountry { get; set; }

    public bool IsAuthorizeCompanyPostalCode { get; set; }

    public bool IsAuthorizeCompanyEmail { get; set; }

    public bool IsAuthorizeCompanyMobile1 { get; set; }

    public bool IsAuthorizeCompanyMobile2 { get; set; }

    public bool IsAuthorizeCompanyMobile3 { get; set; }

    public bool IsAuthorizeCompanyFax { get; set; }

    public bool IsShowRoomUnitLevel { get; set; }

    public bool IsShowRoomStreetNumberStreetName { get; set; }

    public bool IsShowRoomQuarterCityTownship { get; set; }

    public bool IsShowRoomState { get; set; }

    public bool IsShowRoomCountry { get; set; }

    public bool IsShowRoomPostalCode { get; set; }

    public bool IsShowRoomUnitLevel2 { get; set; }

    public bool IsShowRoomStreetNumberStreetName2 { get; set; }

    public bool IsShowRoomQuarterCityTownship2 { get; set; }

    public bool IsShowRoomState2 { get; set; }

    public bool IsShowRoomCountry2 { get; set; }

    public bool IsShowRoomPostalCode2 { get; set; }

    public bool IsShowRoomUnitLevel3 { get; set; }

    public bool IsShowRoomStreetNumberStreetName3 { get; set; }

    public bool IsShowRoomQuarterCityTownship3 { get; set; }

    public bool IsShowRoomState3 { get; set; }

    public bool IsShowRoomCountry3 { get; set; }

    public bool IsShowRoomPostalCode3 { get; set; }

    public bool IsShowRoomUnitLevel4 { get; set; }

    public bool IsShowRoomStreetNumberStreetName4 { get; set; }

    public bool IsShowRoomQuarterCityTownship4 { get; set; }

    public bool IsShowRoomState4 { get; set; }

    public bool IsShowRoomCountry4 { get; set; }

    public bool IsShowRoomPostalCode4 { get; set; }

    public bool IsShowRoomUnitLevel5 { get; set; }

    public bool IsShowRoomStreetNumberStreetName5 { get; set; }

    public bool IsShowRoomQuarterCityTownship5 { get; set; }

    public bool IsShowRoomState5 { get; set; }

    public bool IsShowRoomCountry5 { get; set; }

    public bool IsShowRoomPostalCode5 { get; set; }

    public bool IsWarehouseUnitLevel { get; set; }

    public bool IsWarehouseStreetNumberStreetName { get; set; }

    public bool IsWarehouseQuarterCityTownship { get; set; }

    public bool IsWarehouseState { get; set; }

    public bool IsWarehouseCountry { get; set; }

    public bool IsWarehousePostalCode { get; set; }
}
