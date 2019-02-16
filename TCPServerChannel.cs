/*
 *  Copyright (C) 2015-2019  Igor Tyulyakov aka g10101k, g101k. Contacts: <g101k@mail.ru>
 *  
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *
 *       http://www.apache.org/licenses/LICENSE-2.0
 *
 *   Unless required by applicable law or agreed to in writing, software
 *   distributed under the License is distributed on an "AS IS" BASIS,
 *   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *   See the License for the specific language governing permissions and
 *   limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Indusoft.Alcor.Common;
using Indusoft.Alcor.Common.Channels;
using Indusoft.Alcor.Common.WCFService.DTO;
using Indusoft.Alcor.DB.Config;
using Indusoft.Alcor.LogServer;

namespace Indusoft.Alcor.ChannelManager.Channels
{
    internal class TCPServerChannel : BaseChannel
    {
        private TcpClient _client;
        private object _sync = new object();
        private object _openSync = new object();

        private Task _receiveTask;
        private Task _reconnectTask;
        private CancellationTokenSource _cancellationToken = new CancellationTokenSource();

        private string _host;
        private int _port;
        private TimeSpan _resubscribeTime;
        private uint _keepAliveTime;

        public TCPServerChannel(DatabaseConfiguration.SimpleChannel simpleChannel) : base(simpleChannel)
        {
            var HostPort = _channel.Address.Split(new char[] { ':' });
            _host = HostPort[0];
            _port = Convert.ToInt32(HostPort[1]);

            _resubscribeTime = TimeSpan.Zero;
            var resubscribeTimeParam = simpleChannel.GetParameter(Constants.ResubscribeTime);
            if (resubscribeTimeParam != null)
                _resubscribeTime = TimeSpan.Parse(resubscribeTimeParam.ValueAsString);

            _keepAliveTime = 60000;
            var keepAliveTimeParam = simpleChannel.GetParameter(Constants.KeepAliveTime);
            if (keepAliveTimeParam != null)
                _keepAliveTime = uint.Parse(keepAliveTimeParam.ValueAsString);

            _receiveTask = new Task(ReceiveTask, _cancellationToken.Token, TaskCreationOptions.LongRunning);
            _receiveTask.Start();

            if (_resubscribeTime.TotalSeconds > 0)
            {
                _reconnectTask = new Task(ReconnectTask, _cancellationToken.Token, TaskCreationOptions.LongRunning);
                _reconnectTask.Start();
            }
        }

        public static bool IsConnected(TcpClient client)
        {
            try
            {
                bool connected = client != null && client.Client != null && client.Connected && !(client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0);

                return connected;
            }
            catch
            {
                return false;
            }
        }

        private void ReconnectTask(object state)
        {
            CancellationToken token = (CancellationToken)state;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    int sleep = 0;
                    while (sleep < _resubscribeTime.TotalSeconds)
                    {
                        Thread.Sleep(1000);
                        sleep++;
                        if (token.IsCancellationRequested)
                            return;
                    }
                    _client.Client.Shutdown(SocketShutdown.Both);
                    _client.Client.Disconnect(true);
                }
                catch (Exception ex)
                {
                    _logger.Error(nameof(ReconnectTask), ex);
                }
            }
        }

        private void ReceiveTask(object state)
        {
            CancellationToken token = (CancellationToken)state;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (IsConnected(_client))
                    {
                        if (_client.Client.Available > 0)
                        {
                            lock (_sync)
                            {
                                int toread = _client.Client.Available;
                                byte[] buffer = new byte[toread];
                                _client.Client.Receive(buffer, 0, toread, SocketFlags.None);
                                _internalBuffer.AddRange(buffer);
                                _channel.SendNewMonitoringData(new ChannelDataDTO() { ChannelName = Name, Timestamp = DateTime.UtcNow, ChannelDataType = ChannelDataTypeDTO.Read, DeviceName = null, Data = buffer });
                                if (OnData != null)
                                {
                                    byte[] buf = new byte[_internalBuffer.Count];
                                    InternalRead(null, buf, 0, buf.Length);
                                    OnData?.Invoke(this, buf);
                                }
                            }
                        }
                        else
                            Thread.Sleep(10);
                    }
                    else
                    {
                        InternalOpen(string.Empty, RequestPriority.Read, null, null);
                    }
                    Thread.Sleep(10);
                    HeartbeatLogger.Heartbeat(GetType().ToString(), Environment.StackTrace);
                }
                catch (Exception ex)
                {
                    _logger.Error(nameof(ReceiveTask), ex);
                }
                Thread.Sleep(10);
            }
        }

        public override event EventHandler<byte[]> OnData;

        public override void InternalClose()
        {
            if (_client != null && _client.Client != null && _client.Connected)
                _client.Close();
        }

        // Convert tcp_keepalive C struct To C# struct
        [
               System.Runtime.InteropServices.StructLayout
               (
                   System.Runtime.InteropServices.LayoutKind.Explicit
               )
        ]
        unsafe struct TcpKeepAlive
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            [
                  System.Runtime.InteropServices.MarshalAs
                   (
                       System.Runtime.InteropServices.UnmanagedType.ByValArray,
                       SizeConst = 12
                   )
            ]
            public fixed byte Bytes[12];

            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint On_Off;

            [System.Runtime.InteropServices.FieldOffset(4)]
            public uint KeepaLiveTime;

            [System.Runtime.InteropServices.FieldOffset(8)]
            public uint KeepaLiveInterval;
        }

        public int SetKeepAliveValues(System.Net.Sockets.Socket Socket, bool On_Off, uint KeepaLiveTime, uint KeepaLiveInterval)
        {
            int Result = -1;

            unsafe
            {
                TcpKeepAlive KeepAliveValues = new TcpKeepAlive();

                KeepAliveValues.On_Off = Convert.ToUInt32(On_Off);
                KeepAliveValues.KeepaLiveTime = KeepaLiveTime;
                KeepAliveValues.KeepaLiveInterval = KeepaLiveInterval;

                byte[] InValue = new byte[12];

                for (int I = 0; I < 12; I++)
                    InValue[I] = KeepAliveValues.Bytes[I];

                Result = Socket.IOControl(IOControlCode.KeepAliveValues, InValue, null);
            }

            return Result;
        }

        public override bool InternalOpen(string drvName, RequestPriority priority, IAttributes attributes, IParameters parameters)
        {
            try
            {
                lock (_openSync)
                {
                    if (IsConnected(_client))
                        return true;
                    _client = new TcpClient();
                    _client.ReceiveTimeout = readTimeout;
                    _client.SendTimeout = writeTimeout;
                    _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    SetKeepAliveValues(_client.Client, true, _keepAliveTime, 5000);
                    _client.Connect(_host, _port);
                    _logger.Debug("InternalOpen", "InternalOpen({0}:{1}, keepAlive:{2})", _host, _port, _keepAliveTime);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("InternalOpen", ex);
                Thread.Sleep(_reconnectTimeout);
            }
            return false;
        }

        public override int InternalRead(string drvName, byte[] buffer, int offset, int maxBytesToRead)
        {
            try
            {
                lock (_sync)
                {
                    byte[] buf = _internalBuffer.ToArray();
                    int len = (maxBytesToRead - offset) > buf.Length ? buf.Length : maxBytesToRead - offset;
                    Array.ConstrainedCopy(buf, 0, buffer, offset, len);
                    _internalBuffer.RemoveRange(0, len);
                    if (len > 0)
                        _channel.SendNewMonitoringData(new ChannelDataDTO() { ChannelName = Name, Timestamp = DateTime.UtcNow, ChannelDataType = ChannelDataTypeDTO.Read, DeviceName = drvName, Data = buffer.Take(len).ToArray() });
                    return len;
                    //return _serialPort.Read(buffer, offset, maxBytesToRead);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("InternalRead", ex);
                return 0;
            }
        }

        public override int InternalWrite(string drvName, byte[] buffer, int offset, int bytesToWrite)
        {
            var sended = _client.Client.Send(buffer, offset, bytesToWrite, SocketFlags.None);
            _channel.SendNewMonitoringData(new ChannelDataDTO() { ChannelName = Name, Timestamp = DateTime.UtcNow, ChannelDataType = ChannelDataTypeDTO.Write, DeviceName = null, Data = buffer.Take(sended).ToArray() });
            _channel.SendNewMonitoringData(new ChannelDataDTO() { ChannelName = Name, Timestamp = DateTime.UtcNow, ChannelDataType = ChannelDataTypeDTO.Write, DeviceName = drvName, Data = buffer.Take(sended).ToArray() });
            return sended;
        }

        ~TCPServerChannel()
        {
            _cancellationToken.Cancel();
        }
    }
}