using InvestmentTaxCalculator.Enumerations;
using InvestmentTaxCalculator.Model.Interfaces;
using InvestmentTaxCalculator.Model.TaxEvents;
using InvestmentTaxCalculator.Model.UkTaxModel.Fx;

using Microsoft.Extensions.Logging;

using Syncfusion.Blazor.Data;

using System.Text;

namespace InvestmentTaxCalculator.Model.UkTaxModel.Stocks;

/// <summary>
/// Calculate Fx and stock trades
/// </summary>
/// <param name="section104Pools"></param>
/// <param name="tradeList"></param>
public class UkTradeCalculator(UkSection104Pools section104Pools, ITradeAndCorporateActionList tradeList, TradeTaxCalculationFactory tradeTaxCalculationFactory, ILogger<UkTradeCalculator> logger) : ITradeCalculator
{
    public List<ITradeTaxCalculation> CalculateTax()
    {
        // Log all stock splits for debugging
        var stockSplits = tradeList.CorporateActions.OfType<StockSplit>().ToList();
        foreach (var split in stockSplits)
        {
            logger.LogWarning("StockSplit found: {AssetName} {SplitTo}:{SplitFrom} on {Date}",
                split.AssetName, split.SplitTo, split.SplitFrom, split.Date);
        }

        ApplySymbolChanges();
        List<ITradeTaxCalculation> tradeTaxCalculations = [.. tradeTaxCalculationFactory.GroupTrade(tradeList.Trades)];
        GroupedTradeContainer<ITradeTaxCalculation> _tradeContainer = new(tradeTaxCalculations, tradeList.CorporateActions);
        UkMatchingRules.ApplyUkTaxRuleSequence(MatchTrade, _tradeContainer, section104Pools);
        return tradeTaxCalculations;
    }

    private void ApplySymbolChanges()
    {
        var symbolChanges = tradeList.CorporateActions.OfType<SymbolChange>().ToList();
        foreach (var change in symbolChanges)
        {
            // Only rename trades that occurred BEFORE the symbol change date
            // Trades after the symbol change should already have the new symbol
            foreach (var trade in tradeList.Trades.Where(t => t.AssetName == change.OldAssetName && t.Date < change.Date))
            {
                logger.LogInformation("SymbolChange: Renaming trade {OldSymbol} -> {NewSymbol} for trade on {Date}",
                    change.OldAssetName, change.AssetName, trade.Date);
                trade.AssetName = change.AssetName;
            }
            // Also rename corporate actions that occurred before the symbol change
            foreach (var action in tradeList.CorporateActions.Where(a => a != change && a.AssetName == change.OldAssetName && a.Date < change.Date))
            {
                logger.LogInformation("SymbolChange: Renaming corporate action {OldSymbol} -> {NewSymbol} for action on {Date}",
                    change.OldAssetName, change.AssetName, action.Date);
                action.AssetName = change.AssetName;
            }
        }
    }

    public void MatchTrade(ITradeTaxCalculation trade1, ITradeTaxCalculation trade2, TaxMatchType taxMatchType, TaxableStatus taxableStatus)
    {
        TradePairSorter<ITradeTaxCalculation> tradePairSorter = new(trade1, trade2);
        if (trade1.CalculationCompleted || trade2.CalculationCompleted) return;
        MatchAdjustment matchAdjustment = tradeList.CorporateActions
            .Aggregate(new MatchAdjustment(), (matchAdjustment, corporateAction) => corporateAction.TradeMatching(trade1, trade2, matchAdjustment));
        tradePairSorter.SetQuantityAdjustmentFactor(matchAdjustment.MatchAdjustmentFactor);

        // Diagnostic logging for debugging quantity mismatch errors
        logger.LogWarning(
            "MatchTrade: {MatchType} for {AssetName} | " +
            "Acquisition: Date={AcqDate}, TotalQty={AcqTotalQty}, UnmatchedQty={AcqUnmatchedQty}, MatchQty={AcqMatchQty} | " +
            "Disposal: Date={DispDate}, TotalQty={DispTotalQty}, UnmatchedQty={DispUnmatchedQty}, MatchQty={DispMatchQty} | " +
            "AdjustmentFactor={Factor}",
            taxMatchType,
            tradePairSorter.DisposalTrade.AssetName,
            tradePairSorter.AcquisitionTrade.Date,
            tradePairSorter.AcquisitionTrade.TotalQty,
            tradePairSorter.AcquisitionTrade.UnmatchedQty,
            tradePairSorter.AcquisitionMatchQuantity,
            tradePairSorter.DisposalTrade.Date,
            tradePairSorter.DisposalTrade.TotalQty,
            tradePairSorter.DisposalTrade.UnmatchedQty,
            tradePairSorter.DisposalMatchQuantity,
            matchAdjustment.MatchAdjustmentFactor);

        // Check for potential quantity mismatch before it throws
        if (tradePairSorter.AcquisitionMatchQuantity > tradePairSorter.AcquisitionTrade.UnmatchedQty ||
            tradePairSorter.DisposalMatchQuantity > tradePairSorter.DisposalTrade.UnmatchedQty)
        {
            logger.LogError(
                "QUANTITY MISMATCH DETECTED: {MatchType} for {AssetName} | " +
                "Acquisition: Date={AcqDate}, TotalQty={AcqTotalQty}, UnmatchedQty={AcqUnmatchedQty}, MatchQty={AcqMatchQty} | " +
                "Disposal: Date={DispDate}, TotalQty={DispTotalQty}, UnmatchedQty={DispUnmatchedQty}, MatchQty={DispMatchQty} | " +
                "AdjustmentFactor={Factor} | " +
                "CorporateActions={CorporateActions}",
                taxMatchType,
                tradePairSorter.DisposalTrade.AssetName,
                tradePairSorter.AcquisitionTrade.Date,
                tradePairSorter.AcquisitionTrade.TotalQty,
                tradePairSorter.AcquisitionTrade.UnmatchedQty,
                tradePairSorter.AcquisitionMatchQuantity,
                tradePairSorter.DisposalTrade.Date,
                tradePairSorter.DisposalTrade.TotalQty,
                tradePairSorter.DisposalTrade.UnmatchedQty,
                tradePairSorter.DisposalMatchQuantity,
                matchAdjustment.MatchAdjustmentFactor,
                string.Join(", ", matchAdjustment.CorporateActions.Select(ca => $"{ca.GetType().Name}:{ca.Date:yyyy-MM-dd}")));
        }

        TradeMatch disposalTradeMatch = new()
        {
            Date = DateOnly.FromDateTime(tradePairSorter.DisposalTrade.Date),
            AssetName = tradePairSorter.DisposalTrade.AssetName,
            TradeMatchType = taxMatchType,
            MatchAcquisitionQty = tradePairSorter.AcquisitionMatchQuantity,
            MatchDisposalQty = tradePairSorter.DisposalMatchQuantity,
            BaseCurrencyMatchAllowableCost = tradePairSorter.AcquisitionTrade.GetProportionedCostOrProceed(tradePairSorter.AcquisitionMatchQuantity),
            BaseCurrencyMatchDisposalProceed = tradePairSorter.DisposalTrade.GetProportionedCostOrProceed(tradePairSorter.DisposalMatchQuantity),
            MatchedBuyTrade = tradePairSorter.AcquisitionTrade,
            MatchedSellTrade = tradePairSorter.DisposalTrade,
            AdditionalInformation = BuildInfoString(matchAdjustment.CorporateActions),
            IsTaxable = taxableStatus,
        };
        TradeMatch AcqusitionTradeMatch = disposalTradeMatch with
        {
            BaseCurrencyMatchAllowableCost = WrappedMoney.GetBaseCurrencyZero(),
            BaseCurrencyMatchDisposalProceed = WrappedMoney.GetBaseCurrencyZero(),
        };
        tradePairSorter.AcquisitionTrade.MatchQty(tradePairSorter.AcquisitionMatchQuantity);
        tradePairSorter.DisposalTrade.MatchQty(tradePairSorter.DisposalMatchQuantity);
        tradePairSorter.AcquisitionTrade.MatchHistory.Add(AcqusitionTradeMatch);
        tradePairSorter.DisposalTrade.MatchHistory.Add(disposalTradeMatch);
    }

    private static string BuildInfoString(List<CorporateAction> corporateActions)
    {
        StringBuilder sb = new();
        foreach (var action in corporateActions)
        {
            sb.AppendLine(action.Reason.ToString());
        }
        return sb.ToString();
    }
}
