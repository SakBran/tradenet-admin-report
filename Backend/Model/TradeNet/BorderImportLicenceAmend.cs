using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("BorderImportLicenceAmend")]
public partial class BorderImportLicenceAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string BorderImportLicenceId { get; set; } = null!;

    public bool IsSellerName { get; set; }

    public bool IsSellerAddress { get; set; }

    public bool IsSellerCountryId { get; set; }

    public bool IsPortofDischargeId { get; set; }

    public bool IsModeofTransport { get; set; }

    public bool IsExportImportMethodId { get; set; }

    public bool IsConsignedCountryId { get; set; }

    public bool IsCountryofOriginId { get; set; }

    public bool IsExportImportIncotermId { get; set; }

    public bool IsCommodityType { get; set; }

    public bool IsUsage { get; set; }

    public bool IsRemark { get; set; }
}
