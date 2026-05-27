using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("MCBPaymentLog")]
public partial class McbpaymentLog
{
    [Column("contextId")]
    public string ContextId { get; set; } = null!;

    [Column("merchantId")]
    public string? MerchantId { get; set; }

    [Column("paymentGatewayUrl")]
    public string? PaymentGatewayUrl { get; set; }

    [Column("redirectUrl")]
    public string? RedirectUrl { get; set; }

    [Column("merchantPaymentRef")]
    public string? MerchantPaymentRef { get; set; }

    [Column("amount", TypeName = "decimal(18, 0)")]
    public decimal? Amount { get; set; }

    [Column("currency")]
    public string? Currency { get; set; }

    [Column("payerFsp")]
    public string? PayerFsp { get; set; }

    [Column("payerId")]
    public string? PayerId { get; set; }

    [Column("transactionRef")]
    public string? TransactionRef { get; set; }

    [Column("transactionTimestamp")]
    public string? TransactionTimestamp { get; set; }

    [Column("payerFspTransactionRef")]
    public string? PayerFspTransactionRef { get; set; }

    [Column("payerFspTransactionTimestamp")]
    public string? PayerFspTransactionTimestamp { get; set; }

    [Column("createTime")]
    public string? CreateTime { get; set; }

    [Column("expiryTime")]
    public string? ExpiryTime { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }
}
