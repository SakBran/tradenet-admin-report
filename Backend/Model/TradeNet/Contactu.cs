using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

public partial class Contactu
{
    [Key]
    public int Id { get; set; }

    [StringLength(200)]
    public string? EnglishTitle { get; set; }

    public string? EnglishDescription { get; set; }

    public string? EnglishDepartment { get; set; }

    [StringLength(200)]
    public string? EnglishAddress { get; set; }

    [StringLength(200)]
    public string? EnglishPhone { get; set; }

    [StringLength(200)]
    public string? EnglishFax { get; set; }

    [StringLength(200)]
    public string? MyanmarTitle { get; set; }

    public string? MyanmarDescription { get; set; }

    public string? MyanmarDepartment { get; set; }

    [StringLength(200)]
    public string? MyanmarAddress { get; set; }

    [StringLength(200)]
    public string? MyanmarPhone { get; set; }

    [StringLength(200)]
    public string? MyanmarFax { get; set; }

    [StringLength(200)]
    public string? Email { get; set; }

    [StringLength(200)]
    public string? Website { get; set; }

    public bool IsDeleted { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public int CreatedUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedUserId { get; set; }
}
