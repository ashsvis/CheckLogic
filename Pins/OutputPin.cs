using System;

namespace CheckLogic
{
    [Serializable]
    public class OutputCollection
    {
        private readonly OutputPin[] _arr = new OutputPin[32];

        public OutputCollection()
        {
            for (var i = 0; i < _arr.Length; i++)
                _arr[i] = new OutputPin();
        }

        public OutputPin this[uint i]
        {
            get
            {
                if (i >= 1 && i <= 32)
                    return _arr[i - 1];
                if (i == 0) return null;
                throw new IndexOutOfRangeException("Индекс за границами массива!");
            }
            set
            {
                if (i >= 1 && i <= 32)
                    _arr[i - 1] = value;
                else
                    if (i != 0)
                        throw new IndexOutOfRangeException("Индекс за границами массива!");
            }
        }

        private uint _length = 1;

        public uint Length
        {
            get { return _length; }
            set
            {
                if (value >= 1 && value <= 32)
                    _length = value;
                else
                    if (value != 0)
                        throw new IndexOutOfRangeException("Значение за границами массива!");
            }
        }
    }

    [Serializable]
    public class OutputPin
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public bool Invert { get; set; }
        public LinkPin Link { get; set; }
    }
}
