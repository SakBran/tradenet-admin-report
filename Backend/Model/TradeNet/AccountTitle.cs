using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("AccountTitle")]
public partial class AccountTitle
{
    [Key]
    public int Id { get; set; }

    public int ChequeNoId { get; set; }

    public int AccountTypeId { get; set; }

    [StringLength(50)]
    public string Code { get; set; } = null!;

    public string Description { get; set; } = null!;

    [StringLength(200)]
    public string FormType { get; set; } = null!;

    [StringLength(50)]
    public string ApplyType { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedUserId { get; set; }
}
