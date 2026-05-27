using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("AccountTransaction")]
public partial class AccountTransaction
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string TransactionId { get; set; } = null!;

    [StringLength(50)]
    public string TransactionFormType { get; set; } = null!;

    public double TotalAmount { get; set; }

    [StringLength(50)]
    public string? VoucherNo { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? VoucherDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? PaymentDate { get; set; }

    public bool IsPayment { get; set; }

    public string? Remark { get; set; }

    [StringLength(20)]
    public string PaymentType { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string? MemberId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int? CreatedUserId { get; set; }
}
