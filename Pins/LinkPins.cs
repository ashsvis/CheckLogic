using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace CheckLogic
{
    [Serializable]
    public class LinkPin
    {
        public void TransferLink(object value)
        {
            foreach (var target in _targets
                .Where(target => target.Pin <= target.Module.Inputs.Length))
                target.Module.Inputs[target.Pin].Value = value;
        }

        public LinkPin(ModulePin pin)
        {
            _targets.Add(pin);
        }

        private readonly List<ModulePin> _targets = new List<ModulePin>();
        public List<ModulePin> Targets 
        {
            get { return _targets; }
        }
    }

    [Serializable]
    public class ModulePin
    {
        public Plot Module { get; set; }
        public uint Pin { get; set; }
        public uint PageOrder { get; set; }
        public PointF FirstPoint { get; set; }
        public PointF LastPoint { get; set; }
    }
}
