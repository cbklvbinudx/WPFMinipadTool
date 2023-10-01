
namespace MinipadWPFTest.Models
{
    public class SensorValues
    {
        public SensorValues() 
        {
            RawValues = new int[3];
            MappedValues = new int[3];
        }
        public SensorValues(int keyCount) 
        {
            RawValues = new int[keyCount];
            MappedValues = new int[keyCount];
            for (int i = 0; i < keyCount; i++)
            {
                RawValues[i] = -1;
                MappedValues[i] = -1;
            }
        }

        public int[] RawValues { get; set; }
        public int[] MappedValues { get; set; }
    }
}
