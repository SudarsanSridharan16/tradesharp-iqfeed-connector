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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TradeSharp.MarketDataProvider.IQFeed.Provider;
using TradeHub.Common.Core.DomainModels;
using TradeHub.Common.Core.Constants;
using TradeHub.Common.Core.ValueObjects.MarketData;

namespace TradeSharp.MarketDataProvider.IQFeed.Tests.Integration
{
    public class MarketDataTestCase
    {
        private IqFeedMarketDataProvider _marketDataProvider;

        [SetUp]
        public void SetUp()
        {
            _marketDataProvider = new IqFeedMarketDataProvider();
        }

        [TearDown]
        public void TearDown()
        {
            _marketDataProvider.Stop();
        }

        [Test]
        [Category("Integration")]
        public void Logon_SendRequestToServer_ReceiveLogonArrived()
        {
            bool logonReceived = false;

            var logonManualResetEvent = new ManualResetEvent(false);

            _marketDataProvider.LogonArrived += delegate (string providerName)
            {
                logonReceived = true;
                logonManualResetEvent.Set();
            };

            _marketDataProvider.Start();

            logonManualResetEvent.WaitOne(10000, false);

            Assert.AreEqual(true, logonReceived, "Logon Received");
        }

        [Test]
        [Category("Integration")]
        public void NewSubscription_SendRequestToServer_ReceiveQuoteStreamByServer()
        {
            string symbol = "CME";
            bool logonReceived = false;
            bool tickReceived = false;

            var logonManualResetEvent = new ManualResetEvent(false);
            var tickManualResetEvent = new ManualResetEvent(false);

            _marketDataProvider.LogonArrived += delegate (string providerName)
            {
                logonReceived = true;
                logonManualResetEvent.Set();

                _marketDataProvider.SubscribeTickData(new Subscribe() { Security = new Security() { Symbol = symbol } });
            };

            _marketDataProvider.TickArrived += delegate (Tick tick)
            {
                tickReceived = true;
                Console.WriteLine(tick);
            };

            _marketDataProvider.Start();

            logonManualResetEvent.WaitOne(10000, false);
            tickManualResetEvent.WaitOne(10000, false);

            Assert.AreEqual(true, logonReceived, "Logon Received");
            Assert.AreEqual(true, tickReceived, "Tick Received");
        }

        [Test]
        [Category("Integration")]
        public void BarData_SendRequestToServer_ReceiveBarDataFromServer()
        {
            string symbol = "AAPL";
            bool logonReceived = false;
            bool dataReceived = false;

            var logonManualResetEvent = new ManualResetEvent(false);
            var dataManualResetEvent = new ManualResetEvent(false);

            var dataRequestMessage = new BarDataRequest() { Security = new Security() { Symbol = symbol } };
            dataRequestMessage.Id = "AAOOA";
            dataRequestMessage.BarPriceType = BarPriceType.MEAN;
            dataRequestMessage.BarLength = 60;
            dataRequestMessage.BarFormat = BarFormat.TIME;
            dataRequestMessage.BarSeed = 0;
            dataRequestMessage.PipSize = 1;

            _marketDataProvider.LogonArrived += delegate (string providerName)
            {
                logonReceived = true;
                logonManualResetEvent.Set();

                _marketDataProvider.SubscribeBars(dataRequestMessage);

            };

            _marketDataProvider.BarArrived += delegate (Bar data, string key)
            {
                dataReceived = true;
                dataManualResetEvent.Set();
                Console.WriteLine(key + data);
            };

            _marketDataProvider.Start();

            logonManualResetEvent.WaitOne(10000, false);
            dataManualResetEvent.WaitOne(220000, false);

            Assert.AreEqual(true, logonReceived, "Logon Received");
            Assert.AreEqual(true, dataReceived, "Data Received");
        }

        [Test]
        [Category("Integration")]
        public void HistoricalData_SendRequestToServer_ReceiveHistoricalDataFromServer()
        {
            bool logonReceived = false;
            bool dataReceived = false;

            var logonManualResetEvent = new ManualResetEvent(false);
            var dataManualResetEvent = new ManualResetEvent(false);

            var dataRequestMessage = new HistoricDataRequest() { Security = new Security() { Symbol = "AAPL" } };
            dataRequestMessage.Id = "AAOOA";
            dataRequestMessage.BarType = BarType.INTRADAY;
            dataRequestMessage.Interval = 60;
            dataRequestMessage.StartTime = new DateTime(2015, 2, 1);
            dataRequestMessage.EndTime = new DateTime(2015, 3, 1);

            _marketDataProvider.LogonArrived += delegate (string providerName)
            {
                logonReceived = true;
                logonManualResetEvent.Set();

                _marketDataProvider.HistoricBarDataRequest(dataRequestMessage);

            };

            _marketDataProvider.HistoricBarDataArrived += delegate (HistoricBarData data)
            {
                dataReceived = true;
                dataManualResetEvent.Set();
                Console.WriteLine(data.Security.Symbol);
            };

            _marketDataProvider.Start();

            logonManualResetEvent.WaitOne(10000, false);
            dataManualResetEvent.WaitOne(20000, false);

            Assert.AreEqual(true, logonReceived, "Logon Received");
            Assert.AreEqual(true, dataReceived, "Data Received");
        }
    }
}
