﻿using System;
using System.Collections.Generic;
using System.Text;

namespace VEDriversLite.Devices.APIs.HARDWARIO.Dto
{
    public class HWSensor
    {
        /// <summary>
        /// Thermometer sensor data
        /// </summary>
        public HWThermometer thermometer { get; set; } = new HWThermometer();
        /// <summary>
        /// Accelerometer sensor data
        /// </summary>
        public HWAccelerometer accelerometer { get; set; } = new HWAccelerometer();
    }
}
