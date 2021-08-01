﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VEDriversLite;
using VEDriversLite.Common;
using VEDriversLite.Dto;
using VEDriversLite.NFT;
using VEDriversLite.NFT.Coruzant;
using VEDriversLite.Security;
using VEDriversLite.UnstoppableDomains;
using VEDriversLite.WooCommerce;

namespace TestVEDriversLite
{
    public static class VEDriversLiteTests
    {
        private static string password = string.Empty;
        private static NeblioAccount account = new NeblioAccount();
        private static DogeAccount dogeAccount = new DogeAccount();

        [TestEntry]
        public static void Help(string param)
        {
            Console.WriteLine("Help for The Tests");
            Console.WriteLine("----------------------------------");
            Console.WriteLine("For detail info about functions please look inside of the functions.");
            Console.WriteLine("Important for running:");
            Console.WriteLine("- run GenerateNewAccount if you dont have account. It will create file key.txt where is your new address and key.");
            Console.WriteLine("- run LoadAccount if you have account and stored key.txt file");
            Console.WriteLine("- run LoadAccountWithCreds if you have account but you want to fill manually pass,ekey,address");
            Console.WriteLine("Start of auto refreshing account data is called after successfull load");
            Console.WriteLine("---------------------------------");
        }

        [TestEntry]
        public static void GenerateNewAccount(string param)
        {
            GenerateNewAccountAsync(param);
        }
        public static async Task GenerateNewAccountAsync(string param)
        {
            password = param;
            await account.CreateNewAccount(password, true);
            Console.WriteLine($"Account created.");
            Console.WriteLine($"Address: {account.Address}");
            Console.WriteLine($"Encrypted Private Key: {await account.AccountKey.GetEncryptedKey("", true)}");
            StartRefreshingData(null);
        }


        [TestEntry]
        public static void LoadAccount(string param)
        {
            LoadAccountAsync(param);
        }
        public static async Task LoadAccountAsync(string param)
        {
            if (string.IsNullOrEmpty(param))
                throw new Exception("Password cannot be empty.");

            password = param;
            await account.LoadAccount(password);
        }

        [TestEntry]
        public static void LoadAccountFromFile(string param)
        {
            LoadAccountFromFileAsync(param);
        }
        public static async Task LoadAccountFromFileAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input pass,filename");
            var pass = split[0];
            var file = split[1];
            await account.LoadAccount(pass, file, false);
        }

        [TestEntry]
        public static void LoadAccountFromFileAndDestroyAllNFTs(string param)
        {
            LoadAccountFromFileAndDestroyAllNFTsAsync(param);
        }
        public static async Task LoadAccountFromFileAndDestroyAllNFTsAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 1)
                throw new Exception("Please input pass,filename");
            var file = string.Empty;
            var pass = string.Empty;

            if (split.Length == 1)
                file = split[0];
            else if (split.Length == 2)
            {
                pass = split[0];
                file = split[1];
            }
            else
            {
                Console.WriteLine("wrong input.");
                return;
            }
            await account.LoadAccount(pass, file, false);

            Console.WriteLine("Loading account NFTs...");
            var attempts = 100;
            while(account.NFTs.Count == 0)
            {
                await Task.Delay(1000);
                if (attempts < 0)
                {
                    Console.WriteLine("Cannot find any NFTs on this address.");
                    return;
                }
                else
                {
                    attempts--;
                }
            }

            Console.WriteLine($"NFTs found. There are {account.NFTs.Count} to destroy. Starting now...");
            while (account.NFTs.Count > 1)
            {
                var nftsToDestroy = new List<INFT>();

                if (account.NFTs.Count >= 10)
                    for (var i = 0; i < 10; i++)
                        nftsToDestroy.Add(account.NFTs[i]);
                else
                    foreach (var nft in account.NFTs)
                        nftsToDestroy.Add(nft);

                if (nftsToDestroy.Count == 0)
                    break;

                Console.WriteLine($"Starting to destroy lot of {nftsToDestroy.Count} NFts from rest of {account.NFTs.Count}. ");
                var done = false;
                while (!done)
                {
                    try
                    {
                        var res = await account.DestroyNFTs(nftsToDestroy);
                        done = res.Item1;
                        if (!done)
                        {
                            await Task.Delay(5000);
                            Console.WriteLine("Probably waiting for enough confirmations." + res.Item2);
                        }
                        else
                        {
                            Console.WriteLine("Another NFTs destroyed. TxId:" + res.Item2);
                            await Task.Delay(7000); // wait to until main account update.
                        }
                        if (nftsToDestroy.Count == 0 || account.NFTs.Count == 0)
                            break;
                    }
                    catch (Exception ex)
                    {
                        await Task.Delay(5000);
                        Console.WriteLine("Probably .Waiting for enough confirmations." + ex.Message);
                    }
                }
            }

            Console.WriteLine("All NFTs destroyed.");

        }

        [TestEntry]
        public static void LoadAccountFromVENFTBackup(string param)
        {
            LoadAccountFromVENFTBackupAsync(param);
        }
        public static async Task LoadAccountFromVENFTBackupAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input pass,filename");
            var pass = split[0];
            var file = split[1];
            await account.LoadAccountFromVENFTBackup(pass, file);
            StartRefreshingData(null);
        }

        [TestEntry]
        public static void StartRefreshingData(string param)
        {
            StartRefreshingDataAsync(param);
        }
        public static async Task StartRefreshingDataAsync(string param)
        {
            if (string.IsNullOrEmpty(account.Address))
                throw new Exception("Account is not initialized.");

            await account.StartRefreshingData();
            Console.WriteLine("Refreshing started.");
        }

        [TestEntry]
        public static void LoadAccountWithCreds(string param)
        {
            LoadAccountWithCredsAsync(param);
        }
        public static async Task LoadAccountWithCredsAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input pass,encryptedprivatekey,address");
            var pass = split[0];
            var ekey = split[1];
            var addr = split[2];
            await account.LoadAccount(pass, ekey, addr);
        }

        [TestEntry]
        public static void GetDecryptedPrivateKey(string param)
        {
            GetDecryptedPrivateKeyAsync(param);
        }
        public static async Task GetDecryptedPrivateKeyAsync(string param)
        {
            if (string.IsNullOrEmpty(account.Address))
                throw new Exception("Account is not initialized.");
            if (account.IsLocked())
                throw new Exception("Account is locked.");
            var res = await account.AccountKey.GetEncryptedKey();
            Console.WriteLine($"Private key for address {account.Address} is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void DisplayAccountDetails(string param)
        {
            Console.WriteLine("Account Details");
            Console.WriteLine($"Account Total Balance: {account.TotalBalance} NEBL");
            Console.WriteLine($"Account Total Spendable Balance: {account.TotalSpendableBalance} NEBL");
            Console.WriteLine($"Account Total Unconfirmed Balance: {account.TotalUnconfirmedBalance} NEBL");
            Console.WriteLine($"Account Total Tx Count: {account.AddressInfo.Transactions?.Count}");
            Console.WriteLine($"Account Total Utxos Count: {account.Utxos.Count}");
            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine("Account Utxos:");
            account.Utxos.ForEach(u => { Console.WriteLine($"Utxo: {u.Txid}:{u.Index}"); });
            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine($"Account Total NFTs Count: {account.AddressNFTCount}");
            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine("Account NFTs Names:");
            account.NFTs.ForEach(n => { Console.WriteLine($"NFT Name is: {n.Name}"); });
            Console.WriteLine("-----------------------------------------------------");
        }

        [TestEntry]
        public static void SendTransaction(string param)
        {
            SendTransactionAsync(param);
        }
        public static async Task SendTransactionAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 1)
                throw new Exception("Please input receiveraddress,amountofneblio");
            var receiver = split[0];
            var am = split[1];
            var amount = Convert.ToDouble(am, CultureInfo.InvariantCulture);
            var res = await account.SendNeblioPayment(receiver, amount);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void SendTransactionWithMessage(string param)
        {
            SendTransactionWithMessageAsync(param);
        }
        public static async Task SendTransactionWithMessageAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 1)
                throw new Exception("Please input receiveraddress,amountofneblio,message");
            var receiver = split[0];
            var am = split[1];
            var msg = split[2];
            var amount = Convert.ToDouble(am, CultureInfo.InvariantCulture);
            var res = await account.SendNeblioPayment(receiver, amount, msg);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }


        [TestEntry]
        public static void SendSplitTransaction(string param)
        {
            SendSplitTransactionAsync(param);
        }
        public static async Task SendSplitTransactionAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 1)
                throw new Exception("Please input receiveraddress,amountofneblio,count");
            var receiver = split[0];
            var am = split[1];
            var cnt = split[2];
            var amount = Convert.ToDouble(am, CultureInfo.InvariantCulture);
            var count = Convert.ToInt32(cnt, CultureInfo.InvariantCulture);
            var res = await account.SplitNeblioCoin(new List<string>() { receiver }, count, amount);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void DestroyNFTs(string param)
        {
            DestroyNFTsAsync(param);
        }
        public static async Task DestroyNFTsAsync(string param)
        {
            var nfts = new List<INFT>();
            var nftlist = new List<(string, int)>()
            {
                ("5d5ccf74b2d142c063e01bec584b98edd73e5ca529cf2810db36114eb6bfd208",0),
                ("5d5ccf74b2d142c063e01bec584b98edd73e5ca529cf2810db36114eb6bfd208",1),
                ("5d5ccf74b2d142c063e01bec584b98edd73e5ca529cf2810db36114eb6bfd208",2),
                ("5d5ccf74b2d142c063e01bec584b98edd73e5ca529cf2810db36114eb6bfd208",3),
                ("5d5ccf74b2d142c063e01bec584b98edd73e5ca529cf2810db36114eb6bfd208",4)
            };

            foreach (var nft in nftlist)
            {
                var n = await NFTFactory.GetNFT(NFTHelpers.TokenId, nft.Item1, nft.Item2, 0, true);
                n.UtxoIndex = nft.Item2;
                nfts.Add(n);
            }

            var res = await account.DestroyNFTs(nfts);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void SendAirdrop(string param)
        {
            SendAirdropAsync(param);
        }
        public static async Task SendAirdropAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 3)
                throw new Exception("Please input receiveraddress,tokenId,tokenamount,amountofneblio");
            var receiver = split[0];
            var tokid = split[1];
            var tam = split[2];
            var am = split[3];
            var tamount = Convert.ToDouble(tam, CultureInfo.InvariantCulture);
            var amount = Convert.ToDouble(am, CultureInfo.InvariantCulture);
            var res = await account.SendAirdrop(receiver, tokid, tamount, amount);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }


        [TestEntry]
        public static void SendVENFTAirdrop(string param)
        {
            SendVENFTAirdropAsync(param);
        }
        public static async Task SendVENFTAirdropAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 1)
                throw new Exception("Please input receiveraddress");
            var receiver = split[0];
            var tokid = "La58e9EeXUMx41uyfqk6kgVWAQq9yBs44nuQW8";
            var tamount = 100;
            var amount = 0.05;
            var res = await account.SendAirdrop(receiver, tokid, tamount, amount);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void SendVENFTAirdropFromFile(string param)
        {
            SendVENFTAirdropFromFileAsync(param);
        }
        public static async Task SendVENFTAirdropFromFileAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 1)
                throw new Exception("Please input filename");
            var filename = split[0];
            var receivers = FileHelpers.ReadTextFromFile(filename);
            var receiversList = new List<string>();
            var tokid = "La58e9EeXUMx41uyfqk6kgVWAQq9yBs44nuQW8";
            var tamount = 100;
            var amount = 0.05;

            (bool, string) res = (false, string.Empty);

            Console.WriteLine("----------------------------------------");
            Console.WriteLine("Automatic VENFT Airdrop started");
            Console.WriteLine("----------------------------------------");
            using (var reader = new StringReader(receivers))
            {
                for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    var done = false;
                    while (!done)
                    {
                        try
                        {
                            Console.WriteLine($"Airdrop For address: {line} started.");
                            res = await account.SendAirdrop(line, tokid, tamount, amount);
                            done = res.Item1;
                        }
                        catch (Exception ex)
                        {
                            // probably just waiting for enought confirmation
                            await Task.Delay(5000);
                        }
                    }

                    Console.WriteLine($"New Airdrop for {line} has TxId hash ");
                    Console.WriteLine(res.Item2);
                    Console.WriteLine("----------------------------------------");
                }
            }
        }

        [TestEntry]
        public static void SendTokenTransaction(string param)
        {
            SendTokenTransactionAsync(param);
        }
        public static async Task SendTokenTransactionAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 3)
                throw new Exception("Please input receiveraddress,amountoftokens,data");
            var receiver = split[0];
            var am = split[1];
            var data = split[2];

            var amount = Convert.ToInt32(am);
            // create metadata
            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(data))
                metadata.Add("Data", data);
            else
                metadata.Add("Data", "My first Neblio token metadata");
            // send 10 VENFT to receiver with connected metadata
            var res = await account.SendNeblioTokenPayment(NFTHelpers.TokenId, metadata, receiver, amount);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void MintNFT(string param)
        {
            MintNFTAsync(param);
        }
        public static async Task MintNFTAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 5)
                throw new Exception("Please input name,author,description,link,imagelink");

            Console.WriteLine("Minting NFT");
            // create NFT object
            var nft = new ImageNFT("");

            Console.WriteLine("Fill name:");
            var data = split[0];
            if (!string.IsNullOrEmpty(data))
                nft.Name = data;
            else
                nft.Name = "My First NFT";

            Console.WriteLine("Fill Author:");
            data = split[1];
            if (!string.IsNullOrEmpty(data))
                nft.Author = data;
            else
                nft.Author = "fyziktom";

            Console.WriteLine("Fill Description:");
            data = split[2];
            if (!string.IsNullOrEmpty(data))
                nft.Description = data;
            else
                nft.Description = "This was created with VEDriversLite";

            Console.WriteLine("Fill Link:");
            data = split[3];
            if (!string.IsNullOrEmpty(data))
                nft.Link = data;
            else
                nft.Link = "https://veframework.com/";

            Console.WriteLine("Fill Image Link:");
            data = split[4];
            if (!string.IsNullOrEmpty(data))
                nft.ImageLink = data;
            else
                nft.ImageLink = "https://gateway.ipfs.io/ipfs/QmWTkVqaWn1ABZ1UMKL91pxxspzXW6yodJ9bjUn6nPLeHX";

            // MintNFT
            var res = await account.MintNFT(nft);

            // or multimint with 5 coppies (mint 1 + 5);
            // var res = await account.MintMultiNFT(NFTHelpers.TokenId, nft, 5); 
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void MintNFTTicket(string param)
        {
            MintNFTTicketAsync(param);
        }
        public static async Task MintNFTTicketAsync(string param)
        {
            Console.WriteLine("Minting NFT");
            // create NFT object
            var nft = new TicketNFT("");

            /*
            nft.Author = "fyziktom";
            nft.Name = "Birth of the fyziktom :D";
            nft.Description = "The moment when the fyziktom start to see the world with own eyes.";
            nft.Tags = "fyziktom birth revolution robot timetravel";
            nft.Link = "https://veframework.com/";
            nft.AuthorLink = "https://fyziktom.com/";
            nft.EventId = "531221bc8a59b5b36af8dcaf6dac317b204b89dfc3ed301d497032c3bcf5799c";
            nft.EventDate = DateTime.Parse("1991-03-11T10:20:00");
            nft.Location = "Brno,Czech Republic";
            nft.LocationCoordinates = "49.175621,16.569651";
            nft.Seat = "Row A, Seat 55";
            nft.Price = 0.05;
            nft.PriceInDoge = 10;
            nft.TicketClass = ClassOfNFTTicket.VIP;
            nft.VideoLink = "https://youtu.be/dwemJ4Sx1CA";
            nft.ImageLink = "https://gateway.ipfs.io/ipfs/QmQ5qNNtShVqrZstzWMTZeWXFSnDojks3RWZpja6gJy8MJ";
            */

            nft.Author = "Elton John";
            nft.Name = "FAREWELL - The Final Tour";
            nft.Description = "The Final Tour of genius of the music.";
            nft.Tags = "eltonjohn tour genius";
            nft.Link = "https://www.eltonjohn.com/";
            nft.AuthorLink = "https://www.eltonjohn.com/";
            nft.EventId = "531221bc8a59b5b36af8dcaf6dac317b204b89dfc3ed301d497032c3bcf5799c";
            nft.EventAddress = "NWHozNL3B85PcTXhipmFoBMbfonyrS9WiR";
            nft.EventDate = DateTime.Parse("2022-07-15T04:20:00");
            nft.Location = "Philadelphia,The USA";
            nft.LocationCoordinates = "39.947041,-75.165295";
            nft.Seat = "Section A";
            nft.Price = 10;
            nft.PriceInDoge = 10;
            nft.TicketClass = ClassOfNFTTicket.VIP;
            nft.VideoLink = "https://youtu.be/ZHwVBirqD2s";
            nft.ImageLink = "https://gateway.ipfs.io/ipfs/QmW91e2zi7ndzgonneee7LNWRASHGPnqAS6FvxgCPThaPv";
            nft.MusicInLink = true;

            // count of the tickets
            int cps = 3;

            Console.WriteLine("Start of minting tickets.");
            int lots = 0;
            int rest = 0;
            rest += cps % NeblioTransactionHelpers.MaximumTokensOutpus;
            lots += (int)((cps - rest) / NeblioTransactionHelpers.MaximumTokensOutpus);
            (bool, string) res = (false, string.Empty);

            if (lots > 1 || (lots == 1 && rest > 0))
            {
                var done = false;
                for (int i = 0; i < lots; i++)
                {
                    Console.WriteLine("-----------------------------");
                    Console.WriteLine($"Minting lot {i} from {lots}:");
                    done = false;
                    while (!done)
                    {
                        res = await account.MintMultiNFT(nft, NeblioTransactionHelpers.MaximumTokensOutpus);
                        done = res.Item1;
                        if (!done)
                        {
                            Console.WriteLine("Waiting for spendable utxo...");
                            await Task.Delay(5000);
                        }
                    }
                    Console.WriteLine("New TxId hash is: ");
                    Console.WriteLine(res.Item2);
                }
                if (rest > 0)
                {
                    Console.WriteLine($"Minting rest {rest} tickets:");
                    done = false;
                    while (!done)
                    {
                        res = await account.MintMultiNFT(nft, rest);
                        done = res.Item1;
                        if (!done)
                        {
                            Console.WriteLine("Waiting for spendable utxo...");
                            await Task.Delay(5000);
                        }
                    }
                    Console.WriteLine("New TxId hash is: ");
                    Console.WriteLine(res.Item2);
                }
            }
            else
            {
                res = await account.MintMultiNFT(nft, cps);
            }

            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void MintNFTTicketFromEvent(string param)
        {
            MintNFTTicketFromEventAsync(param);
        }
        public static async Task MintNFTTicketFromEventAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 3)
                throw new Exception("Please input eventId,eventAddress,tickettype,receiver,amount");
            var eventId = split[0];
            var eventAddress = split[1];
            var ticketType = split[2];
            var receiver = split[3];
            var amount = Convert.ToInt32(split[4]);

            Console.WriteLine("Minting NFT");

            var enft = await NFTFactory.GetNFT("", eventId, 0, 0, true, true, NFTTypes.Event);
            if (enft == null)
            {
                Console.WriteLine("Cannot find event NFT. Quit...");
                return;
            }
            // create NFT object
            var nft = new TicketNFT("");
            await nft.FillFromEventNFT(enft);
            nft.TicketClass = (ClassOfNFTTicket)Enum.Parse(typeof(ClassOfNFTTicket), ticketType);

            // count of the tickets
            int cps = amount;
            Console.WriteLine("Start of minting tickets.");
            int lots = 0;
            int rest = 0;
            rest += cps % NeblioTransactionHelpers.MaximumTokensOutpus;
            lots += (int)((cps - rest) / NeblioTransactionHelpers.MaximumTokensOutpus);
            (bool, string) res = (false, string.Empty);

            if (lots > 1 || (lots == 1 && rest > 0))
            {
                var done = false;
                for (int i = 0; i < lots; i++)
                {
                    Console.WriteLine("-----------------------------");
                    Console.WriteLine($"Minting lot {i} from {lots}:");
                    done = false;
                    while (!done)
                    {
                        res = await account.MintMultiNFT(nft, NeblioTransactionHelpers.MaximumTokensOutpus, receiver);
                        done = res.Item1;
                        if (!done)
                        {
                            Console.WriteLine("Waiting for spendable utxo...");
                            await Task.Delay(5000);
                        }
                    }
                    Console.WriteLine("New TxId hash is: ");
                    Console.WriteLine(res.Item2);
                }
                if (rest > 0)
                {
                    Console.WriteLine($"Minting rest {rest} tickets:");
                    done = false;
                    while (!done)
                    {
                        res = await account.MintMultiNFT(nft, rest, receiver);
                        done = res.Item1;
                        if (!done)
                        {
                            Console.WriteLine("Waiting for spendable utxo...");
                            await Task.Delay(5000);
                        }
                    }
                    Console.WriteLine("New TxId hash is: ");
                    Console.WriteLine(res.Item2);
                }
            }
            else
            {
                res = await account.MintMultiNFT(nft, cps, receiver);
            }

            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void MintNFTEvent(string param)
        {
            MintNFTEventAsync(param);
        }
        public static async Task MintNFTEventAsync(string param)
        {
            Console.WriteLine("Minting NFT Event");
            // create NFT object
            var nft = new EventNFT("");

            nft.Author = "Elton John";
            nft.Name = "FAREWELL - The Final Tour";
            nft.Description = "The Final Tour of genius of the music.";
            nft.Tags = "eltonjohn tour genius";
            nft.Link = "https://www.eltonjohn.com/";
            nft.AuthorLink = "https://www.eltonjohn.com/";
            nft.EventId = "";
            nft.EventDate = DateTime.Parse("2022-07-15T04:20:00");
            nft.Location = "Philadelphia,The USA";
            nft.LocationCoordinates = "39.947041,-75.165295";
            nft.Price = 10;
            nft.PriceInDoge = 10;
            nft.EventClass = ClassOfNFTEvent.Concert;
            nft.VideoLink = "https://youtu.be/ZHwVBirqD2s";
            nft.ImageLink = "https://gateway.ipfs.io/ipfs/QmW91e2zi7ndzgonneee7LNWRASHGPnqAS6FvxgCPThaPv";
            nft.MusicInLink = true;

            Console.WriteLine("Start of minting tickets.");

            var res = await account.MintNFT(nft);
            
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void ChangeNFTEvent(string param)
        {
            ChangeNFTEventAsync(param);
        }
        public static async Task ChangeNFTEventAsync(string param)
        {
            Console.WriteLine("Loading NFT Event");

            var NFT = await NFTFactory.GetNFT("", param, 0, 0, true);

            Console.WriteLine("Changing NFT Event");
            // create NFT object
            var nft = NFT as EventNFT;

            nft.Name = "FAREWELL - The Final Tour";
            nft.EventId = "";
            nft.EventDate = DateTime.UtcNow;//DateTime.Parse("2022-07-15T04:20:00");
            nft.LocationCoordinates = "39.947041,-75.165295";

            Console.WriteLine("Start of minting tickets.");

            var res = await account.MintNFT(nft);

            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }




        [TestEntry]
        public static void SendNFT(string param)
        {
            SendNFTAsync(param);
        }
        public static async Task SendNFTAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 3)
                throw new Exception("Please input receiveraddress,utxo,index");

            Console.WriteLine("Input NFT Utxo: ");
            var nftutxo = split[1];
            Console.WriteLine("Input NFT Utxo Index: ");
            var nftutxoindex = Convert.ToInt32(split[2]);
            // load existing NFT object and wait for whole data synchronisation
            var nft = await NFTFactory.GetNFT(NFTHelpers.TokenId, nftutxo, nftutxoindex, 0, true);
            // send NFT to receiver
            if (nft == null)
                throw new Exception("NFT does not exists!");
            Console.WriteLine("Receiver");
            var receiver = split[0];
            var res = await account.SendNFT(receiver, nft, false, 0);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void SplitNeblioTokens(string param)
        {
            SplitNeblioTokensAsync(param);
        }
        public static async Task SplitNeblioTokensAsync(string param)
        {
            if (string.IsNullOrEmpty(param))
                throw new Exception("Please input filename");

            var file = FileHelpers.ReadTextFromFile(param);
            if (string.IsNullOrEmpty(file))
                throw new Exception("File is empty.");

            var dto = JsonConvert.DeserializeObject<SplitNeblioTokensDto>(file);
            if (dto == null)
                throw new Exception("Cannot deserialize file content.");

            var meta = new Dictionary<string, string>();
            meta.Add("Data", "Thank you.");
            var res = await account.SplitTokens(dto.tokenId, meta, dto.receivers, dto.lots, dto.amount);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void SplitNeblio(string param)
        {
            SplitNeblioAsync(param);
        }
        public static async Task SplitNeblioAsync(string param)
        {
            if (string.IsNullOrEmpty(param))
                throw new Exception("Please input filename");

            var file = FileHelpers.ReadTextFromFile(param);
            if (string.IsNullOrEmpty(file))
                throw new Exception("File is empty.");

            var dto = JsonConvert.DeserializeObject<SplitNeblioDto>(file);
            if (dto == null)
                throw new Exception("Cannot deserialize file content.");

            var res = await account.SplitNeblioCoin(dto.receivers, dto.lots, dto.amount);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void WritePriceToNFT(string param)
        {
            WritePriceToNFTAsync(param);
        }
        public static async Task WritePriceToNFTAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input utxo,index,price");

            Console.WriteLine("Input NFT Utxo: ");
            var nftutxo = split[0];
            Console.WriteLine("Input NFT Utxo Index: ");
            var nftutxoindex = Convert.ToInt32(split[1]);
            // load existing NFT object and wait for whole data synchronisation
            var nft = await NFTFactory.GetNFT(NFTHelpers.TokenId, nftutxo, nftutxoindex, 0, true);
            if (nft == null)
                throw new Exception("NFT does not exists!");
            // send NFT to receiver
            var price = Convert.ToDouble(split[2], CultureInfo.InvariantCulture);
            var res = await account.SendNFT(account.Address, nft, true, price);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void SendNFTPayment(string param)
        {
            SendNFTPaymentAsync(param);
        }
        public static async Task SendNFTPaymentAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 3)
                throw new Exception("Please input utxo, utxoindex, price");

            Console.WriteLine("Input NFT Utxo: ");
            var nftutxo = split[0];
            Console.WriteLine("Input NFT Utxo Index: ");
            var nftutxoindex = Convert.ToInt32(split[1]);
            // load existing NFT object and wait for whole data synchronisation. NFT must have written price!
            var nft = await NFTFactory.GetNFT(NFTHelpers.TokenId, nftutxo, nftutxoindex, 0, true);
            if (nft == null)
                throw new Exception("NFT does not exists!");
            if (!nft.PriceActive)
                throw new Exception("NFT does not have setted price.");
            // send NFT to receiver
            Console.WriteLine("Receiver");
            var receiver = split[2];
            var res = await account.SendNFTPayment(receiver, nft);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void VerificationOfNFTs(string param)
        {
            VerificationOfNFTsAsync(param);
        }
        public static async Task VerificationOfNFTsAsync(string param)
        {
            if (string.IsNullOrEmpty(param))
                throw new Exception("Input Utxo must be filled.");

            Console.WriteLine("Input NFT Utxo: ");
            var nftutxo = param;
            var res = await account.GetNFTVerifyCode(nftutxo);
            Console.WriteLine("Signature for TxId:");
            Console.WriteLine(res.TxId);
            Console.WriteLine("Signature:");
            Console.WriteLine(res.Signature);
            Console.WriteLine("Verifying now...");
            // res is already OwnershipVerificationCodeDto so creating new object is just example
            var vres = await OwnershipVerifier.VerifyOwner(new OwnershipVerificationCodeDto()
            {
                TxId = nftutxo,
                Signature = res.Signature
            });

            Console.WriteLine("Result of verification is: ");
            Console.WriteLine(vres.VerifyResult);
            Console.WriteLine("Owner of NFT is: ");
            Console.WriteLine(vres.Owner);
            Console.WriteLine("Loaded NFT is: ");
            Console.WriteLine(vres.NFT.Name);
        }

        [TestEntry]
        public static void SignMessage(string param)
        {
            SignMessageAsync(param);
        }
        public static async Task SignMessageAsync(string param)
        {
            if (string.IsNullOrEmpty(param))
                throw new Exception("Message must be filled.");

            Console.WriteLine("Mesaage for signature: ");
            Console.WriteLine(param);
            var signature = await account.SignMessage(param);
            Console.WriteLine("Signature of the message is: ");
            Console.WriteLine(signature.Item2);
        }

        [TestEntry]
        public static void VerifyMessage(string param)
        {
            VerifyMessageAsync(param);
        }
        public static async Task VerifyMessageAsync(string param)
        {
            var split = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 3)
                throw new Exception("Please input message,signature,address. Dont use any other separator in this case.");

            Console.WriteLine("Mesaage for signature: ");
            Console.WriteLine(split[0]);
            Console.WriteLine("Signature: ");
            Console.WriteLine(split[1]);
            Console.WriteLine("Address: ");
            Console.WriteLine(split[2]);
            var ver = await ECDSAProvider.VerifyMessage(split[0], split[1], split[2]);
            Console.WriteLine("Signature verification result is: ");
            Console.WriteLine(ver.Item2);
        }

        [TestEntry]
        public static void GetTxDetails(string param)
        {
            GetTxDetailsAsync(param);
        }
        public static async Task GetTxDetailsAsync(string param)
        {
            if (string.IsNullOrEmpty(param))
                throw new Exception("TxId must be filled.");

            Console.WriteLine("Input Tx Id Hash");
            var txinfo = await NeblioTransactionHelpers.GetTransactionInfo(param);
            // sign it with loaded account
            Console.WriteLine("Timestamp");
            Console.WriteLine(TimeHelpers.UnixTimestampToDateTime((double)txinfo.Blocktime));
            Console.WriteLine("Number of confirmations: ");
            Console.WriteLine(txinfo.Confirmations);
            Console.WriteLine("--------------");
            Console.WriteLine("Vins:");
            txinfo.Vin.ToList().ForEach(v =>
            {
                Console.WriteLine($"Vin of value: {v.ValueSat} Nebl sat. from txid: {v.Txid} and vout index {v.Vout}");
            });
            Console.WriteLine("-------------");
            Console.WriteLine("--------------");
            Console.WriteLine("Vouts:");
            txinfo.Vout.ToList().ForEach(v =>
            {
                Console.WriteLine($"Vout index {v.N} of value: {v.Value} Nebl sat. to receiver scrupt pub key {v.ScriptPubKey?.Addresses?.FirstOrDefault()}");
            });
            Console.WriteLine("-------------");
        }


        [TestEntry]
        public static void GetNFTDetails(string param)
        {
            GetNFTDetailsAsync(param);
        }
        public static async Task GetNFTDetailsAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input utxo,index");

            Console.WriteLine("Input NFT Tx Id Hash");
            var txid = split[0];
            Console.WriteLine("Input NFT Utxo Index: ");
            var nftutxoindex = Convert.ToInt32(split[1]);
            var nft = await NFTFactory.GetNFT(NFTHelpers.TokenId, txid, nftutxoindex, 0, true);
            // sign it with loaded account
            Console.WriteLine("Name: ");
            Console.WriteLine(nft.Name);
            if(System.Text.RegularExpressions.Regex.Match(nft.Name, RegexMatchPaterns.EmojiPattern).Success)
                Console.WriteLine("there are emojis in the name");
            Console.WriteLine("Author: ");
            Console.WriteLine(nft.Author);
            Console.WriteLine("Description: ");
            Console.WriteLine(nft.Description);
            Console.WriteLine("Link: ");
            Console.WriteLine(nft.Link);
            Console.WriteLine("Image Link: ");
            Console.WriteLine(nft.ImageLink);
        }

        [TestEntry]
        public static void EncryptMessage(string param)
        {
            EncryptMessageAsync(param);
        }
        public static async Task EncryptMessageAsync(string param)
        {
            if (string.IsNullOrEmpty(account.Address))
                throw new Exception("Account is not initialized.");
            if (account.IsLocked())
                throw new Exception("Account is locked.");
            var split = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input message,address.");

            var pk = await NFTHelpers.GetPubKeyFromLastFoundTx(split[1]);
            if (!pk.Item1)
                throw new Exception("Cannot load public key of this address. Probably does not have any spended transaction.");
            var res = await ECDSAProvider.EncryptMessage(split[0], pk.Item2.ToHex());
            Console.WriteLine("Encrypted message is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void EncryptTextFileContent(string param)
        {
            EncryptTextFileContentAsync(param);
        }
        public static async Task EncryptTextFileContentAsync(string param)
        {
            var split = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input address,filename.");

            if (string.IsNullOrEmpty(account.Address))
                throw new Exception("Account is not initialized.");
            if (account.IsLocked())
                throw new Exception("Account is locked.");
            if (string.IsNullOrEmpty(split[0]))
                throw new Exception("Please input address.");
            if (!FileHelpers.IsFileExists(split[1]))
                throw new Exception("File does not exists.");

            var filecontent = FileHelpers.ReadTextFromFile(split[1]);
            if (string.IsNullOrEmpty(filecontent))
                throw new Exception("File is empty.");

            var pk = await NFTHelpers.GetPubKeyFromLastFoundTx(split[0]);
            if (!pk.Item1)
                throw new Exception("Cannot load public key of this address. Probably does not have any spended transaction.");
            var res = await ECDSAProvider.EncryptMessage(filecontent, pk.Item2.ToHex());
            if (res.Item1)
                FileHelpers.WriteTextToFile("encrypted-" + split[1], res.Item2);
            Console.WriteLine("Encrypted message is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void DecryptMessage(string param)
        {
            DecryptMessageAsync(param);
        }
        public static async Task DecryptMessageAsync(string param)
        {
            if (string.IsNullOrEmpty(account.Address))
                throw new Exception("Account is not initialized.");
            if (account.IsLocked())
                throw new Exception("Account is locked.");

            var res = await ECDSAProvider.DecryptMessage(param, await account.AccountKey.GetEncryptedKey());
            Console.WriteLine("Decrypted message is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void EncryptMessageWithSharedSecret(string param)
        {
            EncryptMessageWithSharedSecretAsync(param);
        }
        public static async Task EncryptMessageWithSharedSecretAsync(string param)
        {
            if (string.IsNullOrEmpty(account.Address))
                throw new Exception("Account is not initialized.");
            if (account.IsLocked())
                throw new Exception("Account is locked.");
            var split = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input message,address.");

            var res = await ECDSAProvider.EncryptStringWithSharedSecret(split[0], split[1], account.Secret);
            Console.WriteLine("Encrypted message is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void DecryptMessageWithSharedSecret(string param)
        {
            DecryptMessageWithSharedSecretAsync(param);
        }
        public static async Task DecryptMessageWithSharedSecretAsync(string param)
        {
            if (string.IsNullOrEmpty(account.Address))
                throw new Exception("Account is not initialized.");
            if (account.IsLocked())
                throw new Exception("Account is locked.");
            var split = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input message,address.");

            var res = await ECDSAProvider.DecryptStringWithSharedSecret(split[0], split[1], account.Secret);
            Console.WriteLine("Decrypted message is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void AESEncryptMessage(string param)
        {
            AESEncryptMessageAsync(param);
        }
        public static async Task AESEncryptMessageAsync(string param)
        {
            var split = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input message,password.");

            var res = await SymetricProvider.EncryptString(split[1], split[0]);
            Console.WriteLine("Encrypted message is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void AESDecryptMessage(string param)
        {
            AESDecryptMessageAsync(param);
        }
        public static async Task AESDecryptMessageAsync(string param)
        {
            var split = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input message,password.");

            var res = await SymetricProvider.DecryptString(split[1], split[0]);
            Console.WriteLine("Decrypted message is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void SendMessageNFT(string param)
        {
            SendMessageNFTAsync(param);
        }
        public static async Task SendMessageNFTAsync(string param)
        {
            var split = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 3)
                throw new Exception("Please input name,message,receiver.");

            if (string.IsNullOrEmpty(split[1]))
                throw new Exception("Message cannot be empty!");

            if (string.IsNullOrEmpty(split[2]))
                throw new Exception("Receiver cannot be empty!");

            var res = await account.SendMessageNFT(split[0], split[1], split[2]);
            if (res.Item1)
                Console.WriteLine("NFT TxId is: ");
            Console.WriteLine(res.Item2);
        }

        [TestEntry]
        public static void LoadMessageNFT(string param)
        {
            LoadMessageNFTAsync(param);
        }
        public static async Task LoadMessageNFTAsync(string param)
        {
            var split = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input txid, txidindex");

            var nft = await NFTFactory.GetNFT(NFTHelpers.TokenId, split[0], Convert.ToInt32(split[1]), 0, true);
            await (nft as MessageNFT).Decrypt(account.Secret);
            Console.WriteLine($"NFT TxId is: {nft.Utxo}");
            Console.WriteLine($"NFT Name is: {nft.Name}");
            Console.WriteLine($"NFT Description is: {nft.Description}");
            Console.WriteLine();
        }


        #region DogeTests

        ////////////////////////////////////////////////////////////
        ///// Doge Tests

        [TestEntry]
        public static void DogeGenerateNewAccount(string param)
        {
            GenerateNewDogeAccountAsync(param);
        }
        public static async Task GenerateNewDogeAccountAsync(string param)
        {
            password = param;
            await dogeAccount.CreateNewAccount(password, true);
            Console.WriteLine($"Account created.");
            Console.WriteLine($"Address: {dogeAccount.Address}");
            Console.WriteLine($"Encrypted Private Key: {await dogeAccount.AccountKey.GetEncryptedKey("", true)}");
            StartRefreshingData(null);
        }

        [TestEntry]
        public static void DogeLoadAccount(string param)
        {
            LoadDogeAccountAsync(param);
        }
        public static async Task LoadDogeAccountAsync(string param)
        {
            if (string.IsNullOrEmpty(param))
                throw new Exception("Password cannot be empty.");

            password = param;
            await dogeAccount.LoadAccount(password);
            DogeStartRefreshingData(null);
        }


        [TestEntry]
        public static void DogeStartRefreshingData(string param)
        {
            StartRefreshingDogeDataAsync(param);
        }
        public static async Task StartRefreshingDogeDataAsync(string param)
        {
            if (string.IsNullOrEmpty(dogeAccount.Address))
                throw new Exception("Account is not initialized.");

            await dogeAccount.StartRefreshingData();
            Console.WriteLine("Refreshing started.");
        }

        [TestEntry]
        public static void DogeLoadAccountWithCreds(string param)
        {
            LoadDogeAccountWithCredsAsync(param);
        }
        public static async Task LoadDogeAccountWithCredsAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input pass,encryptedprivatekey,address");
            var pass = split[0];
            var ekey = split[1];
            var addr = split[2];
            await dogeAccount.LoadAccount(pass, ekey, addr);
            DogeStartRefreshingData(null);
        }

        [TestEntry]
        public static void DogeGetDecryptedPrivateKey(string param)
        {
            GetDogeDecryptedPrivateKeyAsync(param);
        }
        public static async Task GetDogeDecryptedPrivateKeyAsync(string param)
        {
            if (string.IsNullOrEmpty(dogeAccount.Address))
                throw new Exception("Account is not initialized.");
            if (dogeAccount.IsLocked())
                throw new Exception("Account is locked.");
            var res = await dogeAccount.AccountKey.GetEncryptedKey();
            Console.WriteLine($"Private key for address {dogeAccount.Address} is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void DogeDisplayAccountDetails(string param)
        {
            Console.WriteLine("Account Details");
            Console.WriteLine($"Account Total Balance: {dogeAccount.TotalBalance} DOGE");
            Console.WriteLine($"Account Total Spendable Balance: {dogeAccount.TotalSpendableBalance} DOGE");
            Console.WriteLine($"Account Total Unconfirmed Balance: {dogeAccount.TotalUnconfirmedBalance} DOGE");
            Console.WriteLine($"Account Total Utxos Count: {dogeAccount.Utxos.Count}");
            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine("Account Utxos:");
            dogeAccount.Utxos.ForEach(u => { Console.WriteLine($"Utxo: {u.TxId}:{u.N}"); });
            Console.WriteLine("-----------------------------------------------------");
        }

        [TestEntry]
        public static void DogeGetTxDetails(string param)
        {
            DogeGetTxDetailsAsync(param);
        }
        public static async Task DogeGetTxDetailsAsync(string param)
        {
            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine("Request Tx Info");
            var txid = param;
            var txinfo = await DogeTransactionHelpers.TransactionInfoAsync(txid);
            var msg = await DogeTransactionHelpers.ParseDogeMessage(txinfo);
            Console.WriteLine("TxInfo:");
            Console.WriteLine(JsonConvert.SerializeObject(txinfo, Formatting.Indented));
            Console.WriteLine("-------------------------------------------------------");
            if (msg.Item1)
                Console.WriteLine("This Transaction contains message: " + msg.Item2);
        }

        [TestEntry]
        public static void DogeSendTransaction(string param)
        {
            SendDogeTransactionAsync(param);
        }
        public static async Task SendDogeTransactionAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input receiveraddress,amountofdoge");
            var receiver = split[0];
            var am = split[1];
            var amount = Convert.ToDouble(am, CultureInfo.InvariantCulture);
            var res = await dogeAccount.SendPayment(receiver, amount);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void DogeSendTransactionWithMessage(string param)
        {
            DogeSendTransactionWithMessageAsync(param);
        }
        public static async Task DogeSendTransactionWithMessageAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 4)
                throw new Exception("Please input receiveraddress,amountofdoge,fee,message");
            var receiver = split[0];
            var am = split[1];
            var f = split[2];
            var message = split[3];
            var amount = Convert.ToDouble(am, CultureInfo.InvariantCulture);
            var fee = Convert.ToUInt64(f, CultureInfo.InvariantCulture);
            var res = await dogeAccount.SendPayment(receiver, amount, message, fee);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void DogeSendTransactionWithIPFSUpload(string param)
        {
            DogeSendTransactionWithIPFSUploadAsync(param);
        }
        public static async Task DogeSendTransactionWithIPFSUploadAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 3)
                throw new Exception("Please input receiveraddress,amountofdoge,filename");
            var receiver = split[0];
            var am = split[1];
            var amount = Convert.ToDouble(am, CultureInfo.InvariantCulture);
            var fileName = split[2];
            var filebytes = File.ReadAllBytes(fileName);
            var link = string.Empty;
            try
            {
                using (Stream stream = new MemoryStream(filebytes))
                {
                    var imageLink = await NFTHelpers.ipfs.FileSystem.AddAsync(stream, fileName);
                    link = "https://gateway.ipfs.io/ipfs/" + imageLink.ToLink().Id.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during uploading the image to the IPFS." + ex.Message);
            }

            var res = await dogeAccount.SendPayment(receiver, amount, link);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void DogeBuyNFT(string param)
        {
            DogeBuyNFTAsync(param);
        }
        public static async Task DogeBuyNFTAsync(string param)
        {
            var split = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 3)
                throw new Exception("Please input receiveraddress,neblioaddress,nfttxid");
            var receiver = split[0];
            var neblioaddress = split[1];
            var nftid = split[2];
            var nft = await NFTFactory.GetNFT(NFTHelpers.TokenId, nftid);

            var res = await dogeAccount.BuyNFT(neblioaddress, receiver, nft);
            Console.WriteLine("New TxId hash is: ");
            Console.WriteLine(res);
        }

        [TestEntry]
        public static void DogeSignMessage(string param)
        {
            DogeSignMessageAsync(param);
        }
        public static async Task DogeSignMessageAsync(string param)
        {
            if (string.IsNullOrEmpty(param))
                throw new Exception("Message must be filled.");

            Console.WriteLine("Mesaage for signature: ");
            Console.WriteLine(param);
            var signature = await dogeAccount.SignMessage(param);
            Console.WriteLine("Signature of the message is: ");
            Console.WriteLine(signature.Item2);
        }

        [TestEntry]
        public static void DogeVerifyMessage(string param)
        {
            DogeVerifyMessageAsync(param);
        }
        public static async Task DogeVerifyMessageAsync(string param)
        {
            var split = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 3)
                throw new Exception("Please input message,signature,address. Dont use any other separator in this case.");

            Console.WriteLine("Mesaage for signature: ");
            Console.WriteLine(split[0]);
            Console.WriteLine("Signature: ");
            Console.WriteLine(split[1]);
            Console.WriteLine("Address: ");
            Console.WriteLine(split[2]);
            var ver = await ECDSAProvider.VerifyDogeMessage(split[0], split[1], split[2]);
            Console.WriteLine("Signature verification result is: ");
            Console.WriteLine(ver.Item2);
        }

        #endregion
        ///////////////////////////////////////////////////////////////////

        ///////////////////////////////////////////////////////////////////

        #region Coruzant

        [TestEntry]
        public static void CoruzantCreateEmptyProfileNFTFile(string param)
        {
            CoruzantCreateEmptyProfileNFTFileAsync(param);
        }
        public static async Task CoruzantCreateEmptyProfileNFTFileAsync(string param)
        {

            if (string.IsNullOrEmpty(param))
            {
                Console.WriteLine("Please fill file name without type. It will be saved as json file.");
                return;
            }
            Console.WriteLine("Creating File with template for CoruzantProfile NFT.");

            // create NFT object
            var nft = new CoruzantProfileNFT("");
            // Example data
            nft.Name = "Tomas";
            nft.Surname = "Svoboda";
            nft.Nickname = "fyziktom";
            nft.Age = 30;
            nft.Description = "Tomas Svoboda is the Founder and CTO at TechnicInsider and is a technology enthusiast. He has been working with technology since he was a kid. He studied medical electronic devices in high school, and at his university.";
            nft.Author = "Brian E. Thomas";
            nft.ImageLink = "https://coruzant.com/wp-content/uploads/2021/03/svoboda-tomas.jpg";
            nft.IconLink = "https://ntp1-icons.ams3.digitaloceanspaces.com/6e05f020d88f8490190a9d9a625f37b649b7dae0.png";
            nft.Link = "https://fyziktom.com/";
            nft.PodcastLink = "https://gateway.ipfs.io/ipfs/QmTWPM5cCE1wbR5Cn9yr1ZwkzYdaZ9xNSovaA6dirDYp9S";
            nft.PersonalPageLink = "https://coruzant.com/profiles/tomas-svoboda/";
            nft.Linkedin = "fyziktom";
            nft.Twitter = "fyziktom";
            nft.CompanyLink = "https://technicinsider.com/";
            nft.CompanyName = "Technicinsider";
            nft.WorkingPosition = "CEO";
            nft.Tags = "fyziktom technologies industry40 blockchain neblio";
            nft.TokenId = CoruzantNFTHelpers.CoruzantTokenId;

            FileHelpers.WriteTextToFile(param + ".json", JsonConvert.SerializeObject(nft, Formatting.Indented));

            Console.WriteLine("File created.");
        }

        [TestEntry]
        public static void CoruzantMintProfileFormFileNFT(string param)
        {
            CoruzantMintProfileFormFileNFTAsync(param);
        }
        public static async Task CoruzantMintProfileFormFileNFTAsync(string param)
        {
            if (string.IsNullOrEmpty(param))
            {
                Console.WriteLine("Please fill file name without type. Template must be json format with extension .json!");
                return;
            }

            var filecontent = FileHelpers.ReadTextFromFile(param + ".json");
            if (string.IsNullOrEmpty(filecontent))
            {
                Console.WriteLine("File is empty!");
                return;
            }

            Console.WriteLine("Minting NFT");
            // create NFT object
            try
            {
                var inft = JsonConvert.DeserializeObject<PostNFT>(filecontent);
                if (inft.Type == NFTTypes.CoruzantProfile)
                {
                    var nft = JsonConvert.DeserializeObject<CoruzantProfileNFT>(filecontent);
                    if (nft != null)
                    {
                        var res = await account.MintNFT(nft);

                        Console.WriteLine("New TxId hash is: ");
                        Console.WriteLine(res.Item2);
                    }
                }
                else
                {
                    Console.WriteLine("Input file is not template for Coruzant Profile NFT.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cannot deserialize template. Please chcek if it is correct." + ex.Message);
            }
        }

        #endregion
        //////////////////////////////////////////////////////////////////

        //////////////////////////////////////////////////////////////////

        #region Tools

        public class IPFSLinksNFTsDto
        {
            public string Address { get; set; } = string.Empty;
            public string Utxo { get; set; } = string.Empty;
            public int Index { get; set; } = 0;
            public string Link { get; set; } = string.Empty;
            public string ImageLink { get; set; } = string.Empty;
            public string PodcastLink { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public NFTTypes Type { get; set; } = NFTTypes.Image;
        }
        [TestEntry]
        public static void GetAllIpfsLinks(string param)
        {
            GetAllIpfsLinksAsync(param);
        }
        public static async Task GetAllIpfsLinksAsync(string param)
        {

            var owners = await NeblioTransactionHelpers.GetTokenOwners(NFTHelpers.TokenId);

            var ipfsLinkNFTs = new Dictionary<string, IPFSLinksNFTsDto>();

            Console.WriteLine("Starting searching the owners:");
            Console.WriteLine("--------------------------------");
            foreach (var own in owners)
            {
                Console.WriteLine("--------------------------------");
                Console.WriteLine($"Owner {own.Address}. Loading NFTS...");
                var addnfts = await NFTHelpers.LoadAddressNFTs(own.Address);
                Console.WriteLine("--------------------------------");
                Console.WriteLine($"----------{addnfts.Count} NFT Loaded------------");
                foreach (var nft in addnfts)
                {
                    if (!string.IsNullOrEmpty(nft.Link) || !string.IsNullOrEmpty(nft.ImageLink))
                    {
                        var save = false;
                        if (!string.IsNullOrEmpty(nft.Link))
                            if (nft.Link.Contains("https://gateway.ipfs.io/ipfs/"))
                                save = true;
                        if (!string.IsNullOrEmpty(nft.ImageLink))
                            if (nft.ImageLink.Contains("https://gateway.ipfs.io/ipfs/"))
                                save = true;

                        if (save)
                        {
                            var dto = new IPFSLinksNFTsDto()
                            {
                                Address = own.Address,
                                Utxo = nft.Utxo,
                                Index = nft.UtxoIndex,
                                Link = nft.Link,
                                ImageLink = nft.ImageLink,
                                Name = nft.Name,
                                Type = nft.Type
                            };
                            if (dto.Link == null)
                                dto.Link = string.Empty;
                            if (dto.ImageLink == null)
                                dto.ImageLink = string.Empty;

                            if (ipfsLinkNFTs.Values.FirstOrDefault(n => n.Link == dto.Link) != null)
                                dto.Link = string.Empty;
                            if (ipfsLinkNFTs.Values.FirstOrDefault(n => n.ImageLink == dto.ImageLink) != null)
                                dto.ImageLink = string.Empty;
                            if (ipfsLinkNFTs.Values.FirstOrDefault(n => n.PodcastLink == dto.PodcastLink) != null)
                                dto.PodcastLink = string.Empty;

                            if (nft.Type == NFTTypes.CoruzantProfile || nft.Type == NFTTypes.CoruzantArticle || nft.Type == NFTTypes.CoruzantPodcast)
                                dto.PodcastLink = (nft as CommonCoruzantNFT).PodcastLink;

                            ipfsLinkNFTs.Add($"{nft.Utxo}:{nft.UtxoIndex}", dto);
                        }
                    }

                }
                Console.WriteLine("---------------------------------------------------------");
                Console.WriteLine($"-------Processing of address {own.Address} done---------");
            }

            var ipfs = new Ipfs.Http.IpfsClient("http://127.0.0.1:5001");
            foreach (var nft in ipfsLinkNFTs)
            {
                try
                {

                    if (!string.IsNullOrEmpty(nft.Value.ImageLink) && nft.Value.ImageLink.Contains("https://gateway.ipfs.io/ipfs/"))
                    {
                        Console.WriteLine("Pinning...");
                        await ipfs.Pin.AddAsync(nft.Value.ImageLink.Replace("https://gateway.ipfs.io/ipfs/", string.Empty));
                        Console.WriteLine("Pinned. " + nft.Value.ImageLink.Replace("https://gateway.ipfs.io/ipfs/", string.Empty));
                    }
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("Cannot pin " + nft.Value.ImageLink.Replace("https://gateway.ipfs.io/ipfs/", string.Empty));
                }
                try
                {
                    if (!string.IsNullOrEmpty(nft.Value.Link) && nft.Value.Link.Contains("https://gateway.ipfs.io/ipfs/"))
                    {
                        Console.WriteLine("Pinning...");
                        await ipfs.Pin.AddAsync(nft.Value.Link.Replace("https://gateway.ipfs.io/ipfs/", string.Empty));
                        Console.WriteLine("Pinned. " + nft.Value.Link.Replace("https://gateway.ipfs.io/ipfs/", string.Empty));
                    }
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("Cannot pin " + nft.Value.Link.Replace("https://gateway.ipfs.io/ipfs/", string.Empty));
                }
                try
                {
                    if (!string.IsNullOrEmpty(nft.Value.PodcastLink) && nft.Value.PodcastLink.Contains("https://gateway.ipfs.io/ipfs/"))
                    {
                        Console.WriteLine("Pinning...");
                        await ipfs.Pin.AddAsync(nft.Value.PodcastLink.Replace("https://gateway.ipfs.io/ipfs/", string.Empty));
                        Console.WriteLine("Pinned. " + nft.Value.PodcastLink.Replace("https://gateway.ipfs.io/ipfs/", string.Empty));
                    }
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("Cannot pin " + nft.Value.PodcastLink.Replace("https://gateway.ipfs.io/ipfs/", string.Empty));
                }
            }

            var filename = $"{TimeHelpers.DateTimeToUnixTimestamp(DateTime.UtcNow)}-ipfsLinkNFTs.json";
            Console.WriteLine($"Completed search. Saving file. {filename}");
            var output = JsonConvert.SerializeObject(ipfsLinkNFTs, Formatting.Indented);
            FileHelpers.WriteTextToFile(filename, output);
        }

        [TestEntry]
        public static void PinIPFSFile(string param)
        {
            PinIPFSFileAsync(param);
        }
        public static async Task PinIPFSFileAsync(string param)
        {
            var ipfs = new Ipfs.Http.IpfsClient("http://127.0.0.1:5001");

            try
            {
                Console.WriteLine("Start Pinning...");
                var res = await ipfs.Pin.AddAsync(param);
                Console.WriteLine("Pinned.");
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Cannot pin " + param);
            }
            #endregion
            ///////////////////////////////////////////////////////////////////
        }

        [TestEntry]
        public static void GetNeblioAddressFromUDomains(string param)
        {
            GetNeblioAddressFromUDomainsAsync(param);
        }
        public static async Task GetNeblioAddressFromUDomainsAsync(string param)
        {
            Console.WriteLine("Requesting the Unstoppable domains...");

            var add = await UnstoppableDomainsHelpers.GetNeblioAddress(param);

            Console.WriteLine("Neblio Address is:");
            Console.WriteLine(add);
            Console.WriteLine("---------------------"); 
        }

        [TestEntry]
        public static void ValidateNeblioAddress(string param)
        {
            ValidateNeblioAddressAsync(param);
        }
        public static async Task ValidateNeblioAddressAsync(string param)
        {
            Console.WriteLine("Validating the Neblio Address...");

            var add = await NeblioTransactionHelpers.ValidateNeblioAddress(param);

            Console.WriteLine($"Neblio Address {param} is:");
            if (add.Item1)
                Console.WriteLine("Valid.");
            else
                Console.WriteLine("Not Valid.");

            Console.WriteLine("---------------------");
        }

        //////////////////////////////////////////////
        #region WooCommerce

        [TestEntry]
        public static void WoCInitWooCommerceShop(string param)
        {
            WoCInitWooCommerceShopAsync(param);
        }
        public static async Task WoCInitWooCommerceShopAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 4)
                throw new Exception("Please input apiurl,apikey,apisecret,jwt");
            var apiurl = split[0];
            var apikey = split[1];
            var secret = split[2];
            var jwt = split[3];

            Console.WriteLine("--------------------------------------");
            Console.WriteLine("---------WooCommerce Shop Init----------");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("API Url: " + apiurl);
            Console.WriteLine("-----------------------------------------");

            var res = await WooCommerceHelpers.InitStoreApiConnection(apiurl, apikey, secret, jwt, true);

            await WoCGetShopStatsAsync(string.Empty);
        }

        [TestEntry]
        public static void WoCGetWPJWTToken(string param)
        {
            WoCGetWPJWTTokenAsync(param);
        }
        public static async Task WoCGetWPJWTTokenAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 3)
                throw new Exception("Please input apiurl,wplogin,wppass");
            var apiurl = split[0];
            var wplogin = split[1];
            var wppass = split[2];

            Console.WriteLine("--------------------------------------");
            Console.WriteLine("-------WordPress JWT Token Request-------");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("API Url: " + apiurl);
            Console.WriteLine("-----------------------------------------");

            var res = await WooCommerceHelpers.GetJWTToken(apiurl, wplogin, wppass);
            if (res.Item1) 
                Console.WriteLine("Token received from the server.");
            Console.WriteLine("");
            Console.WriteLine(res.Item2);
        }

        [TestEntry]
        public static void WoCGetShopStats(string param)
        {
            WoCGetShopStatsAsync(param);
        }
        public static async Task WoCGetShopStatsAsync(string param)
        {
            if (!WooCommerceHelpers.IsInitialized)
            {
                Console.WriteLine("Shop is not initialized.");
                return;
            }
            Console.WriteLine("--------------------------------------");
            Console.WriteLine("---------WooCommerce Shop Init----------");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("API Url: " + WooCommerceHelpers.Shop.WooCommerceStoreUrl);
            Console.WriteLine("-----------------------------------------");

            Console.WriteLine($"---------Products {WooCommerceHelpers.Shop.Products.Count}----------");

            foreach (var p in WooCommerceHelpers.Shop.Products.Values)
                Console.WriteLine($"Product {p.id} - {p.name}, image url: {p.images.FirstOrDefault()?.src}, price {p.regular_price}.");

            Console.WriteLine($"---------Orders {WooCommerceHelpers.Shop.Orders.Count}---------");

            foreach (var o in WooCommerceHelpers.Shop.Orders.Values)
            {
                Console.WriteLine($"========Order {o.order_key}========");
                Console.WriteLine($"ID: {o.id}");
                Console.WriteLine($"Status: {o.status}");
                Console.WriteLine($"Price: {o.total} {o.currency}.");
                Console.WriteLine($"Items - {o.line_items.Count}:");
                var txid = !string.IsNullOrEmpty(o.transaction_id) ? o.transaction_id : "none";
                Console.WriteLine($"Transaction: {txid}.");

                foreach (var pr in o.line_items)
                    Console.WriteLine($"  - Item {pr.name}, product id {pr.product_id} - price {pr.price}, qty {pr.quantity}.");

                Console.WriteLine("========================");
                if (!string.IsNullOrEmpty(o.billing.first_name))
                {
                    Console.WriteLine("---------");
                    Console.WriteLine($"Billing:");
                    Console.WriteLine(JsonConvert.SerializeObject(o.billing, Formatting.Indented));
                }
                if (!string.IsNullOrEmpty(o.shipping.first_name))
                {
                    Console.WriteLine("---------");
                    Console.WriteLine($"Shipping:");
                    Console.WriteLine(JsonConvert.SerializeObject(o.shipping, Formatting.Indented));
                }
                Console.WriteLine("=========================");
            }

            Console.WriteLine("------------------------------");
            Console.WriteLine("-----------End-----------");
            /*
            var wait = 20;
            while(true)
            {
                await Task.Delay(10000);
               
                if (wait < 0)
                {
                    Console.WriteLine("Continue?");
                    var c = Console.ReadLine();
                    if (c != "" && c != "y" && c != "Y")
                        break;
                }
            }*/
            //Console.WriteLine(res);
        }

        [TestEntry]
        public static void WoCUpdateOrderStatus(string param)
        {
            WoCUpdateOrderStatusAsync(param);
        }
        public static async Task WoCUpdateOrderStatusAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input orderid,status");
            var orderkey = split[0];
            
            Console.WriteLine($"Order Key: {orderkey}");
            
            var res = await WooCommerceHelpers.Shop.UpdateOrderStatus(orderkey, split[1]);
            Console.WriteLine($"Actual status: {res.statusclass}");
            Console.WriteLine($"New Status status: {split[1]}");
            Console.WriteLine("");
            Console.WriteLine("");
            await WoCGetOrderAsync(res.id.ToString());
            //await WoCGetShopStatsAsync("");
        }

        [TestEntry]
        public static void WoCUpdateOrderTransactionId(string param)
        {
            WoCUpdateOrderTransactionIdAsync(param);
        }
        public static async Task WoCUpdateOrderTransactionIdAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                throw new Exception("Please input orderid,txid");
            var orderkey = split[0];

            Console.WriteLine($"Order Key: {orderkey}");
            var res = await WooCommerceHelpers.Shop.UpdateOrderTxId(orderkey, split[1]);
            Console.WriteLine($"Transaction Id: {res.transaction_id}");
            Console.WriteLine("");  
            Console.WriteLine("");
            await WoCGetOrderAsync(res.id.ToString());
            //await WoCGetShopStatsAsync("");
        }

        [TestEntry]
        public static void WoCGetProduct(string param)
        {
            WoCGetProductAsync(param);
        }
        public static async Task WoCGetProductAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 1)
                throw new Exception("Please input productid");
            var productid = Convert.ToInt32(split[0]);

            Console.WriteLine($"Product Id: {productid}");
            var res = await WooCommerceHelpers.Shop.GetProduct(productid);
            Console.WriteLine($"Name: {res.name}");
            Console.WriteLine($"Transaction Id: {res.regular_price}");
            Console.WriteLine("");
            Console.WriteLine("");
        }

        [TestEntry]
        public static void WoCGetOrder(string param)
        {
            WoCGetOrderAsync(param);
        }
        public static async Task WoCGetOrderAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 1)
                throw new Exception("Please input orderid");
            var orderid = Convert.ToInt32(split[0]);

            Console.WriteLine($"Order Id: {orderid}");
            var res = await WooCommerceHelpers.Shop.GetOrder(orderid);
            Console.WriteLine($"Order Key: {res.order_key}");
            Console.WriteLine($"Status: {res.status}");
            Console.WriteLine($"Total: {res.total} {res.currency}");
            Console.WriteLine($"TxId: {res.transaction_id}");
            Console.WriteLine("");
            Console.WriteLine("");
        }

        [TestEntry]
        public static void UploadImageToWPFromIPFS(string param)
        {
            UploadImageToWPFromIPFSAsync(param);
        }
        public static async Task UploadImageToWPFromIPFSAsync(string param)
        {
            var split = param.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 1)
                throw new Exception("Please input link,filename");
            var link = split[0];
            var filename = split[1];
            //var res = await WooCommerceHelpers.UploadIFPSImageToWPByAPI(link, filename);
            var res = await WooCommerceHelpers.UploadIFPSImageToWP(link, filename);
            Console.WriteLine("New Url is: ");
            Console.WriteLine(res.Item2);
        }
        #endregion
    }
}
