using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Model
{
    [Table("ImportLicence")]
    public class ImportLicenceDailyImport
    {
        [Key]
        public int Id { get; set; }

        public int TotalCount { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "date")]
        public DateTime? LicenceDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime CreatedDate { get; set; }
    }
}
