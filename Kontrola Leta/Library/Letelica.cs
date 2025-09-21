using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Library
{
    [Serializable]
    public class Letelica
    {
        public string imeLetelice { get; set; }
        public string imePilota { get; set; }
        public string registracijaLetelice { get; set; }
        public int maxPutnika { get; set; }
        public int trenutnoPutnika { get; set; }
    }
}
