using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Keyless]
[Table("MCBLog")]
public partial class Mcblog
{
    [Column("contextId")]
    public string? ContextId { get; set; }

    [Column("paymentGatewayUrl")]
    public string? PaymentGatewayUrl { get; set; }

    [Column("merchantPaymentRef")]
    public string? MerchantPaymentRef { get; set; }
}
