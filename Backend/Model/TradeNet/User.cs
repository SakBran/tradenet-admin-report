using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

public partial class User
{
    [Key]
    public int Id { get; set; }

    [StringLength(200)]
    public string UserType { get; set; } = null!;

    public int SakhanId { get; set; }

    [StringLength(200)]
    public string FullName { get; set; } = null!;

    public int UserCode { get; set; }

    [StringLength(200)]
    public string UserName { get; set; } = null!;

    [StringLength(200)]
    public string Password { get; set; } = null!;

    [StringLength(200)]
    public string Position { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool IsOpened { get; set; }

    public bool IsDeleted { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int? UpdatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }
}
