﻿using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VEDriversLite.Bookmarks;
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
        public ProfileNFT Profile { get; set; } = new ProfileNFT("");
        public Dictionary<string, TokenSupplyDto> TokensSupplies { get; set; } = new Dictionary<string, TokenSupplyDto>();
        public List<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
        public GetAddressResponse AddressInfo { get; set; } = new GetAddressResponse();

        public event EventHandler Refreshed;
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

            var minorRefresh = 20;

            // todo cancelation token
            _ = Task.Run(async () =>
            {
                await ReLoadNFTs();
                Profile = await NFTHelpers.FindProfileNFT(NFTs);
                await CheckPayments();
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
                            minorRefresh = 20;
                        }

                        lastNFTcount = AddressNFTCount;

                        Refreshed?.Invoke(this, null);
                    }
                    catch (Exception ex)
                    {
                        // todo
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
                Console.WriteLine("Cannot create account! " + ex.Message);
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
                throw new Exception("Cannot deserialize key from file. Please check file key.txt or delete it for create new address!");
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
                Console.WriteLine("Cannot deserialize the bookmarks.");
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
                return (false,"Already Exists!");

            return (true,JsonConvert.SerializeObject(Bookmarks));
        }

        public async Task<(bool,string)> RemoveBookmark(string address)
        {
            var bk = Bookmarks.FirstOrDefault(b => b.Address == address);
            if (bk != null)
                Bookmarks.Remove(bk);
            else
                return (false,"Not Found!");

            return (true,JsonConvert.SerializeObject(Bookmarks));
        }

        public async Task<string> SerializeBookmarks()
        {
            return JsonConvert.SerializeObject(Bookmarks);
        }

        public async Task ReloadTokenSupply()
        {
            TokensSupplies = await NeblioTransactionHelpers.CheckTokensSupplies(Address);
        }

        public async Task ReloadCountOfNFTs()
        {
            var nftsu = await NeblioTransactionHelpers.GetAddressNFTsUtxos(Address);
            if (nftsu != null)
                AddressNFTCount = nftsu.Count;
        }

        public async Task ReloadMintingSupply()
        {
            var mintingSupply = await NeblioTransactionHelpers.GetActualMintingSupply(Address);
            SourceTokensBalance = mintingSupply.Item1;

        }

        public async Task ReloadAccountInfo()
        {
            AddressInfo = await NeblioTransactionHelpers.AddressInfoAsync(Address);
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
                    return ($"You dont have Neblio on the address. Probably waiting for more than {NeblioTransactionHelpers.MinimumConfirmations} confirmations.", null);
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
                PaymentSentError?.Invoke(this, "Account is locked.");
                return (false, "Account is locked.");
            }
            var res = await CheckSpendableNeblio(amount);
            if (res.Item2 == null)
            {
                PaymentSentError?.Invoke(this, res.Item1);
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
                    PaymentSent?.Invoke(this, rtxid);
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                PaymentSentError?.Invoke(this, ex.Message);
                return (false, ex.Message);
            }

            PaymentSentError?.Invoke(this, "Unexpected error during send.");
            return (false, "Unexpected error during send.");
        }

        public async Task<(bool, string)> SendNeblioTokenPayment(string tokenId, IDictionary<string,string> metadata, string receiver, int amount)
        {
            if (IsLocked())
            {
                PaymentSentError?.Invoke(this, "Account is locked.");
                return (false, "Account is locked.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                PaymentSentError?.Invoke(this, res.Item1);
                return (false, res.Item1);
            }
            var tres = await CheckSpendableNeblioTokens(tokenId, amount);
            if (tres.Item2 == null)
            {
                PaymentSentError?.Invoke(this, tres.Item1);
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
                    PaymentSent?.Invoke(this, rtxid);
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                PaymentSentError?.Invoke(this, ex.Message);
                return (false, ex.Message);
            }

            PaymentSentError?.Invoke(this, "Unexpected error during send.");
            return (false, "Unexpected error during send.");
        }

        public async Task<(bool, string)> MintNFT(string tokenId, INFT NFT)
        {
            if (IsLocked())
            {
                PaymentSentError?.Invoke(this, "Account is locked.");
                return (false, "Account is locked.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                PaymentSentError?.Invoke(this, res.Item1);
                return (false, res.Item1);
            }
            var tres = await CheckSpendableNeblioTokens(tokenId, 2);
            if (tres.Item2 == null)
            {
                PaymentSentError?.Invoke(this, tres.Item1);
                return (false, tres.Item1);
            }

            try
            {
                var rtxid = string.Empty;
                switch (NFT.Type)
                {
                    case NFTTypes.Image:
                        rtxid = await NFTHelpers.MintImageNFT(Address, AccountKey, NFT, res.Item2, tres.Item2);
                        break;
                    case NFTTypes.Post:
                        rtxid = await NFTHelpers.MintPostNFT(Address, AccountKey, NFT, res.Item2, tres.Item2);
                        break;
                    case NFTTypes.Profile:
                        rtxid = await NFTHelpers.MintProfileNFT(Address, AccountKey, NFT, res.Item2, tres.Item2);
                        break;
                }
                if (rtxid != null)
                {
                    PaymentSent?.Invoke(this, rtxid);

                    if (NFT.Type == NFTTypes.Profile)
                        Profile = NFT as ProfileNFT;

                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                PaymentSentError?.Invoke(this, ex.Message);
                return (false, ex.Message);
            }

            PaymentSentError?.Invoke(this, "Unexpected error during send.");
            return (false, "Unexpected error during send.");
        }

        public async Task<(bool, string)> ChangeProfileNFT(INFT NFT)
        {
            if (IsLocked())
            {
                PaymentSentError?.Invoke(this, "Account is locked.");
                return (false, "Account is locked.");
            }
            if (string.IsNullOrEmpty(NFT.Utxo))
            {
                PaymentSentError?.Invoke(this, "Cannot change Profile without provided Utxo TxId.");
                return (false, "Cannot change Profile without provided Utxo TxId.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                PaymentSentError?.Invoke(this, res.Item1);
                return (false, res.Item1);
            }

            try
            {
                var rtxid = await NFTHelpers.ChangeProfileNFT(Address, AccountKey, NFT, res.Item2);

                if (rtxid != null)
                {
                    Profile = NFT as ProfileNFT;
                    PaymentSent?.Invoke(this, rtxid);
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                PaymentSentError?.Invoke(this, ex.Message);
                return (false, ex.Message);
            }

            PaymentSentError?.Invoke(this, "Unexpected error during send.");
            return (false, "Unexpected error during send.");

        }

        public async Task<(bool, string)> ChangePostNFT(INFT NFT)
        {
            if (IsLocked())
            {
                PaymentSentError?.Invoke(this, "Account is locked.");
                return (false, "Account is locked.");
            }
            if (string.IsNullOrEmpty(NFT.Utxo))
            {
                PaymentSentError?.Invoke(this, "Cannot change NFT without provided Utxo TxId.");
                return (false, "Cannot change NFT without provided Utxo TxId.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                PaymentSentError?.Invoke(this, res.Item1);
                return (false, res.Item1);
            }

            try
            {
                var rtxid = await NFTHelpers.ChangePostNFT(Address, AccountKey, NFT, res.Item2);

                if (rtxid != null)
                {
                    PaymentSent?.Invoke(this, rtxid);
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                PaymentSentError?.Invoke(this, ex.Message);
                return (false, ex.Message);
            }

            PaymentSentError?.Invoke(this, "Unexpected error during send.");
            return (false, "Unexpected error during send.");
        }

        public async Task<(bool, string)> SendNFT(string receiver, INFT NFT, bool priceWrite, double price)
        {
            if (IsLocked())
            {
                PaymentSentError?.Invoke(this, "Account is locked.");
                return (false, "Account is locked.");
            }
            if (string.IsNullOrEmpty(NFT.Utxo))
            {
                PaymentSentError?.Invoke(this, "Cannot send NFT without provided Utxo TxId.");
                return (false, "Cannot send NFT without provided Utxo TxId.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                PaymentSentError?.Invoke(this, res.Item1);
                return (false, res.Item1);
            }

            if (string.IsNullOrEmpty(receiver) || priceWrite)
                receiver = Address;

            try
            {
                var rtxid = await NFTHelpers.SendNFT(Address, receiver, AccountKey, NFT, priceWrite, res.Item2, price);

                if (rtxid != null)
                {
                    PaymentSent?.Invoke(this, rtxid);
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                PaymentSentError?.Invoke(this, ex.Message);
                return (false, ex.Message);
            }

            PaymentSentError?.Invoke(this, "Unexpected error during send.");
            return (false, "Unexpected error during send.");
        }

        public async Task<(bool, string)> SendNFTPayment(string receiver, INFT NFT)
        {
            if (IsLocked())
            {
                PaymentSentError?.Invoke(this, "Account is locked.");
                return (false, "Account is locked.");
            }
            if (string.IsNullOrEmpty(NFT.Utxo))
            {
                PaymentSentError?.Invoke(this, "Cannot send NFT without provided Utxo TxId.");
                return (false, "Cannot send NFT without provided Utxo TxId.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                PaymentSentError?.Invoke(this, res.Item1);
                return (false, res.Item1);
            }

            try
            {
                var rtxid = await NFTHelpers.SendNFTPayment(Address, AccountKey, receiver, NFT, res.Item2);

                if (rtxid != null)
                {
                    PaymentSent?.Invoke(this, rtxid);
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                PaymentSentError?.Invoke(this, ex.Message);
                return (false, ex.Message);
            }

            PaymentSentError?.Invoke(this, "Unexpected error during send.");
            return (false, "Unexpected error during send.");
        }
    }
}
