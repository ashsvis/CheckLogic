using System.Collections.Generic;
using System.Drawing;

namespace CheckLogic
{
    public class PlotsOwner : List<Plot> 
    {
        private readonly GetAllPlots _getAllPlots;

        public bool Emulation { get; set; }

        public Color BackColor { get; set; }

        public Brush BackBrushColor { get; set; }

        public PlotsOwner(GetAllPlots getAllPlots)
        {
            _getAllPlots = getAllPlots;
        }

        public IEnumerable<Plot> GetAllPlots()
        {
            return _getAllPlots != null ? _getAllPlots() : this;
        }
    }
}