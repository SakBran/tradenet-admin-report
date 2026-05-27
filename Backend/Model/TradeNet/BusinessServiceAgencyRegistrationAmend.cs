using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace API.Model.TradeNet;

[Table("BusinessServiceAgencyRegistrationAmend")]
public partial class BusinessServiceAgencyRegistrationAmend
{
    [Key]
    [StringLength(36)]
    [Unicode(false)]
    public string Id { get; set; } = null!;

    [StringLength(36)]
    [Unicode(false)]
    public string BusinessServiceAgencyRegistrationId { get; set; } = null!;

    public bool IsName { get; set; }

    public bool IsBusinessTypeId { get; set; }

    public bool IsAuthorizeCompany { get; set; }

    public bool IsServiceAgent { get; set; }

    public bool IsCommodity { get; set; }
}
