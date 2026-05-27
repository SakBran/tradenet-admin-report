using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("BorderImportPermitAmend")]
public partial class BorderImportPermitAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string BorderImportPermitId { get; set; } = null!;

    [Column("IsNRC")]
    public bool IsNrc { get; set; }

    [Column("IsOldNRC")]
    public bool IsOldNrc { get; set; }

    public bool IsPassportNo { get; set; }

    public bool IsAuthorisedAgentName { get; set; }

    public bool IsAuthorisedAgentAddress { get; set; }

    public bool IsSellerCountryId { get; set; }

    public bool IsPortofShipmentId { get; set; }

    public bool IsCountryofOriginId { get; set; }

    public bool IsPortofDischargeId { get; set; }

    public bool IsPermitType { get; set; }

    public bool IsRemark { get; set; }

    public bool IsCommodityType { get; set; }
}
