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

using RestSharp;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using QuantConnect.Api;
using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Logging;
using Newtonsoft.Json.Linq;
using CoinAPI.WebSocket.V1;
using QuantConnect.Interfaces;
using QuantConnect.Data.Market;
using QuantConnect.Configuration;
using System.Security.Cryptography;
using System.Net.NetworkInformation;
using System.Collections.Concurrent;
using CoinAPI.WebSocket.V1.DataModels;
using QuantConnect.Lean.Engine.HistoricalData;

namespace QuantConnect.Lean.DataSource.CoinAPI
{
    /// <summary>
    /// An implementation of <see cref="IDataQueueHandler"/> for CoinAPI
    /// </summary>
    public partial class CoinApiDataProvider : SynchronizingHistoryProvider, IDataQueueHandler
    {
        protected int HistoricalDataPerRequestLimit = 10000;
        private static readonly Dictionary<Resolution, string> _ResolutionToCoinApiPeriodMappings = new Dictionary<Resolution, string>
        {
            { Resolution.Second, "1SEC"},
            { Resolution.Minute, "1MIN" },
            { Resolution.Hour, "1HRS" },
            { Resolution.Daily, "1DAY" },
        };

        private string? _apiKey;
        private string[]? _streamingDataType;
        private CoinApiWsClient? _client;
        private readonly object _locker = new object();
        private ConcurrentDictionary<string, Symbol> _symbolCache = new ConcurrentDictionary<string, Symbol>();
        private CoinApiSymbolMapper? _symbolMapper;
        private IDataAggregator? _dataAggregator;
        private EventBasedDataQueueHandlerSubscriptionManager? _subscriptionManager;

        private readonly TimeSpan _subscribeDelay = TimeSpan.FromMilliseconds(250);
        private readonly object _lockerSubscriptions = new object();
        private DateTime _lastSubscribeRequestUtcTime = DateTime.MinValue;
        private bool _subscriptionsPending;

        private readonly TimeSpan _minimumTimeBetweenHelloMessages = TimeSpan.FromSeconds(5);
        private DateTime _nextHelloMessageUtcTime = DateTime.MinValue;

        private readonly ConcurrentDictionary<string, Tick> _previousQuotes = new ConcurrentDictionary<string, Tick>();

        /// <summary>
        /// 
        /// </summary>
        private bool _initialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoinApiDataProvider"/> class
        /// </summary>
        public CoinApiDataProvider()
        {
            if (!Config.TryGetValue<string>("coinapi-api-key", out var configApiKey) || string.IsNullOrEmpty(configApiKey))
            {
                // If the API key is not provided, we can't do anything.
                // The handler might going to be initialized using a node packet job.
                return;
            }

            if (!Config.TryGetValue<string>("coinapi-product", out var product) || string.IsNullOrEmpty(product))
            {
                product = "Free";
            }

            Initialize(configApiKey, product);
        }

        /// <summary>
        /// Subscribe to the specified configuration
        /// </summary>
        /// <param name="dataConfig">defines the parameters to subscribe to a data feed</param>
        /// <param name="newDataAvailableHandler">handler to be fired on new data available</param>
        /// <returns>The new enumerator for this subscription request</returns>
        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (!CanSubscribe(dataConfig.Symbol))
            {
                return null;
            }

            if (_dataAggregator == null || _subscriptionManager == null)
            {
                throw new InvalidOperationException($"{nameof(CoinApiDataProvider)}.{nameof(Subscribe)}: {nameof(_dataAggregator)} or {nameof(_subscriptionManager)} is not initialized.");
            }

            var enumerator = _dataAggregator.Add(dataConfig, newDataAvailableHandler);
            _subscriptionManager.Subscribe(dataConfig);

            return enumerator;
        }

        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        /// <param name="job">Job we're subscribing for</param>
        public void SetJob(LiveNodePacket job)
        {
            if (_initialized)
            {
                return;
            }

            if (!job.BrokerageData.TryGetValue("coinapi-api-key", out var apiKey) || string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("Invalid or missing Coin API key. Please ensure that the API key is set and not empty.");
            }

            if (!job.BrokerageData.TryGetValue("coinapi-product", out var product) || string.IsNullOrEmpty(product))
            {
                product = "Free";
            }

            Initialize(apiKey, product);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="product"></param>
        /// <exception cref="ArgumentException"></exception>
        private void Initialize(string apiKey, string product)
        {
            ValidateSubscription();

            _apiKey = apiKey;

            if (!Enum.TryParse<CoinApiProduct>(product, true, out var parsedProduct) || !Enum.IsDefined(typeof(CoinApiProduct), parsedProduct))
            {
                throw new ArgumentException($"An error occurred while parsing the price plan '{product}'. Please ensure that the provided price plan is valid and supported by the system.");
            }

            _dataAggregator = Composer.Instance.GetPart<IDataAggregator>();
            if (_dataAggregator == null)
            {
                _dataAggregator =
                    Composer.Instance.GetExportedValueByTypeName<IDataAggregator>(Config.Get("data-aggregator", "QuantConnect.Lean.Engine.DataFeeds.AggregationManager"), forceTypeNameOnExisting: false);
            }

            _streamingDataType = parsedProduct < CoinApiProduct.Streamer
                ? new[] { "trade" }
                : new[] { "trade", "quote" };

            Log.Trace($"{nameof(CoinApiDataProvider)}: using plan '{product}'. Available data types: '{string.Join(",", _streamingDataType)}'");

            _symbolMapper = new CoinApiSymbolMapper();
            _client = new CoinApiWsClient();
            _client.TradeEvent += OnTrade;
            _client.QuoteEvent += OnQuote;
            _client.Error += OnError;
            _subscriptionManager = new EventBasedDataQueueHandlerSubscriptionManager();
            _subscriptionManager.SubscribeImpl += (s, t) => Subscribe(s);
            _subscriptionManager.UnsubscribeImpl += (s, t) => Unsubscribe(s);
            _initialized = true;
        }

        /// <summary>
        /// Adds the specified symbols to the subscription
        /// </summary>
        /// <param name="symbols">The symbols to be added keyed by SecurityType</param>
        private bool Subscribe(IEnumerable<Symbol> symbols)
        {
            ProcessSubscriptionRequest();
            return true;
        }

        /// <summary>
        /// Removes the specified configuration
        /// </summary>
        /// <param name="dataConfig">Subscription config to be removed</param>
        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            _subscriptionManager?.Unsubscribe(dataConfig);
            _dataAggregator?.Remove(dataConfig);
        }


        /// <summary>
        /// Removes the specified symbols to the subscription
        /// </summary>
        /// <param name="symbols">The symbols to be removed keyed by SecurityType</param>
        private bool Unsubscribe(IEnumerable<Symbol> symbols)
        {
            ProcessSubscriptionRequest();
            return true;
        }

        /// <summary>
        /// Returns whether the data provider is connected
        /// </summary>
        /// <returns>true if the data provider is connected</returns>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_client != null)
            {
                _client.TradeEvent -= OnTrade;
                _client.QuoteEvent -= OnQuote;
                _client.Error -= OnError;
                _client.Dispose();
            }
            _dataAggregator.DisposeSafely();
        }

        /// <summary>
        /// Helper method used in QC backend
        /// </summary>
        /// <param name="markets">List of LEAN markets (exchanges) to subscribe</param>
        public void SubscribeMarkets(List<string> markets)
        {
            Log.Trace($"CoinApiDataProvider.SubscribeMarkets(): {string.Join(",", markets)}");

            // we add '_' to be more precise, for example requesting 'BINANCE' doesn't match 'BINANCEUS'
            SendHelloMessage(markets.Select(x => string.Concat(_symbolMapper.GetExchangeId(x.ToLowerInvariant()), "_")));
        }

        private void ProcessSubscriptionRequest()
        {
            if (_subscriptionsPending) return;

            _lastSubscribeRequestUtcTime = DateTime.UtcNow;
            _subscriptionsPending = true;

            Task.Run(async () =>
            {
                while (true)
                {
                    DateTime requestTime;
                    List<Symbol> symbolsToSubscribe;
                    lock (_lockerSubscriptions)
                    {
                        requestTime = _lastSubscribeRequestUtcTime.Add(_subscribeDelay);

                        // CoinAPI requires at least 5 seconds between hello messages
                        if (_nextHelloMessageUtcTime != DateTime.MinValue && requestTime < _nextHelloMessageUtcTime)
                        {
                            requestTime = _nextHelloMessageUtcTime;
                        }

                        symbolsToSubscribe = _subscriptionManager.GetSubscribedSymbols().ToList();
                    }

                    var timeToWait = requestTime - DateTime.UtcNow;

                    int delayMilliseconds;
                    if (timeToWait <= TimeSpan.Zero)
                    {
                        // minimum delay has passed since last subscribe request, send the Hello message
                        SubscribeSymbols(symbolsToSubscribe);

                        lock (_lockerSubscriptions)
                        {
                            _lastSubscribeRequestUtcTime = DateTime.UtcNow;
                            if (_subscriptionManager.GetSubscribedSymbols().Count() == symbolsToSubscribe.Count)
                            {
                                // no more subscriptions pending, task finished
                                _subscriptionsPending = false;
                                break;
                            }
                        }

                        delayMilliseconds = _subscribeDelay.Milliseconds;
                    }
                    else
                    {
                        delayMilliseconds = timeToWait.Milliseconds;
                    }

                    await Task.Delay(delayMilliseconds).ConfigureAwait(false);
                }
            });
        }

        /// <summary>
        /// Returns true if we can subscribe to the specified symbol
        /// </summary>
        private static bool CanSubscribe(Symbol symbol)
        {
            // ignore unsupported security types
            if (symbol.ID.SecurityType != SecurityType.Crypto && symbol.ID.SecurityType != SecurityType.CryptoFuture)
            {
                return false;
            }

            // ignore universe symbols
            return !symbol.Value.Contains("-UNIVERSE-");
        }

        /// <summary>
        /// Subscribes to a list of symbols
        /// </summary>
        /// <param name="symbolsToSubscribe">The list of symbols to subscribe</param>
        private void SubscribeSymbols(List<Symbol> symbolsToSubscribe)
        {
            Log.Trace($"CoinApiDataProvider.SubscribeSymbols(): {string.Join(",", symbolsToSubscribe)}");

            // subscribe to symbols using exact match
            SendHelloMessage(symbolsToSubscribe.Select(x =>
            {
                try
                {
                    var result = string.Concat(_symbolMapper.GetBrokerageSymbol(x), "$");
                    return result;
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    return null;
                }
            }).Where(x => x != null));
        }

        private void SendHelloMessage(IEnumerable<string> subscribeFilter)
        {
            var list = subscribeFilter.ToList();
            if (list.Count == 0)
            {
                // If we use a null or empty filter in the CoinAPI hello message
                // we will be subscribing to all symbols for all active exchanges!
                // Only option is requesting an invalid symbol as filter.
                list.Add("$no_symbol_requested$");
            }

            _client?.SendHelloMessage(new Hello
            {
                apikey = Guid.Parse(_apiKey),
                heartbeat = true,
                subscribe_data_type = _streamingDataType,
                subscribe_filter_symbol_id = list.ToArray()
            });

            if (!IsConnected && !_client.ConnectedEvent.WaitOne(TimeSpan.FromSeconds(30)))
            {
                throw new Exception("Not connected...");
            }

            IsConnected = true;

            _nextHelloMessageUtcTime = DateTime.UtcNow.Add(_minimumTimeBetweenHelloMessages);
        }

        private void OnTrade(object sender, Trade trade)
        {
            try
            {
                var symbol = GetSymbolUsingCache(trade.symbol_id);
                if (symbol == null)
                {
                    return;
                }

                var tick = new Tick(trade.time_exchange, symbol, string.Empty, string.Empty, quantity: trade.size, price: trade.price);

                lock (symbol)
                {
                    _dataAggregator.Update(tick);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        private void OnQuote(object sender, Quote quote)
        {
            try
            {
                // only emit quote ticks if bid price or ask price changed
                Tick previousQuote;
                if (!_previousQuotes.TryGetValue(quote.symbol_id, out previousQuote)
                    || quote.ask_price != previousQuote.AskPrice
                    || quote.bid_price != previousQuote.BidPrice)
                {
                    var symbol = GetSymbolUsingCache(quote.symbol_id);
                    if (symbol == null)
                    {
                        return;
                    }

                    var tick = new Tick(quote.time_exchange, symbol, string.Empty, string.Empty,
                        bidSize: quote.bid_size, bidPrice: quote.bid_price,
                        askSize: quote.ask_size, askPrice: quote.ask_price);

                    _previousQuotes[quote.symbol_id] = tick;
                    lock (symbol)
                    {
                        _dataAggregator.Update(tick);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        private Symbol GetSymbolUsingCache(string ticker)
        {
            if (!_symbolCache.TryGetValue(ticker, out Symbol result))
            {
                try
                {
                    var securityType = ticker.IndexOf("_PERP_") > 0 ? SecurityType.CryptoFuture : SecurityType.Crypto;
                    result = _symbolMapper.GetLeanSymbol(ticker, securityType, string.Empty);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    // we store the null so we don't keep going into the same mapping error
                    result = null;
                }
                _symbolCache[ticker] = result;
            }
            return result;
        }

        private void OnError(object? sender, Exception e)
        {
            IsConnected = false;
            Log.Error(e);
        }

        private class ModulesReadLicenseRead : Api.RestResponse
        {
            [JsonProperty(PropertyName = "license")]
            public string License;

            [JsonProperty(PropertyName = "organizationId")]
            public string OrganizationId;
        }

        /// <summary>
        /// Validate the user of this project has permission to be using it via our web API.
        /// </summary>
        private static void ValidateSubscription()
        {
            try
            {
                const int productId = 335;
                var userId = Globals.UserId;
                var token = Globals.UserToken;
                var organizationId = Globals.OrganizationID;
                // Verify we can authenticate with this user and token
                var api = new ApiConnection(userId, token);
                if (!api.Connected)
                {
                    throw new ArgumentException("Invalid api user id or token, cannot authenticate subscription.");
                }
                // Compile the information we want to send when validating
                var information = new Dictionary<string, object>()
                {
                    {"productId", productId},
                    {"machineName", Environment.MachineName},
                    {"userName", Environment.UserName},
                    {"domainName", Environment.UserDomainName},
                    {"os", Environment.OSVersion}
                };
                // IP and Mac Address Information
                try
                {
                    var interfaceDictionary = new List<Dictionary<string, object>>();
                    foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(nic => nic.OperationalStatus == OperationalStatus.Up))
                    {
                        var interfaceInformation = new Dictionary<string, object>();
                        // Get UnicastAddresses
                        var addresses = nic.GetIPProperties().UnicastAddresses
                            .Select(uniAddress => uniAddress.Address)
                            .Where(address => !IPAddress.IsLoopback(address)).Select(x => x.ToString());
                        // If this interface has non-loopback addresses, we will include it
                        if (!addresses.IsNullOrEmpty())
                        {
                            interfaceInformation.Add("unicastAddresses", addresses);
                            // Get MAC address
                            interfaceInformation.Add("MAC", nic.GetPhysicalAddress().ToString());
                            // Add Interface name
                            interfaceInformation.Add("name", nic.Name);
                            // Add these to our dictionary
                            interfaceDictionary.Add(interfaceInformation);
                        }
                    }
                    information.Add("networkInterfaces", interfaceDictionary);
                }
                catch (Exception)
                {
                    // NOP, not necessary to crash if fails to extract and add this information
                }
                // Include our OrganizationId if specified
                if (!string.IsNullOrEmpty(organizationId))
                {
                    information.Add("organizationId", organizationId);
                }
                var request = new RestRequest("modules/license/read", Method.POST) { RequestFormat = DataFormat.Json };
                request.AddParameter("application/json", JsonConvert.SerializeObject(information), ParameterType.RequestBody);
                api.TryRequest(request, out ModulesReadLicenseRead result);
                if (!result.Success)
                {
                    throw new InvalidOperationException($"Request for subscriptions from web failed, Response Errors : {string.Join(',', result.Errors)}");
                }

                var encryptedData = result.License;
                // Decrypt the data we received
                DateTime? expirationDate = null;
                long? stamp = null;
                bool? isValid = null;
                if (encryptedData != null)
                {
                    // Fetch the org id from the response if it was not set, we need it to generate our validation key
                    if (string.IsNullOrEmpty(organizationId))
                    {
                        organizationId = result.OrganizationId;
                    }
                    // Create our combination key
                    var password = $"{token}-{organizationId}";
                    var key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                    // Split the data
                    var info = encryptedData.Split("::");
                    var buffer = Convert.FromBase64String(info[0]);
                    var iv = Convert.FromBase64String(info[1]);
                    // Decrypt our information
                    using var aes = new AesManaged();
                    var decryptor = aes.CreateDecryptor(key, iv);
                    using var memoryStream = new MemoryStream(buffer);
                    using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
                    using var streamReader = new StreamReader(cryptoStream);
                    var decryptedData = streamReader.ReadToEnd();
                    if (!decryptedData.IsNullOrEmpty())
                    {
                        var jsonInfo = JsonConvert.DeserializeObject<JObject>(decryptedData);
                        expirationDate = jsonInfo["expiration"]?.Value<DateTime>();
                        isValid = jsonInfo["isValid"]?.Value<bool>();
                        stamp = jsonInfo["stamped"]?.Value<int>();
                    }
                }
                // Validate our conditions
                if (!expirationDate.HasValue || !isValid.HasValue || !stamp.HasValue)
                {
                    throw new InvalidOperationException("Failed to validate subscription.");
                }

                var nowUtc = DateTime.UtcNow;
                var timeSpan = nowUtc - Time.UnixTimeStampToDateTime(stamp.Value);
                if (timeSpan > TimeSpan.FromHours(12))
                {
                    throw new InvalidOperationException("Invalid API response.");
                }
                if (!isValid.Value)
                {
                    throw new ArgumentException($"Your subscription is not valid, please check your product subscriptions on our website.");
                }
                if (expirationDate < nowUtc)
                {
                    throw new ArgumentException($"Your subscription expired {expirationDate}, please renew in order to use this product.");
                }
            }
            catch (Exception e)
            {
                Log.Error($"{nameof(CoinApiDataProvider)}.{nameof(ValidateSubscription)}: Failed during validation, shutting down. Error : {e.Message}");
                throw;
            }
        }
    }
}
