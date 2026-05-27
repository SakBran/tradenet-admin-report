using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

public partial class DeleteDatum
{
    [Key]
    public int Id { get; set; }

    public string ApplyType { get; set; } = null!;

    public string ApplicationNo { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaId { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string IndividualTradingId { get; set; } = null!;

    public string CardType { get; set; } = null!;

    public string LicenceNo { get; set; } = null!;

    public string OldLicenceNo { get; set; } = null!;

    public string Status { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string MemberId { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string? ApplicationId { get; set; }

    public string? Source { get; set; }

    [Column("TotalCIF")]
    public double? TotalCif { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? ExchangeRate { get; set; }

    public string FormType { get; set; } = null!;

    [Column("JSON")]
    public string Json { get; set; } = null!;

    public string TransactionId { get; set; } = null!;

    public int? SakhanId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ApplicationDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }
}
