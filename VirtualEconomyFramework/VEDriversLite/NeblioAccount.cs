﻿using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VEDriversLite.Bookmarks;
using VEDriversLite.Events;
using VEDriversLite.NeblioAPI;
using VEDriversLite.NFT;
using VEDriversLite.Security;

namespace VEDriversLite
{
    public class NeblioAccount
    {
        public NeblioAccount()
        {
            Profile = new ProfileNFT("");
            NFTs = new List<INFT>();
            Bookmarks = new List<Bookmark>();
            InitHandlers();
        }
        public Guid Id { get; set; }
        public string Address { get; set; } = string.Empty;
        public double NumberOfTransaction { get; set; } = 0;
        public double NumberOfLoadedTransaction { get; } = 0;
        public bool EnoughBalanceToBuySourceTokens { get; set; } = false;
        public double TotalBalance { get; set; } = 0.0;
        public double TotalSpendableBalance { get; set; } = 0.0;
        public double TotalUnconfirmedBalance { get; set; } = 0.0;
        public double SourceTokensBalance { get; set; } = 0.0;
        public double AddressNFTCount { get; set; } = 0.0;
        public List<INFT> NFTs { get; set; } = new List<INFT>();
        public ConcurrentDictionary<string, INFT> ReceivedPayments = new ConcurrentDictionary<string, INFT>();
        public ProfileNFT Profile { get; set; } = new ProfileNFT("");
        public Dictionary<string, TokenSupplyDto> TokensSupplies { get; set; } = new Dictionary<string, TokenSupplyDto>();
        public List<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
        public GetAddressResponse AddressInfo { get; set; } = new GetAddressResponse();
        public GetAddressInfoResponse AddressInfoUtxos { get; set; } = new GetAddressInfoResponse();

        public event EventHandler Refreshed;
        public event EventHandler<IEventInfo> NewEventInfo;
        public event EventHandler<string> PaymentSent;
        public event EventHandler<string> PaymentSentError;

        [JsonIgnore]
        public EncryptionKey AccountKey { get; set; }

        public bool IsLocked()
        {
            if (AccountKey != null)
            {
                if (AccountKey.IsEncrypted)
                {
                    if (AccountKey.IsPassLoaded)
                        return false;
                    else
                        return true;
                }
                else
                {
                    if (AccountKey.IsLoaded)
                        return false;
                    else
                        return true;
                }
            }
            else
            {
                return true;
            }
        }

        private void InitHandlers()
        {
            NeblioTransactionHelpers.NewEventInfo += NeblioTransactionHelpers_NewEventInfo;
        }
        private void DeInitHandlers()
        {
            NeblioTransactionHelpers.NewEventInfo -= NeblioTransactionHelpers_NewEventInfo;
        }

        private void NeblioTransactionHelpers_NewEventInfo(object sender, IEventInfo e)
        {
            e.Address = Address;
            EventInfoProvider.StoreEventInfo(e);
        }

        private async Task InvokeSendPaymentSuccessEvent(string txid, string title = "Neblio Payment Sent")
        {
            NewEventInfo?.Invoke(this,
                        await EventFactory.GetEvent(EventType.Info,
                                                    title,
                                                    $"Successfull send. Please wait a while for enough confirmations.",
                                                    Address,
                                                    txid,
                                                    100));
        }

        private async Task InvokeAccountLockedEvent(string title = "Cannot send transaction")
        {
            NewEventInfo?.Invoke(this,
                        await EventFactory.GetEvent(EventType.Error,
                                                    title,
                                                    "Account is Locked. Please unlock it in account page.",
                                                    Address,
                                                    string.Empty,
                                                    100));
        }
        private async Task InvokeErrorDuringSendEvent(string errorMessage, string title = "Cannot send transaction")
        {
            NewEventInfo?.Invoke(this,
                        await EventFactory.GetEvent(EventType.Error,
                                                    title,
                                                    errorMessage,
                                                    Address,
                                                    string.Empty,
                                                    100));
        }
        private async Task InvokeErrorEvent(string errorMessage, string title = "Error")
        {
            NewEventInfo?.Invoke(this,
                        await EventFactory.GetEvent(EventType.Error,
                                                    title,
                                                    errorMessage,
                                                    Address,
                                                    string.Empty,
                                                    100));
        }

        public async Task<string> StartRefreshingData(int interval = 3000)
        {
            try
            {
                await ReloadAccountInfo();
                await ReloadMintingSupply();
                await ReloadCountOfNFTs();
                await ReloadTokenSupply();
            }
            catch (Exception ex)
            {
                // todo
            }

            var minorRefresh = 5;

            // todo cancelation token
            _ = Task.Run(async () =>
            {
                try
                {
                    await ReLoadNFTs();
                    Profile = await NFTHelpers.FindProfileNFT(NFTs);
                    await CheckPayments();
                    await RefreshAddressReceivedPayments();
                    
                }
                catch(Exception ex)
                {
                    // todo
                }
                var lastNFTcount = AddressNFTCount;
                while (true)
                {
                    try
                    {
                        await ReloadAccountInfo();
                        await ReloadMintingSupply();
                        await ReloadCountOfNFTs();
                        await ReloadTokenSupply();

                        if (lastNFTcount != AddressNFTCount)
                            await ReLoadNFTs();

                        minorRefresh--;
                        if (minorRefresh < 0)
                        {
                            Profile = await NFTHelpers.FindProfileNFT(NFTs);
                            await CheckPayments();
                            await RefreshAddressReceivedPayments();
                            minorRefresh = 10;
                        }

                        lastNFTcount = AddressNFTCount;

                        Refreshed?.Invoke(this, null);
                    }
                    catch (Exception ex)
                    {
                        await InvokeErrorEvent(ex.Message, "Unknown Error During Refreshing Data");
                    }

                    await Task.Delay(interval);
                }

            });

            return await Task.FromResult("RUNNING");
        }

        public async Task<bool> CreateNewAccount(string password, bool saveToFile = false)
        {
            try
            {
               await Task.Run(async () =>
               {
                   var network = NBitcoin.Altcoins.Neblio.Instance.Mainnet;
                   Key privateKey = new Key(); // generate a random private key
                    PubKey publicKey = privateKey.PubKey;
                   BitcoinSecret privateKeyFromNetwork = privateKey.GetBitcoinSecret(network);
                   var address = publicKey.GetAddress(ScriptPubKeyType.Legacy, network);
                   Address = address.ToString();

                    // todo load already encrypted key
                   AccountKey = new Security.EncryptionKey(privateKeyFromNetwork.ToString(), password);
                   AccountKey.PublicKey = Address;

                   if (!string.IsNullOrEmpty(password))
                       AccountKey.PasswordHash = await Security.SecurityUtil.HashPassword(password);

                   if (saveToFile)
                   {
                        // save to file
                        var kdto = new KeyDto()
                       {
                           Address = Address,
                           Key = await AccountKey.GetEncryptedKey(returnEncrypted: true)
                       };

                       FileHelpers.WriteTextToFile("key.txt", JsonConvert.SerializeObject(kdto));
                   }
               });

                await StartRefreshingData();

                return true;
            }
            catch (Exception ex)
            {
                await InvokeErrorEvent(ex.Message, "Cannot Create Account");
            }

            return false;
        }

        public async Task<bool> LoadAccount(string password)
        {
            if (FileHelpers.IsFileExists("key.txt"))
            {
                try
                {
                    var k = FileHelpers.ReadTextFromFile("key.txt");
                    var kdto = JsonConvert.DeserializeObject<KeyDto>(k);

                    AccountKey = new EncryptionKey(kdto.Key, fromDb: true);
                    await AccountKey.LoadPassword(password);
                    AccountKey.IsEncrypted = true;

                    Address = kdto.Address;

                    await StartRefreshingData();
                }
                catch(Exception ex)
                {
                    throw new Exception("Cannot deserialize key from file. Please check file key.txt or delete it for create new address!");
                }
            }
            else
            {
                CreateNewAccount(password);
            }

            return false;
        }

        public async Task<bool> LoadAccount(string password, string encryptedKey, string address)
        {
            try
            {
                await Task.Run(async () =>
                {
                    AccountKey = new EncryptionKey(encryptedKey, fromDb: true);
                    await AccountKey.LoadPassword(password);
                    AccountKey.IsEncrypted = true;

                    Address = address;
                });

                await StartRefreshingData();

            }
            catch (Exception ex)
            {
                await InvokeErrorEvent(ex.Message, "Cannot Load Account");
                //throw new Exception("Cannot deserialize key from file. Please check file key.txt or delete it for create new address!");
            }

            return false;
        }

        public async Task LoadBookmarks(string bookmarks)
        {
            try
            {
                var bkm = JsonConvert.DeserializeObject<List<Bookmark>>(bookmarks);
                if (bkm != null)
                    Bookmarks = bkm;
            }
            catch(Exception ex)
            {
                await InvokeErrorEvent(ex.Message, "Cannot deserialize the bookmarks.");
            }
        }

        public async Task<(bool,string)> AddBookmark(string name, string address, string note)
        {
            if (!Bookmarks.Any(b => b.Address == address))
                Bookmarks.Add(new Bookmark()
                {
                    Name = name,
                    Address = address,
                    Note = note
                });
            else
            {
                await InvokeErrorEvent("Bookmark Already Exists", "Already Exists");
                return (false, "Already Exists.");
            }

            return (true,JsonConvert.SerializeObject(Bookmarks));
        }

        public async Task<(bool,string)> RemoveBookmark(string address)
        {
            var bk = Bookmarks.FirstOrDefault(b => b.Address == address);
            if (bk != null)
                Bookmarks.Remove(bk);
            else
            {
                await InvokeErrorEvent("Bookmark Not Found.", "Not Found");
                return (false, "Not Found.");
            }

            return (true,JsonConvert.SerializeObject(Bookmarks));
        }

        public async Task<string> SerializeBookmarks()
        {
            return JsonConvert.SerializeObject(Bookmarks);
        }

        public async Task ReloadTokenSupply()
        {
            TokensSupplies = await NeblioTransactionHelpers.CheckTokensSupplies(Address, AddressInfoUtxos);
        }

        public async Task ReloadCountOfNFTs()
        {
            var nftsu = await NeblioTransactionHelpers.GetAddressNFTsUtxos(Address, AddressInfoUtxos);
            if (nftsu != null)
                AddressNFTCount = nftsu.Count;
        }

        public async Task ReloadMintingSupply()
        {
            var mintingSupply = await NeblioTransactionHelpers.GetActualMintingSupply(Address, AddressInfoUtxos);
            SourceTokensBalance = mintingSupply.Item1;

        }

        public async Task ReloadAccountInfo()
        {
            AddressInfo = await NeblioTransactionHelpers.AddressInfoAsync(Address);
            AddressInfoUtxos = await NeblioTransactionHelpers.AddressInfoUtxosAsync(Address);

            if (AddressInfo != null)
            {
                TotalBalance = (double)AddressInfo.Balance;
                TotalUnconfirmedBalance = (double)AddressInfo.UnconfirmedBalance;
                AddressInfo.Transactions = AddressInfo.Transactions.Reverse().ToList();
            }
            else
            {
                AddressInfo = new GetAddressResponse();
            }

            if (TotalBalance > 1)
                EnoughBalanceToBuySourceTokens = true;
        }

        public async Task ReLoadNFTs()
        {
            if (!string.IsNullOrEmpty(Address))
            {
                NFTs = await NFTHelpers.LoadAddressNFTs(Address);
            }
        }

        public async Task RefreshAddressReceivedPayments()
        {
            ReceivedPayments.Clear();
            var pnfts = NFTs.Where(n => n.Type == NFTTypes.Payment).ToList();
            if (pnfts.Count > 0)
            {
                foreach (var p in pnfts)
                {
                    ReceivedPayments.TryAdd(p.NFTOriginTxId, p);
                }
            }
        }

        public async Task CheckPayments()
        {
            var pnfts = NFTs.Where(n => n.Type == NFTTypes.Payment).ToList();
            if (pnfts.Count > 0)
            {
                foreach (var p in pnfts)
                {
                    var pn = NFTs.Where(n => n.Utxo == ((PaymentNFT)p).NFTUtxoTxId).FirstOrDefault();
                    if (pn != null)
                    {
                        if (pn.Price > 0)
                        {
                            if (p.Price >= pn.Price)
                            {
                                try
                                {
                                    var res = await CheckSpendableNeblio(0.001);
                                    if (res.Item2 != null)
                                    {
                                        var rtxid = await NFTHelpers.SendOrderedNFT(Address, AccountKey, (PaymentNFT)p, pn, res.Item2);
                                        Console.WriteLine(rtxid);
                                        await Task.Delay(500);
                                        await ReLoadNFTs();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    //await InvokeErrorDuringSendEvent($"Cannot send ordered NFT. Payment TxId: {p.Utxo}, NFT TxId: {pn.Utxo}, error message: {ex.Message}", "Cannot send ordered NFT");
                                    Console.WriteLine("Cannot send ordered NFT, payment txid: " + p.Utxo + " - " + ex.Message);
                                }
                            }
                        }
                    }
                }
            }
        }

        public async Task<(bool,double)> HasSomeSpendableNeblio(double amount = 0.0002)
        {
            var nutxos = await NeblioTransactionHelpers.GetAddressNeblUtxo(Address, 0.0001, amount);
            if (nutxos.Count == 0)
            {
                return (false, 0.0);
            }
            else
            {
                var a = 0.0;
                foreach (var u in nutxos)
                    a += ((double)u.Value / NeblioTransactionHelpers.FromSatToMainRatio);

                if (a > amount)
                    return (true, a);
                else
                    return (false, a);
            }
        }

        public async Task<(bool, int)> HasSomeSourceForMinting()
        {
            var tutxos = await NeblioTransactionHelpers.FindUtxoForMintNFT(Address, NFTHelpers.TokenId, 1);


            if (tutxos.Count == 0)
            {
                return (false, 0);
            }
            else
            {
                var a = 0;
                foreach (var u in tutxos)
                {
                    var t = u.Tokens.ToArray()[0];
                    a += (int)t.Amount;
                }
                return (true, a);
            }
        }

        public async Task<(bool, string)> ValidateNFTUtxo(string utxo)
        {
            var u = await NeblioTransactionHelpers.ValidateOneTokenNFTUtxo(Address, NFTHelpers.TokenId, utxo);
            if (!u.Item1)
            {
                var msg = $"Provided source tx transaction is not spendable. Probably waiting for more than {NeblioTransactionHelpers.MinimumConfirmations} confirmation.";
                return (false, msg);
            }
            else
            {
                return (true, "OK");
            }
        }

        public async Task<(string, ICollection<Utxos>)> CheckSpendableNeblio(double amount)
        {
            try
            {
                var nutxos = await NeblioTransactionHelpers.GetAddressNeblUtxo(Address, 0.0002, amount);
                if (nutxos == null || nutxos.Count == 0)
                    return ($"You dont have Neblio on the address. Probably waiting for more than {NeblioTransactionHelpers.MinimumConfirmations} confirmations.", null);
                else
                    return ("OK", nutxos);
            }
            catch(Exception ex)
            {
                return ("Cannot check spendable Neblio. " + ex.Message, null);
            }
        }

        public async Task<(string, ICollection<Utxos>)> CheckSpendableNeblioTokens(string id, int amount)
        {
            try
            {
                var tutxos = await NeblioTransactionHelpers.FindUtxoForMintNFT(Address, id, amount);
                if (tutxos == null || tutxos.Count == 0)
                    return ($"You dont have Tokens on the address. You need at least 5 for minting. Probably waiting for more than {NeblioTransactionHelpers.MinimumConfirmations} confirmations.", null);
                else
                    return ("OK", tutxos);
            }
            catch (Exception ex)
            {
                return ("Cannot check spendable Neblio Tokens. " + ex.Message, null);
            }
        }

        public async Task<(bool, string)> OrderSourceTokens(double amount)
        {
            return await SendNeblioPayment("NRJs13ULX5RPqCDfEofpwxGptg5ePB8Ypw", amount);
        }

        public async Task<(bool, string)> SendNeblioPayment(string receiver, double amount)
        {
            if (IsLocked())
            {
                await InvokeAccountLockedEvent();
                return (false, "Account is locked.");
            }
            var res = await CheckSpendableNeblio(amount);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable inputs");
                return (false, res.Item1);
            }            

            // fill input data for sending tx
            var dto = new SendTxData() // please check SendTokenTxData for another properties such as specify source UTXOs
            {
                Amount = amount,
                SenderAddress = Address,
                ReceiverAddress = receiver
            };

            try
            {
                // send tx
                var rtxid = await NeblioTransactionHelpers.SendNeblioTransactionAPIAsync(dto, AccountKey, res.Item2);
                if (rtxid != null)
                {
                    await InvokeSendPaymentSuccessEvent(rtxid, "Neblio Payment Sent");
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }

            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");
        }

        public async Task<(bool, string)> SendNeblioTokenPayment(string tokenId, IDictionary<string,string> metadata, string receiver, int amount)
        {
            if (IsLocked())
            {
                await InvokeAccountLockedEvent();
                return (false, "Account is locked.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable Neblio inputs");
                return (false, res.Item1);
            }
            var tres = await CheckSpendableNeblioTokens(tokenId, amount);
            if (tres.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(tres.Item1, "Not enought spendable token inputs");
                return (false, tres.Item1);
            }

            // fill input data for sending tx
            var dto = new SendTokenTxData() // please check SendTokenTxData for another properties such as specify source UTXOs
            {
                Amount = Convert.ToDouble(amount),
                SenderAddress = Address,
                ReceiverAddress = receiver,
                Metadata = metadata,
                Id = tokenId
            };

            try
            {
                // send tx
                var rtxid = await NeblioTransactionHelpers.SendTokenLotAsync(dto, AccountKey, res.Item2, tres.Item2);
                if (rtxid != null)
                {
                    await InvokeSendPaymentSuccessEvent(rtxid, "Neblio Token Payment Sent");
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }

            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");
        }

        public async Task<(bool, string)> MintNFT(string tokenId, INFT NFT)
        {
            var nft = await NFTFactory.CloneNFT(NFT);

            if (IsLocked())
            {
                await InvokeAccountLockedEvent();
                return (false, "Account is locked.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable Neblio inputs");
                return (false, res.Item1);
            }
            var tres = await CheckSpendableNeblioTokens(tokenId, 2);
            if (tres.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(tres.Item1, "Not enought spendable Token inputs");
                return (false, tres.Item1);
            }

            try
            {
                var rtxid = string.Empty;
                switch (NFT.Type)
                {
                    case NFTTypes.Image:
                        rtxid = await NFTHelpers.MintImageNFT(Address, AccountKey, nft, res.Item2, tres.Item2);
                        break;
                    case NFTTypes.Post:
                        rtxid = await NFTHelpers.MintPostNFT(Address, AccountKey, nft, res.Item2, tres.Item2);
                        break;
                    case NFTTypes.Profile:
                        rtxid = await NFTHelpers.MintProfileNFT(Address, AccountKey, nft, res.Item2, tres.Item2);
                        break;
                }
                if (rtxid != null)
                {
                    await InvokeSendPaymentSuccessEvent(rtxid, "Neblio NFT Sent");
                    if (NFT.Type == NFTTypes.Profile)
                        Profile = NFT as ProfileNFT;

                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }

            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");
        }

        public async Task<(bool, string)> ChangeProfileNFT(INFT NFT)
        {
            var nft = await NFTFactory.CloneNFT(NFT);

            if (IsLocked())
            {
                await InvokeAccountLockedEvent();
                return (false, "Account is locked.");
            }
            if (string.IsNullOrEmpty(NFT.Utxo))
            {
                await InvokeErrorDuringSendEvent("Cannot change profile without providen Utxo TxId.", "Cannot change the profile.");
                return (false, "Cannot change Profile without provided Utxo TxId.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable Neblio inputs");
                return (false, res.Item1);
            }

            try
            {
                var rtxid = await NFTHelpers.ChangeProfileNFT(Address, AccountKey, nft, res.Item2);

                if (rtxid != null)
                {
                    await InvokeSendPaymentSuccessEvent(rtxid, "Profile Changed");
                    Profile = NFT as ProfileNFT;
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }

            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");

        }

        public async Task<(bool, string)> ChangePostNFT(INFT NFT)
        {
            var nft = await NFTFactory.CloneNFT(NFT);

            if (IsLocked())
            {
                await InvokeAccountLockedEvent();
                return (false, "Account is locked.");
            }
            if (string.IsNullOrEmpty(NFT.Utxo))
            {
                await InvokeErrorDuringSendEvent("Cannot change Post NFT without provided Utxo TxId.", "Cannot change the Post NFT");
                return (false, "Cannot change NFT without provided Utxo TxId.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable Neblio inputs");
                return (false, res.Item1);
            }

            try
            {
                var rtxid = await NFTHelpers.ChangePostNFT(Address, AccountKey, nft, res.Item2);

                if (rtxid != null)
                {
                    await InvokeSendPaymentSuccessEvent(rtxid, "NFT Post Changed");
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }

            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");
        }

        public async Task<(bool, string)> SendNFT(string receiver, INFT NFT, bool priceWrite, double price)
        {
            var nft = await NFTFactory.CloneNFT(NFT);

            if (IsLocked())
            {
                await InvokeAccountLockedEvent();
                return (false, "Account is locked.");
            }
            if (string.IsNullOrEmpty(NFT.Utxo))
            {
                await InvokeErrorDuringSendEvent("Cannot snd NFT without provided Utxo TxId.", "Cannot send NFT");
                return (false, "Cannot send NFT without provided Utxo TxId.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable Neblio inputs");
                return (false, res.Item1);
            }

            if (string.IsNullOrEmpty(receiver) || priceWrite)
                receiver = Address;

            try
            {
                var rtxid = await NFTHelpers.SendNFT(Address, receiver, AccountKey, nft, priceWrite, res.Item2, price);

                if (rtxid != null)
                {
                    if (!priceWrite)
                        await InvokeSendPaymentSuccessEvent(rtxid, "NFT Sent");
                    else
                        await InvokeSendPaymentSuccessEvent(rtxid, "Price written to NFT");
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }

            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");
        }

        public async Task<(bool, string)> SendNFTPayment(string receiver, INFT NFT)
        {
            var nft = await NFTFactory.CloneNFT(NFT);

            if (IsLocked())
            {
                await InvokeAccountLockedEvent();
                return (false, "Account is locked.");
            }
            if (string.IsNullOrEmpty(NFT.Utxo))
            {
                await InvokeErrorDuringSendEvent("Cannot send NFT Payment without provided Utxo TxId of this NFT", "Cannot send Payment for NFT");
                return (false, "Cannot send NFT without provided Utxo TxId.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable Neblio inputs");
                return (false, res.Item1);
            }

            try
            {
                var rtxid = await NFTHelpers.SendNFTPayment(Address, AccountKey, receiver, nft, res.Item2);

                if (rtxid != null)
                {
                    await InvokeSendPaymentSuccessEvent(rtxid, "Payment for NFT Sent");
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }

            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");
        }
    }
}
