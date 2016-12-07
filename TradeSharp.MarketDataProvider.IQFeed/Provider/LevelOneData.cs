/***************************************************************************** 
* Copyright 2016 Aurora Solutions 
* 
*    http://www.aurorasolutions.io 
* 
* Aurora Solutions is an innovative services and product company at 
* the forefront of the software industry, with processes and practices 
* involving Domain Driven Design(DDD), Agile methodologies to build 
* scalable, secure, reliable and high performance products.
* 
* TradeSharp is a C# based data feed and broker neutral Algorithmic 
* Trading Platform that lets trading firms or individuals automate 
* any rules based trading strategies in stocks, forex and ETFs. 
* TradeSharp allows users to connect to providers like Tradier Brokerage, 
* IQFeed, FXCM, Blackwood, Forexware, Integral, HotSpot, Currenex, 
* Interactive Brokers and more. 
* Key features: Place and Manage Orders, Risk Management, 
* Generate Customized Reports etc 
* 
* Licensed under the Apache License, Version 2.0 (the "License"); 
* you may not use this file except in compliance with the License. 
* You may obtain a copy of the License at 
* 
*    http://www.apache.org/licenses/LICENSE-2.0 
* 
* Unless required by applicable law or agreed to in writing, software 
* distributed under the License is distributed on an "AS IS" BASIS, 
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
* See the License for the specific language governing permissions and 
* limitations under the License. 
*****************************************************************************/


using System;
using TraceSourceLogger;
using TradeHub.Common.Core.DomainModels;
using Constants = TradeHub.Common.Core.Constants;

namespace TradeSharp.MarketDataProvider.IQFeed.Provider
{/// <summary>
 /// Responsible for providing Level 1 Data
 /// </summary>
    internal class LevelOneData
    {
        private Type _type = typeof(LevelOneData);

        /// <summary>
        /// Holds the logger instance of the calling class
        /// </summary>
        private AsyncClassLogger _logger;

        /// <summary>
        /// Contains Provider name used through out TradeSharp
        /// </summary>
        private readonly string _marketDataProviderName;

        /// <summary>
        /// Responsible for communicating with the IQFeed Connector
        /// </summary>
        private DTNIQFeedCOMLib.LevelOne2 _levelOneComObject;

        /// <summary>
        /// Indicates if the data server is connected
        /// </summary>
        private bool _connected = false;

        #region Events

        // ReSharper Disable InconsistentNaming
        private event Action<bool> _connectionEvent;
        private event Action<Tick> _dataEvent;
        // ReSharper Enable InconsistentNaming

        /// <summary>
        /// Event is raised when connection state changes
        /// </summary>
        public event Action<bool> ConnectionEvent
        {
            add
            {
                if (_connectionEvent == null)
                {
                    _connectionEvent += value;
                }
            }
            remove { _connectionEvent -= value; }
        }

        /// <summary>
        /// Event is raised to new Tick data is received
        /// </summary>
        public event Action<Tick> DataEvent
        {
            add
            {
                if (_dataEvent == null)
                {
                    _dataEvent += value;
                }
            }
            remove { _dataEvent -= value; }
        }

        #endregion

        /// <summary>
        /// Argument Constructor
        /// </summary>
        /// <param name="logger">logger instance of the calling class</param>
        public LevelOneData(AsyncClassLogger logger)
        {
            // Save instance
            _logger = logger;

            // Set provider name to be used in all calls
            _marketDataProviderName = Constants.MarketDataProvider.IqFeed;
        }

        /// <summary>
        /// Indicates if the data server is connected
        /// </summary>
        public bool Connected
        {
            get { return _connected; }
        }

        /// <summary>
        /// Opens a connection to the IQFeed Connector
        /// </summary>
        public void OpenLevelOneDataConnection()
        {
            try
            {
                // Initialize IQFeed COM object
                _levelOneComObject = new DTNIQFeedCOMLib.LevelOne2();

                // Hook necessary IQ Feed events
                RegisterEvents();

                // Use request to initiate the connection and set our IQFeed protocol here.
                _levelOneComObject.SetProtocol("5.2");
            }
            catch (Exception exception)
            {
                _logger.Error(exception, _type.FullName, "OpenLevelOneDataConnection");
            }
        }

        /// <summary>
        /// Subscribes to necessary IQ Feed events for Level 1 Data
        /// </summary>
        private void RegisterEvents()
        {
            // Makes sure that events are subscribed only once
            UnregisterEvents();

            // Add handlers for the various message types
            _levelOneComObject.OnSummaryMsg += new DTNIQFeedCOMLib._ILevelOne2Events_OnSummaryMsgEventHandler(OnSummaryMessage);
            _levelOneComObject.OnUpdateMsg += new DTNIQFeedCOMLib._ILevelOne2Events_OnUpdateMsgEventHandler(OnUpdateMessage);
            _levelOneComObject.OnSystemMsg += new DTNIQFeedCOMLib._ILevelOne2Events_OnSystemMsgEventHandler(OnSystemMessage);
            _levelOneComObject.OnErrorMsg += new DTNIQFeedCOMLib._ILevelOne2Events_OnErrorMsgEventHandler(OnErrorMessage);
        }

        /// <summary>
        /// Un-Subscribes existing IQ Feed events for Level 1 Data
        /// </summary>
        private void UnregisterEvents()
        {
            // Add handlers for the various message types
            _levelOneComObject.OnSummaryMsg -= new DTNIQFeedCOMLib._ILevelOne2Events_OnSummaryMsgEventHandler(OnSummaryMessage);
            _levelOneComObject.OnUpdateMsg -= new DTNIQFeedCOMLib._ILevelOne2Events_OnUpdateMsgEventHandler(OnUpdateMessage);
            _levelOneComObject.OnSystemMsg -= new DTNIQFeedCOMLib._ILevelOne2Events_OnSystemMsgEventHandler(OnSystemMessage);
            _levelOneComObject.OnErrorMsg -= new DTNIQFeedCOMLib._ILevelOne2Events_OnErrorMsgEventHandler(OnErrorMessage);
        }

        /// <summary>
        /// Sends subscription request to IQFeed Connector
        /// </summary>
        /// <param name="symbol"></param>
        public void Subscribe(string symbol)
        {
            if (_connected)
            {
                // Request data
                _levelOneComObject.ReqWatch(symbol.ToUpperInvariant());
            }
        }

        /// <summary>
        /// Sends Symbol un-subscription request to IQFeed Connector
        /// </summary>
        /// <param name="symbol"></param>
        public void Unsubscribe(string symbol)
        {
            if (_connected)
            {
                // Remove symbol
                _levelOneComObject.ReqUnwatch(symbol.ToUpperInvariant());
            }
        }

        #region Events

        /// <summary>
        /// Fires when the objLevelOne COM object sends the app a Summary message
        /// </summary>
        /// <param name="summaryMessage"></param>
        private void OnSummaryMessage(ref string summaryMessage)
        {
            // Parse incoming data to Tick
            Tick tick = ParseMarketData(summaryMessage);

            // Raise event to notify listeners
            if (tick != null)
            {
                if (_dataEvent != null)
                {
                    _dataEvent(tick);
                }
            }
        }

        /// <summary>
        /// Fires when the objLevelOne COM object sends the app a Update message
        /// </summary>
        /// <param name="updateMessage"></param>
        private void OnUpdateMessage(ref string updateMessage)
        {
            // Parse incoming data to Tick
            Tick tick = ParseMarketData(updateMessage);

            // Raise event to notify listeners
            if (tick != null)
            {
                if (_dataEvent != null)
                {
                    _dataEvent(tick);
                }
            }
        }

        /// <summary>
        /// Fires when the objLevelOne COM object sends the app a System message
        /// </summary>
        /// <param name="systemMessage"></param>
        private void OnSystemMessage(ref string systemMessage)
        {
            if (systemMessage.Contains("SERVER CONNECTED"))
            {
                if (!_connected)
                {
                    _connected = true;

                    // Sets desired data feeds
                    SetDynamicFieldSets();

                    // Raise Event to notify listeners
                    if (_connectionEvent != null)
                    {
                        _connectionEvent(_connected);
                    }
                }
            }
            else if (systemMessage.Contains("SERVER DISCONNECTED"))
            {
                if (_connected)
                {
                    _connected = false;

                    // Raise Event to notify listeners
                    if (_connectionEvent != null)
                    {
                        _connectionEvent(_connected);
                    }
                }
            }
        }

        /// <summary>
        /// Fires when the objLevelOne COM object sends the app a Error message
        /// </summary>
        /// <param name="errorMessage"></param>
        private void OnErrorMessage(ref string errorMessage)
        {
            _logger.Error(errorMessage, _type.FullName, "OnErrorMessage");
        }

        #endregion

        /// <summary>
        /// Sets required fields to be received in the Data Messages
        /// </summary>
        private void SetDynamicFieldSets()
        {
            string command = @"Symbol,Most Recent Trade,Most Recent Trade Size,Bid,Bid Size,Ask,Ask Size";
            _levelOneComObject.SelectUpdateFieldName(command);
        }

        /// <summary>
        /// Converts the incoming data string to TradeSharp Tick message
        /// </summary>
        /// <param name="marketData"></param>
        /// <returns></returns>
        private Tick ParseMarketData(string marketData)
        {
            try
            {
                string[] dataArray = marketData.Split(',');

                // Get current UTC time
                DateTime dateTime = DateTime.UtcNow;

                // Create new Tick object
                Tick tick = new Tick(new Security() { Symbol = dataArray[1] }, _marketDataProviderName);

                // Extract BID information
                if (!String.IsNullOrEmpty(dataArray[4]))
                {
                    tick.BidPrice = Convert.ToDecimal(dataArray[4]);
                    tick.BidSize = Convert.ToDecimal(dataArray[5]);
                }

                // Extract ASK information
                if (!String.IsNullOrEmpty(dataArray[6]))
                {
                    tick.AskPrice = Convert.ToDecimal(dataArray[6]);
                    tick.AskSize = Convert.ToDecimal(dataArray[7]);
                }

                // Extract LAST/TRADE information
                tick.LastPrice = Convert.ToDecimal(dataArray[2]);
                tick.LastSize = Convert.ToDecimal(dataArray[3]);

                return tick;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, _type.FullName, "ParseMarketData");
                return null;
            }
        }

        /// <summary>
        /// Stops necessary communication
        /// </summary>
        public void Stop()
        {
            // Unsubscribe all symbols
            _levelOneComObject.ReqUnwatchAll();
        }
    }
}
