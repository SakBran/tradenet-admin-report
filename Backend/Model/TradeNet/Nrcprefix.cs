using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("NRCPrefix")]
public partial class Nrcprefix
{
    [Key]
    public int Id { get; set; }

    public int StatePrefix { get; set; }

    [StringLength(20)]
    public string TownshipPrefix { get; set; } = null!;

    [Column("IMGTownshipPrefix")]
    [StringLength(20)]
    public string? ImgtownshipPrefix { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public int CreatedUserId { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
