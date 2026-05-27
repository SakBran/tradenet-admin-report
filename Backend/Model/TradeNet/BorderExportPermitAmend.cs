using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("BorderExportPermitAmend")]
public partial class BorderExportPermitAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string BorderExportPermitId { get; set; } = null!;

    [Column("IsNRC")]
    public bool IsNrc { get; set; }

    [Column("IsOldNRC")]
    public bool IsOldNrc { get; set; }

    public bool IsConsigneeName { get; set; }

    public bool IsConsigneeAddress { get; set; }

    public bool IsBuyerCountryId { get; set; }

    public bool IsModeofTransport { get; set; }

    public bool IsPortofExportId { get; set; }

    public bool IsPortofDischarge { get; set; }

    public bool IsDestinationCountryId { get; set; }

    public bool IsConsignedCountryId { get; set; }

    public bool IsCountryofOriginId { get; set; }

    public bool IsPermitType { get; set; }

    public bool IsRemark { get; set; }

    public bool IsCommodityType { get; set; }
}
