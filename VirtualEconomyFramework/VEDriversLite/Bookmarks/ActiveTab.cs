﻿using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VEDriversLite.NeblioAPI;
using VEDriversLite.NFT;

namespace VEDriversLite.Bookmarks
{
    public class ActiveTab
    {
        private static object _lock = new object();
        public ActiveTab() { }
        public ActiveTab(string address)
        {
            Address = address;
            ShortAddress = NeblioTransactionHelpers.ShortenAddress(address);
        }

        /// <summary>
        /// Info if the Tab is selected - this is set by Start or Stop functions internally
        /// </summary>
        [JsonIgnore]
        public bool Selected { get; set; } = false;
        /// <summary>
        /// Address loaded in this ActiveTab
        /// </summary>
        public string Address { get; set; } = string.Empty;
        /// <summary>
        /// Shorten version of the address - just help for UI
        /// </summary>
        public string ShortAddress { get; set; } = string.Empty;
        /// <summary>
        /// Flag if the Address is in the bookmark - then it has Bookmark dto loaded
        /// </summary>
        public bool IsInBookmark { get; set; } = false;
        /// <summary>
        /// Tab loads just 40 NFTs. if it can load more, this is true
        /// </summary>
        public bool CanLoadMore { get; set; } = false;
        /// <summary>
        /// Indicate if the autorefresh is running
        /// </summary>
        [JsonIgnore]
        public bool IsRefreshingRunning { get; set; } = false;
        /// <summary>
        /// List of the loaded NFTs of the Address
        /// </summary>
        [JsonIgnore]
        public List<INFT> NFTs { get; set; } = new List<INFT>();
        /// <summary>
        /// List of the loaded Utxos of the Address
        /// </summary>
        [JsonIgnore]
        public ICollection<Utxos> UtxosList { get; set; } = new List<Utxos>();
        /// <summary>
        /// Loaded Bookmark data
        /// </summary>
        [JsonIgnore]
        public Bookmark BookmarkFromAccount { get; set; } = new Bookmark();
        /// <summary>
        /// Loaded list of the received payments NFTs of the address
        /// </summary>
        [JsonIgnore]
        public ConcurrentDictionary<string, INFT> ReceivedPayments = new ConcurrentDictionary<string, INFT>();
        /// <summary>
        /// Profile NFT of the Address
        /// </summary>
        [JsonIgnore]
        public ProfileNFT Profile { get; set; } = new ProfileNFT("");
        /// <summary>
        /// This event is called whenever the list of NFTs is changed
        /// </summary>
        public event EventHandler<string> NFTsChanged;
        /// <summary>
        /// This event is called whenever profile nft is updated or found
        /// </summary>
        public event EventHandler<INFT> ProfileUpdated;
        /// <summary>
        /// This event is called during first loading of the account to keep updated the user
        /// </summary>
        public event EventHandler<string> FirsLoadingStatus;
        /// <summary>
        /// This event is fired whenever some NFT is in received payment too and it should be blocked for any further action.
        /// It provides Utxo and UtxoIndex as touple.
        /// </summary>
        public event EventHandler<(string, int)> NFTAddedToPayments;


        private System.Timers.Timer refreshTimer = new System.Timers.Timer();
        public int MaxLoadedNFTItems { get; set; } = 40;

        /// <summary>
        /// This is same function as in NeblioAcocuntBase - TODO merge them to one common function
        /// This will make loading of the tab much faster
        /// </summary>
        /// <returns></returns>
        public async Task TxCashPreload()
        {
            // cash preload just for the NFT utxos?
            //var nftutxos = await NeblioTransactionHelpers.GetAddressNFTsUtxos(Address, NFTHelpers.AllowedTokens, new GetAddressInfoResponse() { Utxos = Utxos });

            Console.WriteLine("Cash of the TxInfo preload started...");
            
            var utxos = UtxosList.ToArray();
            if (utxos != null && utxos.Length > 1)
            {
                var ucount = utxos.Length;
                if (ucount >= MaxLoadedNFTItems)
                    ucount = MaxLoadedNFTItems;

                var txinfotasks = new Task[ucount * 2];
                var u = 0;
                for (var i = 0; (i + 2) < txinfotasks.Length; i += 2)
                {
                    if (i < txinfotasks.Length)
                        txinfotasks[i] = NeblioTransactionHelpers.GetTransactionInfo(utxos[u].Txid);
                    if (i < txinfotasks.Length + 1)
                    {
                        var tokid = utxos[u].Tokens?.FirstOrDefault()?.TokenId;
                        if (!string.IsNullOrEmpty(tokid))
                        {
                            if (!VEDLDataContext.NFTCache.ContainsKey(utxos[u].Txid))
                                txinfotasks[i + 1] = NeblioTransactionHelpers.GetTokenMetadataOfUtxoCache(tokid, utxos[u].Txid);
                        }
                    }
                    u++;
                }
                for (var t = 0; t < txinfotasks.Length; t++)
                {
                    if (txinfotasks[t] == null) txinfotasks[t] = Task.Delay(1);
                }

                await Task.WhenAll(txinfotasks);
            }
            Console.WriteLine("Cash of the TxInfo preload end...");
        }

        /// <summary>
        /// Start Automated refreshing
        /// </summary>
        /// <param name="interval"></param>
        /// <returns></returns>
        public async Task StartRefreshing(double interval = 5000)
        {
            Selected = true;
            FirsLoadingStatus?.Invoke(this, "Start Loading the data.");
            
            await Reload();
            refreshTimer.Interval = interval;
            refreshTimer.AutoReset = true;
            refreshTimer.Elapsed -= RefreshTimer_Elapsed;
            refreshTimer.Elapsed += RefreshTimer_Elapsed;
            refreshTimer.Enabled = true;
            IsRefreshingRunning = true;
        }

        /// <summary>
        /// Stop automated refreshing
        /// </summary>
        /// <returns></returns>
        public async Task StopRefreshing()
        {
            IsRefreshingRunning = false;
            Selected = false;
            refreshTimer.Stop();
            refreshTimer.Enabled = false;
            refreshTimer.Elapsed -= RefreshTimer_Elapsed;
        }

        private void RefreshTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            refreshTimer.Stop();
            Reload();
        }

        /// <summary>
        /// Reload Address Utxos and refresh the NFT list
        /// </summary>
        /// <returns></returns>
        public async Task Reload()
        {
            try
            {
                UtxosList = await NeblioTransactionHelpers.GetAddressNFTsUtxos(Address, NFTHelpers.AllowedTokens);
                if (NFTs.Count == 0)
                {
                    try
                    {
                        await TxCashPreload();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Cannot finish the preload." + ex.Message);
                    }
                }

                List<INFT> ns;
                lock (_lock)
                {
                    ns = NFTs.ToList();
                }
                NFTHelpers.ProfileNFTFound += NFTHelpers_ProfileNFTFound;
                NFTHelpers.NFTLoadingStateChanged += NFTHelpers_LoadingStateChangedHandler;
                var _NFTs = await NFTHelpers.LoadAddressNFTs(Address, UtxosList, ns, false, MaxLoadedNFTItems, true);
                NFTHelpers.NFTLoadingStateChanged -= NFTHelpers_LoadingStateChangedHandler;
                NFTHelpers.ProfileNFTFound -= NFTHelpers_ProfileNFTFound;
                FirsLoadingStatus?.Invoke(this, "NFTs Loaded.");
                if (_NFTs != null)
                {
                    /*
                    if (_NFTs.Count < UtxosList.Count && MaxLoadedNFTItems < UtxosList.Count)
                    {
                        MaxLoadedNFTItems += 10;
                        CanLoadMore = true;
                    }
                    else
                    {
                        CanLoadMore = false;
                    }*/
                    lock (_lock)
                    {
                        NFTs = new List<INFT>(_NFTs);
                    }
                }

                if (_NFTs.Count > 0)
                    Profile = await NFTHelpers.FindProfileNFT(_NFTs);

                FirsLoadingStatus?.Invoke(this, "Searching for NFT Payments.");
                await RefreshAddressReceivedPayments();

                NFTsChanged?.Invoke(this, Address);
                FirsLoadingStatus?.Invoke(this, "Loading finished.");
                refreshTimer?.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cannot reload the tab content. " + ex.Message);
            }
        }

        private void NFTHelpers_ProfileNFTFound(object sender, INFT e)
        {
            var add = sender as string;
            if (!string.IsNullOrEmpty(add) && add == Address)
            {
                Profile = e as ProfileNFT;
                ProfileUpdated?.Invoke(this, e);
            }
        }

        private void NFTHelpers_LoadingStateChangedHandler(object sender, string e)
        {
            var add = sender as string;
            if (!string.IsNullOrEmpty(add) && add == Address)
            {
                FirsLoadingStatus?.Invoke(this, e);
            }
        }

        /// <summary>
        /// This function will search NFT Payments in the NFTs list and load them into ReceivedPayments list. 
        /// This list is cleared at the start of this function
        /// </summary>
        /// <returns></returns>
        public async Task RefreshAddressReceivedPayments()
        {
            try
            {
                lock (_lock)
                {
                    var firstpnft = ReceivedPayments.Values.FirstOrDefault();
                    var pnfts = NFTs.Where(n => n.Type == NFTTypes.Payment).ToList();
                    var ffirstpnft = pnfts.FirstOrDefault();

                    if ((firstpnft != null && ffirstpnft != null) || firstpnft == null && ffirstpnft != null)
                    {
                        if ((firstpnft == null && ffirstpnft != null) || (firstpnft != null && (firstpnft.Utxo != ffirstpnft.Utxo)))
                        {
                            ReceivedPayments.Clear();
                            foreach (var p in pnfts)
                            {
                                ReceivedPayments.TryAdd(p.NFTOriginTxId, p);
                                if (NFTs.Where(nft => NFTHelpers.IsBuyableNFT(nft.Type))
                                        .FirstOrDefault(n => n.Utxo == (p as PaymentNFT).NFTUtxoTxId && 
                                                             n.UtxoIndex == (p as PaymentNFT).NFTUtxoIndex) != null)
                                {
                                    NFTAddedToPayments?.Invoke(Address, ((p as PaymentNFT).NFTUtxoTxId, (p as PaymentNFT).NFTUtxoIndex));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cannot refresh address received payments. " + ex.Message);
            }
        }

        /// <summary>
        /// Load Bookmark data into ActiveTab class when some bookmark is found in another storage (for example NeblioAccount class)
        /// </summary>
        /// <param name="bkm"></param>
        public void LoadBookmark(Bookmark bkm)
        {
            if (!string.IsNullOrEmpty(bkm.Address) && !string.IsNullOrEmpty(bkm.Name))
            {
                IsInBookmark = true;
                BookmarkFromAccount = bkm;
            }
            else
                ClearBookmark();
        }
        /// <summary>
        /// Clear the Bookmark data in ActiveTab
        /// </summary>
        public void ClearBookmark()
        {
            IsInBookmark = false;
            BookmarkFromAccount = new Bookmark();
        }
    }
}
