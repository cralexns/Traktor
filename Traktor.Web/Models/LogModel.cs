using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Traktor.Web.Models
{
    public class LogModel
    {
        public class LogFile
        {
            public DateTime LastWrite { get; set; }
            public string Path { get; set; }
        }
        public List<LogFile> Logs { get; set; }
        public int SelectedLogIndex { get; set; }

        public string[] LogLines { get; set; }
    }
}
