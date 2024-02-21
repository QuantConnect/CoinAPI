/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using NUnit.Framework;
using QuantConnect.Packets;
using QuantConnect.Configuration;

namespace QuantConnect.DataSource.CoinAPI.Tests
{
    [TestFixture]
    public class CoinApiAdditionalTests
    {
        [Test]
        public void ThrowsOnFailedAuthentication()
        {
            Config.Set("coinapi-api-key", "wrong-api-key");

            Assert.Throws<Exception>(() =>
            {
                using var _coinApiDataQueueHandler = new CoinApiDataProvider();
            });

            // reset api key
            TestSetup.GlobalSetup();
        }

        [Test]
        public void CanInitializeUsingJobPacket()
        {
            var apiKey = Config.Get("coinapi-api-key");
            Config.Set("coinapi-api-key", "");

            var job = new LiveNodePacket
            {
                BrokerageData = new Dictionary<string, string>() {
                    { "coinapi-api-key", "InvalidApiKeyThatWontBeUsed" },
                    { "coinapi-product", "Startup" }
                }
            };

            using var iexDataProvider = new CoinApiDataProvider();

            // Throw because CoinApiSymbolMapper makes request to API (we have invalid api key in LiveNodePacket)
            Assert.Throws<Exception>(() =>
            {
                iexDataProvider.SetJob(job);
            });

            // revert Config of ApiKey for another tests
            Config.Set("coinapi-api-key", apiKey);
        }
    }
}
