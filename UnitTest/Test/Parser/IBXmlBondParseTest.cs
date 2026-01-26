using InvestmentTaxCalculator.Enumerations;
using InvestmentTaxCalculator.Model;
using InvestmentTaxCalculator.Model.Interfaces;
using InvestmentTaxCalculator.Model.TaxEvents;
using InvestmentTaxCalculator.Model.UkTaxModel;
using InvestmentTaxCalculator.Parser.InteractiveBrokersXml;

using System.Globalization;
using System.Xml.Linq;

using UnitTest.Helper;

namespace UnitTest.Test.Parser;

public class IBXmlBondParseTest
{
    [Fact]
    public void ParseBondBuyTrade_WithAccruedInterest()
    {
        XElement xmlDoc = XElement.Parse(@"<Orders>
            <Order accountId=""U1234567"" currency=""USD"" fxRateToBase=""0.80687"" assetCategory=""BOND""
                symbol=""T 2 1/2 02/28/26"" description=""T 2 1/2 02/28/26"" isin=""US9128286F22""
                dateTime=""31-Jan-25 14:39:08"" quantity=""25000"" tradePrice=""98.191""
                tradeMoney=""24547.75"" proceeds=""-24547.75"" taxes=""0""
                ibCommission=""-5"" ibCommissionCurrency=""USD"" netCash=""-24552.75""
                buySell=""BUY"" levelOfDetail=""ORDER"" accruedInt=""-269.34"" />
        </Orders>");

        IList<Trade> result = IBXmlBondTradeParser.ParseXml(xmlDoc);

        result.Count.ShouldBe(1);
        Trade trade = result[0];
        trade.AssetType.ShouldBe(AssetCategoryType.BOND);
        trade.AcquisitionDisposal.ShouldBe(TradeType.ACQUISITION);
        trade.AssetName.ShouldBe("T 2 1/2 02/28/26");
        trade.Isin.ShouldBe("US9128286F22");
        trade.Quantity.ShouldBe(25000m);
        trade.GrossProceed.Amount.Amount.ShouldBe(24547.75m);
        trade.Date.ShouldBe(DateTime.Parse("31-Jan-25 14:39:08", CultureInfo.InvariantCulture));

        // Should have commission only - accrued interest is handled separately under accrued income scheme
        trade.Expenses.Count.ShouldBe(1);
        trade.Expenses.ShouldContain(e => e.Description == "Commission");
    }

    [Fact]
    public void ParseBondSellTrade_WithAccruedInterest()
    {
        XElement xmlDoc = XElement.Parse(@"<Orders>
            <Order accountId=""U1234567"" currency=""USD"" fxRateToBase=""0.79505"" assetCategory=""BOND""
                symbol=""T 0 3/8 09/15/24"" description=""T 0 3/8 09/15/24"" isin=""US91282CCX74""
                dateTime=""02-Apr-24 15:00:01"" quantity=""-1000"" tradePrice=""97.87857125""
                tradeMoney=""-978.79"" proceeds=""978.79"" taxes=""0""
                ibCommission=""-6.05"" ibCommissionCurrency=""USD"" netCash=""972.74""
                buySell=""SELL"" levelOfDetail=""ORDER"" accruedInt=""0.19"" />
        </Orders>");

        IList<Trade> result = IBXmlBondTradeParser.ParseXml(xmlDoc);

        result.Count.ShouldBe(1);
        Trade trade = result[0];
        trade.AssetType.ShouldBe(AssetCategoryType.BOND);
        trade.AcquisitionDisposal.ShouldBe(TradeType.DISPOSAL);
        trade.AssetName.ShouldBe("T 0 3/8 09/15/24");
        trade.Quantity.ShouldBe(1000m);
        trade.GrossProceed.Amount.Amount.ShouldBe(978.79m);

        // Should have commission only - accrued interest is handled separately under accrued income scheme
        trade.Expenses.Count.ShouldBe(1);
        trade.Expenses.ShouldContain(e => e.Description == "Commission");
    }

    [Fact]
    public void ParseBondTrade_NoAccruedInterest()
    {
        XElement xmlDoc = XElement.Parse(@"<Orders>
            <Order accountId=""U1234567"" currency=""USD"" fxRateToBase=""0.87085"" assetCategory=""BOND""
                symbol=""SP 0 11/15/24"" description=""SP 0 11/15/24"" isin=""US912803BD41""
                dateTime=""01-Nov-22 15:53:56"" quantity=""10000"" tradePrice=""91.2""
                tradeMoney=""9120"" proceeds=""-9120"" taxes=""0""
                ibCommission=""-6.5"" ibCommissionCurrency=""USD"" netCash=""-9126.5""
                buySell=""BUY"" levelOfDetail=""ORDER"" accruedInt=""0"" />
        </Orders>");

        IList<Trade> result = IBXmlBondTradeParser.ParseXml(xmlDoc);

        result.Count.ShouldBe(1);
        Trade trade = result[0];
        trade.AssetType.ShouldBe(AssetCategoryType.BOND);
        trade.Quantity.ShouldBe(10000m);

        // Only commission, no accrued interest
        trade.Expenses.Count.ShouldBe(1);
        trade.Expenses[0].Description.ShouldBe("Commission");
    }

    [Fact]
    public void ParseBondRedemption_CorporateAction()
    {
        XElement xmlDoc = XElement.Parse(@"<CorporateActions>
            <CorporateAction accountId=""U1234567"" currency=""USD"" fxRateToBase=""0.79671""
                assetCategory=""BOND"" symbol=""T 2 1/4 03/31/24"" isin=""US91282CEG24""
                description=""(US91282CEG24) FULL CALL / EARLY REDEMPTION FOR USD 1.00 PER BOND""
                dateTime=""29-Mar-24 20:25:00"" quantity=""-25000"" proceeds=""25000""
                value=""0"" fifoPnlRealized=""599.85"" type=""BM""
                transactionID=""27076129128"" levelOfDetail=""DETAIL"" />
        </CorporateActions>");

        IList<Trade> result = IBXmlBondRedemptionParser.ParseXml(xmlDoc);

        result.Count.ShouldBe(1);
        Trade trade = result[0];
        trade.AssetType.ShouldBe(AssetCategoryType.BOND);
        trade.AcquisitionDisposal.ShouldBe(TradeType.DISPOSAL);
        trade.AssetName.ShouldBe("T 2 1/4 03/31/24");
        trade.Isin.ShouldBe("US91282CEG24");
        trade.Quantity.ShouldBe(25000m);
        trade.GrossProceed.Amount.Amount.ShouldBe(25000m);
        trade.TradeReason.ShouldBe(TradeReason.CorporateAction);
        trade.Date.ShouldBe(DateTime.Parse("29-Mar-24 20:25:00", CultureInfo.InvariantCulture));
        trade.Expenses.ShouldBeEmpty();
    }

    [Fact]
    public void ParseBondTrades_IgnoresStockTrades()
    {
        XElement xmlDoc = XElement.Parse(@"<Orders>
            <Order accountId=""U1234567"" currency=""USD"" fxRateToBase=""0.8"" assetCategory=""STK""
                symbol=""AAPL"" description=""AAPL"" dateTime=""01-Jan-24 10:00:00"" quantity=""100""
                proceeds=""-10000"" taxes=""0"" ibCommission=""-1"" ibCommissionCurrency=""USD""
                buySell=""BUY"" levelOfDetail=""ORDER"" accruedInt=""0"" isin=""US123"" />
            <Order accountId=""U1234567"" currency=""USD"" fxRateToBase=""0.8"" assetCategory=""BOND""
                symbol=""T 2 1/2 02/28/26"" description=""T 2 1/2 02/28/26"" dateTime=""01-Jan-24 10:00:00"" quantity=""10000""
                proceeds=""-9800"" taxes=""0"" ibCommission=""-5"" ibCommissionCurrency=""USD""
                buySell=""BUY"" levelOfDetail=""ORDER"" accruedInt=""0"" isin=""US123"" />
        </Orders>");

        IList<Trade> result = IBXmlBondTradeParser.ParseXml(xmlDoc);

        result.Count.ShouldBe(1);
        result[0].AssetName.ShouldBe("T 2 1/2 02/28/26");
    }

    [Fact]
    public void ParseBondRedemption_IgnoresOtherCorporateActions()
    {
        XElement xmlDoc = XElement.Parse(@"<CorporateActions>
            <CorporateAction accountId=""U1234567"" currency=""USD"" fxRateToBase=""0.8""
                assetCategory=""STK"" symbol=""ABC"" description=""ABC stock split""
                dateTime=""01-Jan-24 20:25:00"" quantity=""100""
                type=""FS"" transactionID=""123"" levelOfDetail=""DETAIL"" />
            <CorporateAction accountId=""U1234567"" currency=""USD"" fxRateToBase=""0.8""
                assetCategory=""BOND"" symbol=""T 2 1/4 03/31/24"" isin=""US123""
                description=""(US123) FULL CALL / EARLY REDEMPTION FOR USD 1.00 PER BOND""
                dateTime=""01-Jan-24 20:25:00"" quantity=""-10000"" proceeds=""10000""
                type=""BM"" transactionID=""456"" levelOfDetail=""DETAIL"" />
        </CorporateActions>");

        IList<Trade> result = IBXmlBondRedemptionParser.ParseXml(xmlDoc);

        result.Count.ShouldBe(1);
        result[0].AssetName.ShouldBe("T 2 1/4 03/31/24");
    }

    [Fact]
    public void BondTaxCalculation_BuyAndSell_CalculatesGain()
    {
        // Buy 10000 face value at 98% = 9800 cost + 5 commission = 9805 total cost
        Trade buyTrade = new()
        {
            AssetType = AssetCategoryType.BOND,
            AssetName = "T 2 1/2 02/28/26",
            AcquisitionDisposal = TradeType.ACQUISITION,
            Date = DateTime.Parse("01-Jan-24 10:00:00", CultureInfo.InvariantCulture),
            Quantity = 10000,
            GrossProceed = new DescribedMoney
            {
                Amount = new WrappedMoney(9800m, "GBP"),
                FxRate = 1m
            },
            Expenses =
            [
                new DescribedMoney { Description = "Commission", Amount = new WrappedMoney(5m, "GBP"), FxRate = 1m }
            ]
        };

        // Sell 10000 face value at 99% = 9900 proceeds - 5 commission = 9895 net
        Trade sellTrade = new()
        {
            AssetType = AssetCategoryType.BOND,
            AssetName = "T 2 1/2 02/28/26",
            AcquisitionDisposal = TradeType.DISPOSAL,
            Date = DateTime.Parse("01-Jun-24 10:00:00", CultureInfo.InvariantCulture),
            Quantity = 10000,
            GrossProceed = new DescribedMoney
            {
                Amount = new WrappedMoney(9900m, "GBP"),
                FxRate = 1m
            },
            Expenses =
            [
                new DescribedMoney { Description = "Commission", Amount = new WrappedMoney(5m, "GBP"), FxRate = 1m }
            ]
        };

        List<ITradeTaxCalculation> result = TradeCalculationHelper.CalculateTrades(
            [buyTrade, sellTrade],
            out UkSection104Pools section104Pools
        );

        // Should have 2 calculations (buy and sell)
        result.Count.ShouldBe(2);

        // Find the disposal
        var disposal = result.First(r => r.AcquisitionDisposal == TradeType.DISPOSAL);
        disposal.AssetName.ShouldBe("T 2 1/2 02/28/26");

        // Gain = (9900 - 5) - (9800 + 5) = 9895 - 9805 = 90
        disposal.Gain.Amount.ShouldBe(90m);

        // Section 104 pool should be empty after selling all
        section104Pools.GetExistingOrInitialise("T 2 1/2 02/28/26").Quantity.ShouldBe(0);
    }

    [Fact]
    public void BondTaxCalculation_BuyAndRedemption_CalculatesGain()
    {
        // Buy 25000 face value at 97% = 24250 cost + 5 commission = 24255 total cost
        Trade buyTrade = new()
        {
            AssetType = AssetCategoryType.BOND,
            AssetName = "T 2 1/4 03/31/24",
            AcquisitionDisposal = TradeType.ACQUISITION,
            Date = DateTime.Parse("01-Jan-24 10:00:00", CultureInfo.InvariantCulture),
            Quantity = 25000,
            GrossProceed = new DescribedMoney
            {
                Amount = new WrappedMoney(24250m, "GBP"),
                FxRate = 1m
            },
            Expenses =
            [
                new DescribedMoney { Description = "Commission", Amount = new WrappedMoney(5m, "GBP"), FxRate = 1m }
            ]
        };

        // Redemption at par = 25000 proceeds (no commission on redemption)
        Trade redemption = new()
        {
            AssetType = AssetCategoryType.BOND,
            AssetName = "T 2 1/4 03/31/24",
            AcquisitionDisposal = TradeType.DISPOSAL,
            Date = DateTime.Parse("31-Mar-24 20:25:00", CultureInfo.InvariantCulture),
            Quantity = 25000,
            GrossProceed = new DescribedMoney
            {
                Amount = new WrappedMoney(25000m, "GBP"),
                FxRate = 1m
            },
            TradeReason = TradeReason.CorporateAction,
            Expenses = []
        };

        List<ITradeTaxCalculation> result = TradeCalculationHelper.CalculateTrades(
            [buyTrade, redemption],
            out UkSection104Pools section104Pools
        );

        result.Count.ShouldBe(2);

        var disposal = result.First(r => r.AcquisitionDisposal == TradeType.DISPOSAL);
        disposal.AssetName.ShouldBe("T 2 1/4 03/31/24");

        // Gain = 25000 - (24250 + 5) = 25000 - 24255 = 745
        disposal.Gain.Amount.ShouldBe(745m);

        section104Pools.GetExistingOrInitialise("T 2 1/4 03/31/24").Quantity.ShouldBe(0);
    }
}
