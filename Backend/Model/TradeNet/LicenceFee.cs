using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

public partial class LicenceFee
{
    [Key]
    public int Id { get; set; }

    public double FromAmount { get; set; }

    public double ToAmount { get; set; }

    public double LicenceFees { get; set; }
}
