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
using System.Globalization;
using TraceSourceLogger;
using TradeHub.Common.Core.DomainModels;
using TradeHub.Common.Core.ValueObjects.MarketData;
using Constants = TradeHub.Common.Core.Constants;

namespace TradeSharp.MarketDataProvider.IQFeed.Provider
{
    /// <summary>
    /// Responsible for providing Live Bars
    /// </summary>
    internal class BarData
    {
        private Type _type = typeof(BarData);

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
        private DTNIQFeedCOMLib.StreamingIntervalBars _streamingBarsComObject;

        #region Events

        // ReSharper Disable InconsistentNaming
        private event Action<Bar> _barDataEvent;
        // ReSharper Enable InconsistentNaming

        /// <summary>
        /// Event is raised to new Bar is received
        /// </summary>
        public event Action<Bar> BarDataEvent
        {
            add
            {
                if (_barDataEvent == null)
                {
                    _barDataEvent += value;
                }
            }
            remove { _barDataEvent -= value; }
        }

        #endregion

        /// <summary>
        /// Argument Constructor
        /// </summary>
        /// <param name="logger">logger instance of the calling class</param>
        public BarData(AsyncClassLogger logger)
        {
            // Save instance
            _logger = logger;

            // Set provider name to be used in all calls
            _marketDataProviderName = Constants.MarketDataProvider.IqFeed;
        }

        /// <summary>
        /// Opens a connection to the IQFeed Connector
        /// </summary>
        public void OpenBarDataConnection()
        {
            try
            {
                // Initialize IQFeed COM object
                _streamingBarsComObject = new DTNIQFeedCOMLib.StreamingIntervalBars();

                // Hook necessary IQ Feed events
                RegisterEvents();

                // Use request to initiate the connection and set our IQFeed protocol here.
                _streamingBarsComObject.SetProtocol("5.2");
            }
            catch (Exception exception)
            {
                _logger.Error(exception, _type.FullName, "OpenBarDataConnection");
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
            _streamingBarsComObject.OnIntervalBarCompleteMsg += OnReceiveData;
        }

        /// <summary>
        /// Un-Subscribes existing IQ Feed events for Level 1 Data
        /// </summary>
        private void UnregisterEvents()
        {
            // Add handlers for the various message types
            _streamingBarsComObject.OnIntervalBarCompleteMsg -= OnReceiveData;
        }

        /// <summary>
        /// Sends subcription request to IQ Feed Connnector
        /// </summary>
        /// <param name="barDataRequest"></param>
        public void Subscribe(BarDataRequest barDataRequest)
        {
            // Send new request
            _streamingBarsComObject.BarWatch(barDataRequest.Security.Symbol.ToUpperInvariant(), (int)barDataRequest.BarLength,
                "", // begin date time
                1, // Number of days
                100, // max data points
                "", // begin filter time
                "", // end filter time
                barDataRequest.Id, // request id
                "s", // interval type 's' - seconds, 't' - tick, 'v' - volume
                0); // update interval in seconds
        }

        /// <summary>
        /// Sends un-subcription request to IQ Feed Connnector
        /// </summary>
        /// <param name="barDataRequest"></param>
        public void Unsubscribe(BarDataRequest barDataRequest)
        {
            // Un-subscribe
            _streamingBarsComObject.BarUnwatch(barDataRequest.Security.Symbol.ToUpperInvariant(), barDataRequest.Id);
        }

        /// <summary>
        /// Called when new complete bar is received
        /// </summary>
        /// <param name="receivedData"></param>
        private void OnReceiveData(ref string receivedData)
        {
            // Convert into TradeSharp Bar message
            var bar = ParseBarData(receivedData);

            // Raise Event to notify listeners
            if (bar != null)
            {
                if (_barDataEvent != null)
                {
                    _barDataEvent(bar);
                }
            }
        }

        /// <summary>
        /// Converts the incoming data string to TradeSharp Bar message
        /// </summary>
        /// <param name="barData"></param>
        /// <returns></returns>
        private Bar ParseBarData(string barData)
        {
            try
            {
                // Split incoming data
                string[] barDataArray = barData.Split(',');

                var security = new Security() { Symbol = barDataArray[2] };
                var requestId = barDataArray[0];
                var dateTime = DateTime.ParseExact(barDataArray[3], "yyyy-MM-d HH:mm:ss", CultureInfo.InvariantCulture);

                // Create new Bar object
                Bar bar = new Bar(security, _marketDataProviderName, requestId, dateTime);

                // Set bar fields
                bar.Open = Convert.ToDecimal(barDataArray[4]);
                bar.High = Convert.ToDecimal(barDataArray[5]);
                bar.Low = Convert.ToDecimal(barDataArray[6]);
                bar.Close = Convert.ToDecimal(barDataArray[7]);

                bar.Volume = Convert.ToInt64(barDataArray[9]);

                // Return newly created bar object
                return bar;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, _type.FullName, "ParseBarData");
                return null;
            }
        }

        /// <summary>
        /// Stops necessary communication
        /// </summary>
        public void Stop()
        {
            _streamingBarsComObject.UnwatchAll();
        }
    }
}
