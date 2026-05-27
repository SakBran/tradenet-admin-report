using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("WholeSaleRetail")]
public partial class WholeSaleRetail
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(50)]
    public string WholeSaleRetailNo { get; set; } = null!;

    [StringLength(100)]
    public string RegistrationType { get; set; } = null!;

    public int CardRegistrationFeesId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaId { get; set; } = null!;

    [StringLength(20)]
    public string CompanyRegistrationNo { get; set; } = null!;

    [StringLength(200)]
    public string CompanyName { get; set; } = null!;

    [StringLength(200)]
    public string Mobile { get; set; } = null!;

    [StringLength(200)]
    public string Email { get; set; } = null!;

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(128)]
    public string? WholeSaleRetailUnitLevel { get; set; }

    [StringLength(128)]
    public string WholeSaleRetailStreetNumberStreetName { get; set; } = null!;

    [StringLength(64)]
    public string WholeSaleRetailQuarterCityTownship { get; set; } = null!;

    [StringLength(64)]
    public string WholeSaleRetailState { get; set; } = null!;

    [StringLength(200)]
    public string WholeSaleRetailCountry { get; set; } = null!;

    [StringLength(8)]
    public string? WholeSaleRetailPostalCode { get; set; }

    [StringLength(100)]
    public string TypeofBusiness { get; set; } = null!;

    public string GoodsCategory { get; set; } = null!;

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
