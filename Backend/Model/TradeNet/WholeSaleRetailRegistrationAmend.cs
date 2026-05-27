using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("WholeSaleRetailRegistrationAmend")]
public partial class WholeSaleRetailRegistrationAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string WholeSaleRetailRegistrationId { get; set; } = null!;

    public bool IsCompanyRegistrationNo { get; set; }

    public bool IsCompanyName { get; set; }

    public bool IsMobile { get; set; }

    public bool IsEmail { get; set; }

    public bool IsName { get; set; }

    public bool IsUnitLevel { get; set; }

    public bool IsStreetNumberStreetName { get; set; }

    public bool IsQuarterCityTownship { get; set; }

    public bool IsState { get; set; }

    public bool IsCountry { get; set; }

    public bool IsPostalCode { get; set; }

    public bool IsTypeofBusiness { get; set; }

    public bool IsGoodsCategory { get; set; }
}
