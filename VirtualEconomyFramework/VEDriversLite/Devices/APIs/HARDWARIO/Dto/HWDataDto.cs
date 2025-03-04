﻿using System;
using System.Collections.Generic;
using System.Text;

namespace VEDriversLite.Devices.APIs.HARDWARIO.Dto
{
    public class HWDataDto
    {
        /// <summary>
        /// Tracking data from CHESTER Gateway
        /// </summary>
        public TrackingInfo tracking { get; set; } = new TrackingInfo();
        /// <summary>
        /// Sensor data
        /// </summary>
        public HWSensor sensor { get; set; } = new HWSensor();
    }
}
