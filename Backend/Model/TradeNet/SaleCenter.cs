using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("SaleCenter")]
public partial class SaleCenter
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(50)]
    public string SaleCenterNo { get; set; } = null!;

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
    public string? SaleCenterUnitLevel { get; set; }

    [StringLength(128)]
    public string SaleCenterStreetNumberStreetName { get; set; } = null!;

    [StringLength(128)]
    public string SaleCenterQuarterCityTownship { get; set; } = null!;

    [StringLength(64)]
    public string SaleCenterState { get; set; } = null!;

    [StringLength(200)]
    public string SaleCenterCountry { get; set; } = null!;

    [StringLength(8)]
    public string? SaleCenterPostalCode { get; set; }

    [StringLength(128)]
    public string? SaleCenterUnitLevel2 { get; set; }

    [StringLength(128)]
    public string? SaleCenterStreetNumberStreetName2 { get; set; }

    [StringLength(128)]
    public string? SaleCenterQuarterCityTownship2 { get; set; }

    [StringLength(64)]
    public string? SaleCenterState2 { get; set; }

    [StringLength(200)]
    public string? SaleCenterCountry2 { get; set; }

    [StringLength(8)]
    public string? SaleCenterPostalCode2 { get; set; }

    [StringLength(128)]
    public string? SaleCenterUnitLevel3 { get; set; }

    [StringLength(128)]
    public string? SaleCenterStreetNumberStreetName3 { get; set; }

    [StringLength(128)]
    public string? SaleCenterQuarterCityTownship3 { get; set; }

    [StringLength(64)]
    public string? SaleCenterState3 { get; set; }

    [StringLength(200)]
    public string? SaleCenterCountry3 { get; set; }

    [StringLength(8)]
    public string? SaleCenterPostalCode3 { get; set; }

    [StringLength(128)]
    public string? SaleCenterUnitLevel4 { get; set; }

    [StringLength(128)]
    public string? SaleCenterStreetNumberStreetName4 { get; set; }

    [StringLength(128)]
    public string? SaleCenterQuarterCityTownship4 { get; set; }

    [StringLength(64)]
    public string? SaleCenterState4 { get; set; }

    [StringLength(200)]
    public string? SaleCenterCountry4 { get; set; }

    [StringLength(8)]
    public string? SaleCenterPostalCode4 { get; set; }

    [StringLength(128)]
    public string? SaleCenterUnitLevel5 { get; set; }

    [StringLength(128)]
    public string? SaleCenterStreetNumberStreetName5 { get; set; }

    [StringLength(128)]
    public string? SaleCenterQuarterCityTownship5 { get; set; }

    [StringLength(64)]
    public string? SaleCenterState5 { get; set; }

    [StringLength(200)]
    public string? SaleCenterCountry5 { get; set; }

    [StringLength(8)]
    public string? SaleCenterPostalCode5 { get; set; }

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
