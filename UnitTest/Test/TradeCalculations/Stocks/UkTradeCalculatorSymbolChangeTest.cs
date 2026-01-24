using InvestmentTaxCalculator.Enumerations;
using InvestmentTaxCalculator.Model;
using InvestmentTaxCalculator.Model.Interfaces;
using InvestmentTaxCalculator.Model.TaxEvents;
using InvestmentTaxCalculator.Model.UkTaxModel;
using InvestmentTaxCalculator.Model.UkTaxModel.Stocks;

using System.Globalization;

using UnitTest.Helper;

namespace UnitTest.Test.TradeCalculations.Stocks;

public class UkTradeCalculatorSymbolChangeTest
{
    [Fact]
    public void TestSection104WithSymbolChange()
    {
        // Buy shares under old SPAC ticker
        Trade spacPurchase = new()
        {
            AssetName = "GNPK",
            AcquisitionDisposal = TradeType.ACQUISITION,
            Date = DateTime.Parse("01-Jun-21 10:00:00", CultureInfo.InvariantCulture),
            Quantity = 1000,
            GrossProceed = new() { Amount = new(10000m, "USD"), FxRate = 0.72m },
        };

        // CUSIP/ISIN change (de-SPAC)
        SymbolChange symbolChange = new()
        {
            AssetName = "RDW",
            OldAssetName = "GNPK",
            Date = DateTime.Parse("02-Sep-21 20:25:00", CultureInfo.InvariantCulture),
        };

        // Sell shares under new ticker
        Trade postChangeSale = new()
        {
            AssetName = "RDW",
            AcquisitionDisposal = TradeType.DISPOSAL,
            Date = DateTime.Parse("01-Dec-21 10:00:00", CultureInfo.InvariantCulture),
            Quantity = 500,
            GrossProceed = new() { Amount = new(7500m, "USD"), FxRate = 0.75m },
        };

        UkSection104Pools section104Pools = new(new UKTaxYear(), new ResidencyStatusRecord());
        TaxEventLists taxEventLists = new();
        taxEventLists.AddData([spacPurchase, symbolChange, postChangeSale]);

        UkTradeCalculator calculator = TradeCalculationHelper.CreateUkTradeCalculator(section104Pools, taxEventLists);
        List<ITradeTaxCalculation> result = calculator.CalculateTax();

        // The acquisition under GNPK should be matched with the disposal under RDW
        result[1].MatchHistory.Count.ShouldBe(1);
        result[1].MatchHistory[0].TradeMatchType.ShouldBe(TaxMatchType.SECTION_104);
        result[1].MatchHistory[0].MatchDisposalQty.ShouldBe(500);

        // S104 pool should be under the new symbol with remaining shares
        section104Pools.GetExistingOrInitialise("RDW").Quantity.ShouldBe(500);
        // Cost basis: 10000 * 0.72 = 7200 total, half sold = 3600 remaining
        section104Pools.GetExistingOrInitialise("RDW").AcquisitionCostInBaseCurrency.Amount.ShouldBe(3600m, 0.01m);
    }

    [Fact]
    public void TestBedAndBreakfastAcrossSymbolChange()
    {
        // Buy shares under old ticker
        Trade purchase = new()
        {
            AssetName = "THCB",
            AcquisitionDisposal = TradeType.ACQUISITION,
            Date = DateTime.Parse("01-Mar-21 10:00:00", CultureInfo.InvariantCulture),
            Quantity = 1000,
            GrossProceed = new() { Amount = new(15000m), FxRate = 1m },
        };

        // Sell shares under old ticker (before symbol change)
        Trade sale = new()
        {
            AssetName = "THCB",
            AcquisitionDisposal = TradeType.DISPOSAL,
            Date = DateTime.Parse("15-Jul-21 10:00:00", CultureInfo.InvariantCulture),
            Quantity = 1000,
            GrossProceed = new() { Amount = new(12000m), FxRate = 1m },
        };

        // Symbol change
        SymbolChange symbolChange = new()
        {
            AssetName = "MVST",
            OldAssetName = "THCB",
            Date = DateTime.Parse("23-Jul-21 20:25:00", CultureInfo.InvariantCulture),
        };

        // Repurchase under new ticker within 30 days of sale
        Trade repurchase = new()
        {
            AssetName = "MVST",
            AcquisitionDisposal = TradeType.ACQUISITION,
            Date = DateTime.Parse("25-Jul-21 10:00:00", CultureInfo.InvariantCulture),
            Quantity = 1000,
            GrossProceed = new() { Amount = new(11000m), FxRate = 1m },
        };

        UkSection104Pools section104Pools = new(new UKTaxYear(), new ResidencyStatusRecord());
        TaxEventLists taxEventLists = new();
        taxEventLists.AddData([purchase, sale, symbolChange, repurchase]);

        UkTradeCalculator calculator = TradeCalculationHelper.CreateUkTradeCalculator(section104Pools, taxEventLists);
        List<ITradeTaxCalculation> result = calculator.CalculateTax();

        // The sale and repurchase should trigger bed and breakfast matching
        // because the repurchase (now under MVST) is within 30 days of the sale (renamed from THCB to MVST)
        ITradeTaxCalculation disposal = result.Single(r => r.AcquisitionDisposal == TradeType.DISPOSAL);
        disposal.MatchHistory.Any(m => m.TradeMatchType == TaxMatchType.BED_AND_BREAKFAST).ShouldBeTrue();
    }

    [Fact]
    public void TestSymbolChangeDoesNotAffectOtherAssets()
    {
        Trade otherTrade = new()
        {
            AssetName = "AAPL",
            AcquisitionDisposal = TradeType.ACQUISITION,
            Date = DateTime.Parse("01-Jun-21 10:00:00", CultureInfo.InvariantCulture),
            Quantity = 100,
            GrossProceed = new() { Amount = new(15000m), FxRate = 1m },
        };

        SymbolChange symbolChange = new()
        {
            AssetName = "RDW",
            OldAssetName = "GNPK",
            Date = DateTime.Parse("02-Sep-21 20:25:00", CultureInfo.InvariantCulture),
        };

        UkSection104Pools section104Pools = new(new UKTaxYear(), new ResidencyStatusRecord());
        TaxEventLists taxEventLists = new();
        taxEventLists.AddData([otherTrade, symbolChange]);

        UkTradeCalculator calculator = TradeCalculationHelper.CreateUkTradeCalculator(section104Pools, taxEventLists);
        List<ITradeTaxCalculation> result = calculator.CalculateTax();

        // AAPL trade should be unaffected
        result[0].AssetName.ShouldBe("AAPL");
    }
}
