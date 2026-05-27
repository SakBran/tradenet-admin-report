using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("IndividualTrading")]
public partial class IndividualTrading
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string? MemberId { get; set; }

    [StringLength(10)]
    public string SakhanCode { get; set; } = null!;

    [StringLength(50)]
    public string IndividualTradingNo { get; set; } = null!;

    [Column("TINNo")]
    [StringLength(50)]
    public string Tinno { get; set; } = null!;

    public int PaThaKaTypeId { get; set; }

    public int CardRegistrationFeesId { get; set; }

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

    [StringLength(200)]
    public string? FatherName { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime DateofBirth { get; set; }

    [StringLength(128)]
    public string? UnitLevel { get; set; }

    [StringLength(128)]
    public string StreetNumberStreetName { get; set; } = null!;

    [StringLength(64)]
    public string QuarterCityTownship { get; set; } = null!;

    [StringLength(64)]
    public string State { get; set; } = null!;

    [StringLength(200)]
    public string Country { get; set; } = null!;

    [StringLength(8)]
    public string? PostalCode { get; set; }

    [StringLength(50)]
    public string Mobile1 { get; set; } = null!;

    [StringLength(50)]
    public string? Mobile2 { get; set; }

    [StringLength(50)]
    public string? Mobile3 { get; set; }

    [StringLength(50)]
    public string? Fax { get; set; }

    [StringLength(200)]
    public string Email { get; set; } = null!;

    public string? Remark { get; set; }

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

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
