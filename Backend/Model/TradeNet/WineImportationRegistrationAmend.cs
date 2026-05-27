using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("WineImportationRegistrationAmend")]
public partial class WineImportationRegistrationAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string WineImportationRegistrationId { get; set; } = null!;

    public bool IsName { get; set; }

    [Column("IsNRC")]
    public bool IsNrc { get; set; }

    [Column("IsOldNRC")]
    public bool IsOldNrc { get; set; }

    [Column("IsIsFL11")]
    public bool IsIsFl11 { get; set; }

    [Column("IsFL11Name")]
    public bool IsFl11name { get; set; }

    [Column("IsFL11NRC")]
    public bool IsFl11nrc { get; set; }

    [Column("IsFL11OldNRC")]
    public bool IsFl11oldNrc { get; set; }

    [Column("IsFL11LicenceValidDate")]
    public bool IsFl11licenceValidDate { get; set; }

    [Column("IsIsFL4")]
    public bool IsIsFl4 { get; set; }

    [Column("IsFL4Name")]
    public bool IsFl4name { get; set; }

    [Column("IsFL4NRC")]
    public bool IsFl4nrc { get; set; }

    [Column("IsFL4OldNRC")]
    public bool IsFl4oldNrc { get; set; }

    [Column("IsFL4LicenceValidDate")]
    public bool IsFl4licenceValidDate { get; set; }

    [Column("IsIsFL5")]
    public bool IsIsFl5 { get; set; }

    [Column("IsFL5Name")]
    public bool IsFl5name { get; set; }

    [Column("IsFL5NRC")]
    public bool IsFl5nrc { get; set; }

    [Column("IsFL5OldNRC")]
    public bool IsFl5oldNrc { get; set; }

    [Column("IsFL5LicenceValidDate")]
    public bool IsFl5licenceValidDate { get; set; }

    public bool IsBusinessTypeId { get; set; }

    public bool IsWineTypeId { get; set; }
}
