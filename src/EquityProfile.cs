using System;

namespace Aletheia.EquityValuation
{
    public class EquityProfile
    {

        //THE METRIC WE ARE TRYING TO MEASURE
        public long MarketCap {get; set;}

        //Identifying information
        public string Symbol {get; set;}
        public DateTime CapturedAtUtc {get; set;}

        //Any property that is a metric (will be used in the market cap calculation/NN calculation) will be prepended with "MET_"

        //Static - period based (income statement, cash flow statement)
        public long? MET_AnnualSales {get; set;}
        public long? MET_AnnualNetIncome {get; set;}
        public long? MET_AnnualOperatingCashFlow {get; set;}
        public long? MET_AnnualInvestingCashFlow {get; set;}
        public long? MET_AnnualFinancingCashFlow {get; set;}

        //Static - balance sheet
        public long? MET_Assets {get; set;}
        public long? MET_Debt {get; set;}
        public long? MET_Equity {get; set;}

        //Growth
        public float? MET_AnnualSalesGrowth {get; set;}
        public float? MET_AnnualNetIncomeGrowth {get; set;}

        //Dividend
        public float? MET_DividendYield {get; set;}
        public float? MET_DividendPayoutRatio {get; set;}

        //Statistics
        public float? MET_AnnualProfitMarginPercent {get; set;}
        public float? MET_PercentEquity {get; set;}
        public float? MET_CurrentDebtAsPercentOfCurrentAssets {get; set;}
    }
}