using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("MPUPaymentTransactionLog")]
public partial class MpupaymentTransactionLog
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string? ApplicationNo { get; set; }

    [StringLength(50)]
    public string? InvoiceNo { get; set; }

    [StringLength(50)]
    public string? ResponseCode { get; set; }

    [StringLength(50)]
    public string? AccountNo { get; set; }

    [StringLength(50)]
    public string? TransactionRefNo { get; set; }

    [StringLength(50)]
    public string? ApprovalCode { get; set; }

    [StringLength(50)]
    public string? TransactionDateTime { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }

    [StringLength(200)]
    public string? FailReason { get; set; }

    [Column("hashValue")]
    [StringLength(200)]
    public string? HashValue { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }

    public string? UserDefined1 { get; set; }

    public string? UserDefined2 { get; set; }

    public string? UserDefined3 { get; set; }
}
