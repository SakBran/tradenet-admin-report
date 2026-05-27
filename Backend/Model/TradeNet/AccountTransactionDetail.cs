using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("AccountTransactionDetail")]
public partial class AccountTransactionDetail
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string AccountTransactionId { get; set; } = null!;

    public int AccountTitleId { get; set; }

    public double Amount { get; set; }

    public int SortOrder { get; set; }
}
