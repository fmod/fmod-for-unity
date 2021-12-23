using System;

namespace FMODUnity
{
    [Serializable]
    public struct AutomatableSlots
    {
        public float slot00;
        public float slot01;
        public float slot02;
        public float slot03;
        public float slot04;
        public float slot05;
        public float slot06;
        public float slot07;
        public float slot08;
        public float slot09;
        public float slot10;
        public float slot11;
        public float slot12;
        public float slot13;
        public float slot14;
        public float slot15;

        public const int Count = 16;

        public float GetValue(int index)
        {
            switch(index)
            {
                case 0:
                    return slot00;
                case 1:
                    return slot01;
                case 2:
                    return slot02;
                case 3:
                    return slot03;
                case 4:
                    return slot04;
                case 5:
                    return slot05;
                case 6:
                    return slot06;
                case 7:
                    return slot07;
                case 8:
                    return slot08;
                case 9:
                    return slot09;
                case 10:
                    return slot10;
                case 11:
                    return slot11;
                case 12:
                    return slot12;
                case 13:
                    return slot13;
                case 14:
                    return slot14;
                case 15:
                    return slot15;
                default:
                    throw new ArgumentException(string.Format("Invalid slot index: {0}", index));
            }
        }
    }
}
