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
            TryUpdateStatus("Downloading quote summary...");
            await e.DownloadSummaryAsync();
            TryUpdateStatus("Quote downloaded.");
            TryUpdateStatus("Downloading statistical data...");
            await e.DownloadStatisticsAsync();


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

            //Fill in static data
            ToReturn.MET_AnnualRevenue = MostRecentFS.Revenue;
            ToReturn.MET_AnnualNetIncome = MostRecentFS.NetIncome;
            ToReturn.MET_AnnualOperatingCashFlow = MostRecentFS.OperatingCashFlows;
            ToReturn.MET_AnnualInvestingCashFlow = MostRecentFS.InvestingCashFlows;
            ToReturn.MET_AnnualFinancingCashFlow = MostRecentFS.FinancingCashFlows;
            ToReturn.MET_Assets = MostRecentFS.Assets;
            ToReturn.MET_Equity = MostRecentFS.Equity;
            ToReturn.MET_Liabilities = MostRecentFS.Liabilities;

            //Fill in dividend data
            ToReturn.MET_DividendYield = e.Summary.ForwardDividendYield;
            if (e.Statistics.DividendPayoutRatio > 0)
            {
                ToReturn.MET_DividendPayoutRatio = e.Statistics.DividendPayoutRatio;
            }

            //Revenue data
            if (MostRecentFS.Revenue.HasValue && OneYearOldFS.Revenue.HasValue)
            {
                float RevGrowthPercent = (MostRecentFS.Revenue.Value - OneYearOldFS.Revenue.Value) / OneYearOldFS.Revenue.Value;
                ToReturn.MET_AnnualRevenueGrowth = RevGrowthPercent;
            }

            //Net income growth
            if (MostRecentFS.NetIncome.HasValue && OneYearOldFS.NetIncome.HasValue)
            {
                float NetIncomeGrowthPercent = (MostRecentFS.NetIncome.Value - OneYearOldFS.NetIncome.Value) / OneYearOldFS.NetIncome.Value;
                ToReturn.MET_AnnualNetIncomeGrowth = NetIncomeGrowthPercent;
            }

            //Profit margin percent
            if (MostRecentFS.NetIncome.HasValue && MostRecentFS.Revenue.HasValue)
            {
                float marginpercent = MostRecentFS.NetIncome.Value / MostRecentFS.Revenue.Value; 
                ToReturn.MET_AnnualProfitMarginPercent = marginpercent;
            }

            //Percent equity
            if (MostRecentFS.Equity.HasValue && MostRecentFS.Assets.HasValue)
            {
                float equitypercent = MostRecentFS.Equity.Value / MostRecentFS.Assets.Value;
                ToReturn.MET_PercentEquity = equitypercent;
            }

            //current debt as percent of current assets
            if (MostRecentFS.CurrentAssets.HasValue && MostRecentFS.CurrentLiabilities.HasValue)
            {
                float val = MostRecentFS.CurrentAssets.Value / MostRecentFS.CurrentLiabilities.Value;
                ToReturn.MET_CurrentAssetsAsPercentOfCurrentLiabilities = val;
            }

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