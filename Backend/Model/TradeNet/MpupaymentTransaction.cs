using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("MPUPaymentTransaction")]
public partial class MpupaymentTransaction
{
    [Key]
    public int Id { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? TransactionId { get; set; }

    [StringLength(200)]
    public string? Sakhan { get; set; }

    [StringLength(50)]
    public string? PaThaKaNo { get; set; }

    [StringLength(50)]
    public string? ApplyType { get; set; }

    [StringLength(50)]
    public string? FormType { get; set; }

    [StringLength(50)]
    public string? ApplicationNo { get; set; }

    [StringLength(50)]
    public string? InvoiceNo { get; set; }

    [Column("MOCAmount")]
    [StringLength(50)]
    public string? Mocamount { get; set; }

    [Column("IMAmount")]
    [StringLength(50)]
    public string? Imamount { get; set; }

    [StringLength(50)]
    public string? TransactionAmount { get; set; }

    [StringLength(50)]
    public string? MerchantId { get; set; }

    [StringLength(50)]
    public string? ResponseCode { get; set; }

    [StringLength(50)]
    public string? AccountNo { get; set; }

    [StringLength(50)]
    public string? TransactionRefNo { get; set; }

    [StringLength(50)]
    public string? ApprovalCode { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TransactionDateTime { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }

    [StringLength(200)]
    public string? FailReason { get; set; }

    [Column("hashValue")]
    [StringLength(200)]
    public string? HashValue { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string? MemberId { get; set; }

    [StringLength(50)]
    public string? PaymentType { get; set; }
}
