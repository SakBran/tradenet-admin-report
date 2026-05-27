using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("SaleCenterRegistrationAmend")]
public partial class SaleCenterRegistrationAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string SaleCenterRegistrationId { get; set; } = null!;

    public bool IsName { get; set; }

    [Column("IsNRC")]
    public bool IsNrc { get; set; }

    [Column("IsOldNRC")]
    public bool IsOldNrc { get; set; }

    public bool IsBusinessTypeId { get; set; }

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

    public bool IsSaleCenterUnitLevel { get; set; }

    public bool IsSaleCenterStreetNumberStreetName { get; set; }

    public bool IsSaleCenterQuarterCityTownship { get; set; }

    public bool IsSaleCenterState { get; set; }

    public bool IsSaleCenterCountry { get; set; }

    public bool IsSaleCenterPostalCode { get; set; }

    public bool IsWarehouseUnitLevel { get; set; }

    public bool IsWarehouseStreetNumberStreetName { get; set; }

    public bool IsWarehouseQuarterCityTownship { get; set; }

    public bool IsWarehouseState { get; set; }

    public bool IsWarehouseCountry { get; set; }

    public bool IsWarehousePostalCode { get; set; }

    public bool IsSaleCenterUnitLevel2 { get; set; }

    public bool IsSaleCenterStreetNumberStreetName2 { get; set; }

    public bool IsSaleCenterQuarterCityTownship2 { get; set; }

    public bool IsSaleCenterState2 { get; set; }

    public bool IsSaleCenterCountry2 { get; set; }

    public bool IsSaleCenterPostalCode2 { get; set; }

    public bool IsSaleCenterUnitLevel3 { get; set; }

    public bool IsSaleCenterStreetNumberStreetName3 { get; set; }

    public bool IsSaleCenterQuarterCityTownship3 { get; set; }

    public bool IsSaleCenterState3 { get; set; }

    public bool IsSaleCenterCountry3 { get; set; }

    public bool IsSaleCenterPostalCode3 { get; set; }

    public bool IsSaleCenterUnitLevel4 { get; set; }

    public bool IsSaleCenterStreetNumberStreetName4 { get; set; }

    public bool IsSaleCenterQuarterCityTownship4 { get; set; }

    public bool IsSaleCenterState4 { get; set; }

    public bool IsSaleCenterCountry4 { get; set; }

    public bool IsSaleCenterPostalCode4 { get; set; }

    public bool IsSaleCenterUnitLevel5 { get; set; }

    public bool IsSaleCenterStreetNumberStreetName5 { get; set; }

    public bool IsSaleCenterQuarterCityTownship5 { get; set; }

    public bool IsSaleCenterState5 { get; set; }

    public bool IsSaleCenterCountry5 { get; set; }

    public bool IsSaleCenterPostalCode5 { get; set; }
}
