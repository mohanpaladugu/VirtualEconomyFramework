﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VEDrivers.Database;
using VEDrivers.Economy.Tokens;
using VEDrivers.Economy.Transactions;

namespace VEDrivers.Economy.Wallets.Handlers
{
    public abstract class CommonAccountHandler : IAccountHandler
    {
        public abstract Task<string> UpdateAccount(string accountAddress, Guid walletId, AccountTypes type, string name, IDbConnectorService dbservice, bool justInDb = true);
        public abstract IDictionary<string, IToken> FindTokenByMetadata(string account, string key, string value = "");
        public abstract IDictionary<string, IToken> FindAllTokens(string account);
        public abstract LastTxSaveDto GetLastAccountProcessedTxs(string address);
    }
}
