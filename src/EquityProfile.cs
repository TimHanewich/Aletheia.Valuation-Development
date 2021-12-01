using System;

namespace Aletheia.EquityValuation
{
    public class EquityProfile
    {

        //THE METRIC WE ARE TRYING TO MEASURE
        public float MarketCap {get; set;}

        //Identifying information
        public string Symbol {get; set;}
        public DateTime CapturedAtUtc {get; set;}

        //Any property that is a metric (will be used in the market cap calculation/NN calculation) will be prepended with "MET_"

        //Static - period based (income statement, cash flow statement)
        public float? MET_AnnualRevenue {get; set;}
        public float? MET_AnnualNetIncome {get; set;}
        public float? MET_AnnualOperatingCashFlow {get; set;}
        public float? MET_AnnualInvestingCashFlow {get; set;}
        public float? MET_AnnualFinancingCashFlow {get; set;}

        //Static - balance sheet
        public float? MET_Assets {get; set;}
        public float? MET_Liabilities {get; set;}
        public float? MET_Equity {get; set;}

        //Growth
        public float? MET_AnnualRevenueGrowth {get; set;}
        public float? MET_AnnualNetIncomeGrowth {get; set;}

        //Dividend
        public float? MET_DividendYield {get; set;}
        public float? MET_DividendPayoutRatio {get; set;}

        //Statistics
        public float? MET_AnnualProfitMarginPercent {get; set;}
        public float? MET_PercentEquity {get; set;}
        public float? MET_CurrentAssetsAsPercentOfCurrentLiabilities {get; set;}
    }
}