using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("ASPStateTempApplications")]
[Index("AppName", Name = "Index_AppName")]
public partial class AspstateTempApplication
{
    [Key]
    public int AppId { get; set; }

    [StringLength(280)]
    [Unicode(false)]
    public string AppName { get; set; } = null!;
}
