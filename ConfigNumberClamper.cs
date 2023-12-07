using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace YoutubeBoombox
{
    public class ConfigNumberClamper : AcceptableValueBase
    {
        internal int Minimum { get; private set; } = int.MinValue;
        internal int Maximum { get; private set; } = int.MaxValue;

        public ConfigNumberClamper(int min, int max) : base(typeof(int)) 
        {
            Minimum = min;
            Maximum = max;
        }

        public override object Clamp(object value)
        {
            return Mathf.Clamp((int)value, Minimum, Maximum);
        }

        public override bool IsValid(object value)
        {
            int val = (int)value;

            return val >= Minimum && val <= Maximum;
        }

        public override string ToDescriptionString()
        {
            return $"# Range: [{Minimum}, {Maximum}]";
        }
    }
}
