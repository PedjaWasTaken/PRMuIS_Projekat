using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Library
{
    [Serializable]
    public class Sektor
    {
        public int axisX { get; set; }
        public int axisY { get; set; }
        public int axisZ { get; set; }

        public bool zauzet { get; set; } // True ako je zauzet, false ako nije
        public bool meteoroloskiUslovi { get; set; } = false; // True ako su losi, false ako su dobri
    }
}
