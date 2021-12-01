using System;
using System.IO;
using System.Threading.Tasks;
using Yahoo.Finance;
using SecuritiesExchangeCommission;
using SecuritiesExchangeCommission.Edgar;
using Xbrl;
using Xbrl.FinancialStatement;
using Xbrl.Helpers;
using TimHanewich.Toolkit;
using TimHanewich.Toolkit.Web;
using System.Collections.Generic;


namespace Aletheia.EquityValuation
{
    public class EquityProfileBuilder
    {
        public event StringHandler StatusUpdated;

        public async Task<EquityProfile> BuildAsync(string for_symbol)
        {
            EquityProfile ToReturn = new EquityProfile();

            //Get equity assessment
            Equity e = Equity.Create(for_symbol);
            TryUpdateStatus("Downloading quote...");
            await e.DownloadSummaryAsync();
            TryUpdateStatus("Quote downloaded.");

            //Plug in identifiers
            ToReturn.MarketCap = Convert.ToInt64(e.Summary.MarketCap);
            ToReturn.Symbol = e.Summary.StockSymbol;
            ToReturn.CapturedAtUtc = DateTime.UtcNow;

            //Search for latest 2 10-K's
            SecuritiesExchangeCommission.Edgar.SecRequestManager.Instance.UserAgent = TimHanewich.Toolkit.Web.UserAgentGenerator.RandomUserAgent();
            TryUpdateStatus("Searching SEC for 10-K's...");
            EdgarSearch es = await EdgarSearch.CreateAsync(for_symbol, "10-K");
            TryUpdateStatus("SEC search complete w/ " + es.Results.Length.ToString() + " results");

            //Throw an error if none found
            if (es.Results == null || es.Results.Length == 0)
            {
                throw new Exception("SEC search did not return any results");
            }

            //Filter to just 10-K's
            List<EdgarSearchResult> Just10Ks = new List<EdgarSearchResult>();
            foreach (EdgarSearchResult esr in es.Results)
            {
                if (esr.Filing.ToLower() == "10-k")
                {
                    Just10Ks.Add(esr);
                }
            }

            //Throw an error if we have no 10-ks
            if (Just10Ks.Count == 0)
            {
                throw new Exception("The SEC result did not yield any 10-K's");
            }


            //Get the most recent 10-K
            TryUpdateStatus("Finding most recent 10-K");
            EdgarSearchResult MostRecent10K = Just10Ks[0];
            foreach (EdgarSearchResult esr in Just10Ks)
            {
                if (esr.FilingDate > MostRecent10K.FilingDate)
                {
                    MostRecent10K = esr;
                }
            }

            //Get the 10-K that comes after that
            TryUpdateStatus("Finding one year old 10-K");
            EdgarSearchResult OneYearOld10K = null;
            foreach (EdgarSearchResult esr in Just10Ks)
            {
                TimeSpan ts = MostRecent10K.FilingDate - esr.FilingDate;
                if (ts.TotalDays > 350 && ts.TotalDays < 380) //Rougly one year
                {
                    OneYearOld10K = esr;
                }
            }

            //If we did not find a one year old 10-K, throw error
            if (OneYearOld10K == null)
            {
                throw new Exception("Was unable to find one year old 10-K");
            }

            //Download for each
            TryUpdateStatus("Downloading most recent XBRL doc...");
            Stream MostRecentXbrl = await MostRecent10K.DownloadXbrlDocumentAsync();
            TryUpdateStatus("Downloading one year old XBRL doc...");
            Stream OneYearOldXbrl = await OneYearOld10K.DownloadXbrlDocumentAsync();

            //Load both into Xbrl instance docs
            TryUpdateStatus("Loading most recent instance doc");
            XbrlInstanceDocument MostRecentDoc = XbrlInstanceDocument.Create(MostRecentXbrl);
            TryUpdateStatus("Loading one year old instance doc");
            XbrlInstanceDocument OneYearOldDoc = XbrlInstanceDocument.Create(OneYearOldXbrl);

            //Convert each to financial statements
            TryUpdateStatus("Converting most recent instance doc to financial statement");
            FinancialStatement MostRecentFS = MostRecentDoc.CreateFinancialStatement();
            TryUpdateStatus("Converting one year old instance doc to financial statement");
            FinancialStatement OneYearOldFS = OneYearOldDoc.CreateFinancialStatement();
        

            return ToReturn;
        }

        private void TryUpdateStatus(string status)
        {
            try
            {
                StatusUpdated.Invoke(status);
            }
            catch
            {

            }
        }
    }
}