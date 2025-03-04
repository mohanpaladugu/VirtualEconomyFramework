﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VEDriversLite.Devices.Dto;
using VEDriversLite.NFT;

namespace VEDriversLite.Devices
{
    public enum IoTDataDriverType
    {
        Common,
        HARDWARIO,
        PLFramework,
        M5Stack
    }
    public enum CommunicationSchemeType
    {
        Requests,
        PubSub
    }
    public enum IoTCommunicationType
    {
        API,
        File,
        DbMSSQL,
        DbPostgreSQL,
        DbSQLite,
        MQTT = 100,
        OPCUA = 101
    }

    public interface IIoTDataDriver
    {
        /// <summary>
        /// Name of the driver
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// Driver type
        /// </summary>
        IoTDataDriverType Type { get; set; }
        /// <summary>
        /// Communication Schemes (Requests - classic API), (PubSub - like MQTT)
        /// </summary>
        CommunicationSchemeType ComSchemeType { get; set; }
        /// <summary>
        /// Communication type - REST API, MSSQL Database, MQTT, etc.
        /// </summary>
        IoTCommunicationType IoTComType { get; set; }
        /// <summary>
        /// Main connection parameters
        /// </summary>
        CommonConnectionParams CommonConnParams { get; set; }
        /// <summary>
        /// Cancelation token source for the cancel of automatic loading of the messages
        /// </summary>
        CancellationTokenSource CancelTokenSource { get; set; }
        /// <summary>
        /// Cancelation token for the cancel of automatic loading of the messages
        /// </summary>
        CancellationToken CancelToken { get; set; }
        /// <summary>
        /// Is the Driver running - means autorefresh of the messages
        /// </summary>
        bool IsRunning { get; set; }
        /// <summary>
        /// New message found
        /// </summary>

        event EventHandler<string> NewDataReceived;

        /// <summary>
        /// Init driver communication - means autorefresh of the messages
        /// </summary>
        /// <param name="nft"></param>
        /// <returns></returns>
        Task Init(INFT nft);
        /// <summary>
        /// Deinit driver communiation - means autorefresh of the messages
        /// </summary>
        /// <returns></returns>
        Task DeInit();
        /// <summary>
        /// Set connection parameters
        /// </summary>
        /// <param name="ccop"></param>
        /// <returns></returns>
        Task SetConnParams(CommonConnectionParams ccop);
    }
}
