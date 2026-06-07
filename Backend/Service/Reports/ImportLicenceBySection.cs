using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DBContext;
using Microsoft.EntityFrameworkCore;

namespace API.Service.Reports
{

    public class ImportLicenceBySection
    {
        public string Section { get; set; } = "";
        public int ExportImportSectionId { get; set; }
        public string Currency { get; set; } = "";
        public decimal NoOfLicences { get; set; }
        public decimal? TotalValue { get; set; }
    }
}
