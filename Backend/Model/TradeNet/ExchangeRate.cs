using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("ExchangeRate")]
public partial class ExchangeRate
{
    [Key]
    public int Id { get; set; }

    public int CurrencyId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Rate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime Date { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedUserId { get; set; }
}
