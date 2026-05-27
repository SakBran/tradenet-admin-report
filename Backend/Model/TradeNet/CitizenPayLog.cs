using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Keyless]
[Table("CitizenPayLog")]
public partial class CitizenPayLog
{
    [Column("id")]
    public string Id { get; set; } = null!;

    [StringLength(50)]
    public string? MerchantId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Amount { get; set; }

    public string? InvoiceNo { get; set; }

    public string? ApplicationNo { get; set; }

    public string? PathakaNo { get; set; }

    public string? FormType { get; set; }

    public string? ApplyType { get; set; }

    public string? LicenceId { get; set; }

    public string? Token { get; set; }

    [Column("responseCode")]
    public string? ResponseCode { get; set; }

    [Column("transactionRef")]
    public string? TransactionRef { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }
}
