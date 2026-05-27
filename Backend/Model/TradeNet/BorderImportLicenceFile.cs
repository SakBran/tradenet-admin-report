using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

public partial class BorderImportLicenceFile
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string BorderImportLicenceId { get; set; } = null!;

    public int DocumentTypeId { get; set; }

    [StringLength(255)]
    public string Url { get; set; } = null!;

    [StringLength(200)]
    public string Filename { get; set; } = null!;

    [StringLength(200)]
    public string Attachname { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string? ParentId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
