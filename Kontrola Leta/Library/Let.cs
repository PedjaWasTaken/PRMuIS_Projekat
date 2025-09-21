using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Library
{
    [Serializable]
    public class Let
    {
        public Letelica letelica { get; set; }

        public int axisStartX { get; set; }
        public int axisStartY { get; set; }
        public int axisStartZ { get; set; }

        public int axisEndX { get; set; }
        public int axisEndY { get; set; }
        public int axisEndZ { get; set; }
    }
}
