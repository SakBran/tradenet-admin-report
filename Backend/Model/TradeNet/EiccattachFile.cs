using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("EICCAttachFiles")]
public partial class EiccattachFile
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string TransactionId { get; set; } = null!;

    [StringLength(255)]
    public string Url { get; set; } = null!;

    [StringLength(200)]
    public string Filename { get; set; } = null!;

    [StringLength(200)]
    public string Attachname { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int CreatedUserId { get; set; }
}
