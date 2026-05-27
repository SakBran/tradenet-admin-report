using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("PaThaKa")]
public partial class PaThaKa
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string? MemberId { get; set; }

    [StringLength(50)]
    public string PaThaKaNo { get; set; } = null!;

    public int PaThaKaTypeId { get; set; }

    public int CardRegistrationFeesId { get; set; }

    [StringLength(20)]
    public string CompanyRegistrationNo { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CompanyRegistrationDate { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? CompanyId { get; set; }

    [StringLength(200)]
    public string CompanyName { get; set; } = null!;

    [StringLength(128)]
    public string? UnitLevel { get; set; }

    [StringLength(128)]
    public string StreetNumberStreetName { get; set; } = null!;

    [StringLength(128)]
    public string QuarterCityTownship { get; set; } = null!;

    [StringLength(64)]
    public string State { get; set; } = null!;

    [StringLength(200)]
    public string Country { get; set; } = null!;

    [StringLength(8)]
    public string? PostalCode { get; set; }

    public int? CurrencyId { get; set; }

    public double? Capital { get; set; }

    [StringLength(50)]
    public string Mobile1 { get; set; } = null!;

    [StringLength(50)]
    public string? Mobile2 { get; set; }

    [StringLength(50)]
    public string? Mobile3 { get; set; }

    [StringLength(200)]
    public string? Fax { get; set; }

    [StringLength(200)]
    public string Email { get; set; } = null!;

    public int BusinessTypeId { get; set; }

    public int LineofBusinessId { get; set; }

    [StringLength(200)]
    public string OwnerName { get; set; } = null!;

    [Column("OwnerNRCType")]
    [StringLength(50)]
    public string OwnerNrctype { get; set; } = null!;

    [Column("OwnerNRCPrefixId")]
    public int OwnerNrcprefixId { get; set; }

    [Column("OwnerNRCPrefixCodeId")]
    public int OwnerNrcprefixCodeId { get; set; }

    [Column("OwnerNRCNo")]
    [StringLength(50)]
    public string OwnerNrcno { get; set; } = null!;

    [Column("MICPermitNo")]
    [StringLength(200)]
    public string? MicpermitNo { get; set; }

    [StringLength(200)]
    public string? CommencementPdfUrl { get; set; }

    [StringLength(200)]
    public string? CompanyType { get; set; }

    [StringLength(100)]
    public string Status { get; set; } = null!;

    public string? Remark { get; set; }

    [Column("NRCPdfUrl")]
    [StringLength(200)]
    public string? NrcpdfUrl { get; set; }

    [Column("MOCStatus")]
    [StringLength(50)]
    public string? Mocstatus { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime DecisionDate { get; set; }

    public int DecisionCodeId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime StartDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime EndDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime IssuedDate { get; set; }

    public bool IsTrusted { get; set; }

    public bool IsWholeSale { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string WholeSaleRetailId { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ReopenDate { get; set; }

    [Column("IRDStatus")]
    [StringLength(50)]
    public string? Irdstatus { get; set; }
}
