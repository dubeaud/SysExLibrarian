using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysExLibrarian.Models
{
    public class SysExFile
    {
        public string Id { get; set; }

        public string FileName { get; set; }

        public string DiskLocation { get; set; }

        public string Manufacturer { get; set; }

        public long Size { get; set; }
    }
}
