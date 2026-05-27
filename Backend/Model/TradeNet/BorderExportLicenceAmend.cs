using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("BorderExportLicenceAmend")]
public partial class BorderExportLicenceAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string BorderExportLicenceId { get; set; } = null!;

    public bool IsBuyerName { get; set; }

    public bool IsBuyerAddress { get; set; }

    public bool IsBuyerCountryId { get; set; }

    public bool IsPortofExportId { get; set; }

    public bool IsPortofDischarge { get; set; }

    public bool IsModeofTransport { get; set; }

    public bool IsExportImportMethodId { get; set; }

    public bool IsCountryofOriginId { get; set; }

    public bool IsConsignedCountryId { get; set; }

    public bool IsDestinationCountryId { get; set; }

    public bool IsExportImportIncotermId { get; set; }

    public bool IsCommodityType { get; set; }

    public bool IsRemark { get; set; }
}
