using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("WineImportation")]
public partial class WineImportation
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(50)]
    public string WineImportationNo { get; set; } = null!;

    public int CardRegistrationFeesId { get; set; }

    [StringLength(36)]
    [Unicode(false)]
    public string PaThaKaId { get; set; } = null!;

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [Column("NRCType")]
    [StringLength(50)]
    public string Nrctype { get; set; } = null!;

    [Column("NRCPrefixId")]
    public int NrcprefixId { get; set; }

    [Column("NRCPrefixCodeId")]
    public int NrcprefixCodeId { get; set; }

    [Column("NRCNo")]
    [StringLength(50)]
    public string Nrcno { get; set; } = null!;

    [Column("IsFL11")]
    public bool IsFl11 { get; set; }

    [Column("FL11Name")]
    [StringLength(200)]
    public string Fl11name { get; set; } = null!;

    [Column("FL11NRCType")]
    [StringLength(50)]
    public string Fl11nrctype { get; set; } = null!;

    [Column("FL11NRCPrefixId")]
    public int Fl11nrcprefixId { get; set; }

    [Column("FL11NRCPrefixCodeId")]
    public int Fl11nrcprefixCodeId { get; set; }

    [Column("FL11NRCNo")]
    [StringLength(50)]
    public string Fl11nrcno { get; set; } = null!;

    [Column("FL11LicenceValidDate", TypeName = "datetime")]
    public DateTime? Fl11licenceValidDate { get; set; }

    [Column("IsFL4")]
    public bool IsFl4 { get; set; }

    [Column("FL4Name")]
    [StringLength(200)]
    public string Fl4name { get; set; } = null!;

    [Column("FL4NRCType")]
    [StringLength(50)]
    public string Fl4nrctype { get; set; } = null!;

    [Column("FL4NRCPrefixId")]
    public int Fl4nrcprefixId { get; set; }

    [Column("FL4NRCPrefixCodeId")]
    public int Fl4nrcprefixCodeId { get; set; }

    [Column("FL4NRCNo")]
    [StringLength(50)]
    public string Fl4nrcno { get; set; } = null!;

    [Column("FL4LicenceValidDate", TypeName = "datetime")]
    public DateTime? Fl4licenceValidDate { get; set; }

    [Column("IsFL5")]
    public bool IsFl5 { get; set; }

    [Column("FL5Name")]
    [StringLength(200)]
    public string Fl5name { get; set; } = null!;

    [Column("FL5NRCType")]
    [StringLength(50)]
    public string Fl5nrctype { get; set; } = null!;

    [Column("FL5NRCPrefixId")]
    public int Fl5nrcprefixId { get; set; }

    [Column("FL5NRCPrefixCodeId")]
    public int Fl5nrcprefixCodeId { get; set; }

    [Column("FL5NRCNo")]
    [StringLength(50)]
    public string Fl5nrcno { get; set; } = null!;

    [Column("FL5LicenceValidDate", TypeName = "datetime")]
    public DateTime? Fl5licenceValidDate { get; set; }

    public int BusinessTypeId { get; set; }

    [StringLength(20)]
    public string WineTypeId { get; set; } = null!;

    public string? Remark { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime DecisionDate { get; set; }

    public int DecisionCodeId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime StartDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime EndDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime IssuedDate { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }
}
